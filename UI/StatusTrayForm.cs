using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using RemoteSentinel.Core.Models;
using RemoteSentinel.Core.Security;
using RemoteSentinel.Core.Services;
using RemoteSentinel.Core.Utils;
using WinTimer = System.Windows.Forms.Timer;
using System.Collections.Generic;

namespace RemoteSentinel
{
    public partial class StatusTrayForm : Form
    {
        private NotifyIcon _tray;
        private ContextMenuStrip _menu;
        private WinTimer _timer;
        private Icon _iconGreen, _iconRed, _iconYellow;

        private AppConfig _cfg = new AppConfig();
        private string _cfgPath = "";

        // Control de reentradas del diálogo
        private bool _isCredsDialogOpen = false;
        private ToolStripMenuItem _miConfigure;

        // Conexión remota
        private ToolStripMenuItem _miConnect;

        // Servicios refactorizados
        private readonly RemoteDesktopLauncher _rdp = new RemoteDesktopLauncher();

        // Menú “Enviar solicitud de conexión” y cache de sesiones del último probe
        private ToolStripMenuItem _miRequestTurn;
        private List<SessionInfo> _lastProbeSessions = new();

        // Presencia/beacon del ocupante
        private WinTimer _presenceTimer; // envía beacons periódicos mientras esté conectado

        public StatusTrayForm()
        {
            InitializeComponent();

            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            Opacity = 0;

            // Cargar config (sin crear json si no existe)
            _cfg = ConfigService.Load(out _cfgPath);

            // Asegurar Local + InstanceId
            if (_cfg.Local == null) _cfg.Local = new LocalConfig();
            if (string.IsNullOrWhiteSpace(_cfg.Local.InstanceId))
            {
                _cfg.Local.InstanceId = Guid.NewGuid().ToString("N");
                ConfigService.Save(_cfg, _cfgPath);
            }

            // Iconos
            _iconGreen = IconFactory.BuildDotIcon(Color.LimeGreen);
            _iconRed = IconFactory.BuildDotIcon(Color.Red);
            _iconYellow = IconFactory.BuildDotIcon(Color.Goldenrod);

            // Menú
            _menu = new ContextMenuStrip();

            // Conectar
            _miConnect = new ToolStripMenuItem("Conectarse al servidor");
            _miConnect.Click += async delegate { await HandleConnectClickAsync(); };
            _menu.Items.Add(_miConnect);

            // Configurar credenciales…
            _miConfigure = new ToolStripMenuItem("Configurar credenciales…");
            _miConfigure.Click += async delegate
            {
                if (_isCredsDialogOpen) return;
                _isCredsDialogOpen = true;
                _miConfigure.Enabled = false;

                try
                {
                    string host = SecretProtector.Unprotect(_cfg.Server.Host);
                    string user = SecretProtector.Unprotect(_cfg.Server.Username);
                    string pass = SecretProtector.Unprotect(_cfg.Server.Password);

                    using (var dlg = new UI.CredentialsForm(host, user, pass, _cfg.Local?.Alias ?? ""))
                    {
                        if (dlg.ShowDialog(null) == DialogResult.OK)
                        {
                            _cfg.Server.Host = SecretProtector.Protect(dlg.Host);
                            _cfg.Server.Username = SecretProtector.Protect(dlg.Username);
                            _cfg.Server.Password = SecretProtector.Protect(dlg.Password);

                            // guardar alias (no va cifrado)
                            if (_cfg.Local == null) _cfg.Local = new LocalConfig();
                            _cfg.Local.Alias = dlg.Alias;

                            ConfigService.Save(_cfg, _cfgPath);
                            await CheckAndUpdateAsync();
                        }
                    }
                }
                finally
                {
                    _isCredsDialogOpen = false;
                    _miConfigure.Enabled = true;
                }
            };
            _menu.Items.Add(_miConfigure);

            _menu.Items.Add(new ToolStripSeparator());

            // Salir (seguro)
            _menu.Items.Add("Salir", null, async delegate
            {
                try
                {
                    // Limpieza de beacon al salir (best-effort)
                    await CleanupBeaconAsync();

                    if (_tray != null) _tray.ContextMenuStrip = null; // desacoplar
                    if (_menu != null) _menu.Close(ToolStripDropDownCloseReason.ItemClicked);
                }
                catch { }

                var exitTimer = new WinTimer();
                exitTimer.Interval = 1;
                exitTimer.Tick += delegate
                {
                    exitTimer.Stop();
                    exitTimer.Dispose();

                    try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); } } catch { }
                    try { Close(); } catch { }
                    try { Application.ExitThread(); } catch { }
                };
                exitTimer.Start();
            });

            // Refrescar estado cuando se abre el menú
            _menu.Opening += delegate
            {
                UpdateConnectMenuText();
                if (_miConfigure != null) _miConfigure.Enabled = !_isCredsDialogOpen;
            };

            // Bandeja
            _tray = new NotifyIcon();
            _tray.Visible = true;
            _tray.Icon = _iconYellow;
            _tray.Text = "RemoteSentinel";
            _tray.ContextMenuStrip = _menu;

            // 👉 Suscribirse a la desconexión de la RDP: limpiar beacon y refrescar estado
            _rdp.Disconnected += async (_, __) =>
            {
                try
                {
                    _presenceTimer?.Stop();
                    await CleanupBeaconAsync();   // borra occupant.json si es nuestro
                    await CheckAndUpdateAsync();  // refresca icono/tooltip
                }
                catch { }
            };

            // Inicio diferido
            Shown += async delegate
            {
                if (!EnsureCredentialsInteractive())
                {
                    if (_tray != null) _tray.Visible = false;
                    Application.Exit();
                    return;
                }

                _timer = new WinTimer();
                _timer.Interval = Math.Max(2000, _cfg.Probe.IntervalSeconds * 1000);
                _timer.Tick += async delegate { await CheckAndUpdateAsync(); };
                _timer.Start();

                await CheckAndUpdateAsync();
            };
        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            // Limpieza credenciales de cmdkey y .rdp temporal (la versión nueva no necesita host)
            try { _rdp.CleanupOnExit(); } catch { }

            // Detener timer de presencia y borrar beacon (best-effort)
            try { _presenceTimer?.Stop(); } catch { }
            await CleanupBeaconAsync();

            try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); } } catch { }
            try { if (_timer != null) _timer.Dispose(); } catch { }
            try { _iconGreen?.Dispose(); } catch { }
            try { _iconRed?.Dispose(); } catch { }
            try { _iconYellow?.Dispose(); } catch { }
            base.OnFormClosing(e);
        }

        // ---------- Menú Conectar ----------
        private void UpdateConnectMenuText()
        {
            bool connected = _rdp.IsConnected;
            if (connected)
            {
                string host = SecretProtector.Unprotect(_cfg.Server.Host);
                _miConnect.Text = "Conectado a " + host;
                _miConnect.Enabled = false;
            }
            else
            {
                _miConnect.Text = "Conectarse al servidor";
                _miConnect.Enabled = true;
            }
        }

        private async Task HandleConnectClickAsync()
        {
            if (_rdp.IsConnected) return; // ya conectado

            // Validación de credenciales como antes
            string host = SecretProtector.Unprotect(_cfg.Server.Host);
            string user = SecretProtector.Unprotect(_cfg.Server.Username);
            string pass = SecretProtector.Unprotect(_cfg.Server.Password);
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                MessageBox.Show(this, "Faltan credenciales. Configúralas primero.", "RemoteSentinel",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool ok = _rdp.Launch(_cfg);
            UpdateConnectMenuText();

            if (ok)
            {
                // ---- Publicar beacon inmediato con alias + instanceId ----
                try
                {
                    var alias = string.IsNullOrWhiteSpace(_cfg.Local?.Alias) ? Environment.UserName : _cfg.Local.Alias;
                    var instanceId = _cfg.Local?.InstanceId ?? "";
                    _ = await PresenceService.BeaconAsync(_cfg, alias, instanceId);
                }
                catch { /* silencioso */ }

                // ---- Timer para renovar beacon mientras siga conectado ----
                if (_presenceTimer == null)
                {
                    _presenceTimer = new WinTimer { Interval = 10_000 }; // 10s para mayor reactividad
                    _presenceTimer.Tick += async (_, __) =>
                    {
                        try
                        {
                            if (_rdp.IsConnected)
                            {
                                var alias = string.IsNullOrWhiteSpace(_cfg.Local?.Alias) ? Environment.UserName : _cfg.Local.Alias;
                                await PresenceService.BeaconAsync(_cfg, alias, _cfg.Local?.InstanceId ?? "");
                            }
                            else
                            {
                                _presenceTimer.Stop();
                                await CleanupBeaconAsync();   // ← limpieza inmediata si ya no hay RDP
                                await CheckAndUpdateAsync();  // ← refresca el estado de la bandeja
                            }
                        }
                        catch { /* silencioso */ }
                    };
                }
                _presenceTimer.Start();
            }

            await Task.CompletedTask;
        }

        // ---------- Credenciales ----------
        private bool EnsureCredentialsInteractive()
        {
            string host = SecretProtector.Unprotect(_cfg.Server.Host);
            string user = SecretProtector.Unprotect(_cfg.Server.Username);
            string pass = SecretProtector.Unprotect(_cfg.Server.Password);

            if (!string.IsNullOrWhiteSpace(host) &&
                !string.IsNullOrWhiteSpace(user) &&
                !string.IsNullOrWhiteSpace(pass))
                return true;

            if (_isCredsDialogOpen) return false;
            _isCredsDialogOpen = true;
            if (_miConfigure != null) _miConfigure.Enabled = false;

            try
            {
                // Primera vez: pasar alias actual (si lo hubiera)
                using (var dlg = new UI.CredentialsForm("", "", "", _cfg.Local?.Alias ?? ""))
                {
                    if (dlg.ShowDialog(null) != DialogResult.OK) return false;

                    _cfg.Server.Host = SecretProtector.Protect(dlg.Host);
                    _cfg.Server.Username = SecretProtector.Protect(dlg.Username);
                    _cfg.Server.Password = SecretProtector.Protect(dlg.Password);

                    if (_cfg.Local == null) _cfg.Local = new LocalConfig();
                    _cfg.Local.Alias = dlg.Alias;

                    ConfigService.Save(_cfg, _cfgPath); // crear al aceptar
                    return true;
                }
            }
            finally
            {
                _isCredsDialogOpen = false;
                if (_miConfigure != null) _miConfigure.Enabled = true;
            }
        }

        // ---------- Lógica principal ----------
        private async Task CheckAndUpdateAsync()
        {
            try
            {
                var r = await Task.Run(() => SshProbe.Probe(_cfg)); // útil para "Enviar solicitud…"
                if (!r.Ok)
                {
                    if (_tray != null) { _tray.Icon = _iconYellow; _tray.Text = "RemoteSentinel: error (" + r.Error + ")"; }
                    return;
                }

                // Cache de sesiones para "Enviar solicitud…"
                _lastProbeSessions = r.Sessions ?? new List<SessionInfo>();

                // ← CLAVE: solo el beacon decide si mostramos "ocupado"
                var occ = await PresenceService.GetCurrentOccupantAsync(_cfg); // ya con TTL
                bool ocupadoPorBeacon = (occ != null);
                string alias = occ?.Alias;

                if (_tray != null)
                {
                    _tray.Icon = ocupadoPorBeacon ? _iconRed : _iconGreen;
                    _tray.Text = ocupadoPorBeacon
                        ? $"RemoteSentinel: Ocupado por {(string.IsNullOrWhiteSpace(alias) ? "desconocido" : alias)}"
                        : "RemoteSentinel: libre";
                }

                // El menú de "Enviar solicitud…" puede seguir el estado de sesiones (por si hay clientes antiguos sin beacon)
                bool haySesiones = r.ActiveSessions > 0;
                UpdateConnectMenuText();
                UpdateActionsForOccupancy(ocupadoPorBeacon || haySesiones);
            }
            catch (Exception ex)
            {
                if (_tray != null) { _tray.Icon = _iconYellow; _tray.Text = "RemoteSentinel: error (" + ex.Message + ")"; }
            }
        }


        // Inserta/actualiza el menú "Enviar solicitud de conexión" si procede
        private void UpdateActionsForOccupancy(bool ocupado)
        {
            // Quitar menú previo si lo hubiera
            if (_miRequestTurn != null)
            {
                int idx = _menu.Items.IndexOf(_miRequestTurn);
                if (idx >= 0)
                {
                    if (idx > 0 && _menu.Items[idx - 1] is ToolStripSeparator) _menu.Items.RemoveAt(idx - 1);
                    _menu.Items.RemoveAt(idx);
                }
                _miRequestTurn.Dispose();
                _miRequestTurn = null;
            }

            // Si estoy conectado desde mi app, no ofrecer pedir turno
            if (_rdp.IsConnected) return;

            // Si está ocupado por otro (cualquier sesión activa o beacon) mostramos "Enviar solicitud de conexión"
            if (ocupado)
            {
                _miRequestTurn = new ToolStripMenuItem("Enviar solicitud de conexión");
                _miRequestTurn.Click += async (_, __) =>
                {
                    string alias = string.IsNullOrWhiteSpace(_cfg.Local?.Alias) ? Environment.UserName : _cfg.Local.Alias;
                    string text = $"Solicitud de conexión de {alias}.";

                    var (ok, sent, fail) = await MessageService.SendToActiveSessionsAsync(_cfg, _lastProbeSessions, text);

                    MessageBox.Show(this,
                        ok
                            ? $"Solicitud enviada ({sent} enviada(s){(fail > 0 ? $", {fail} fallida(s)" : "")})."
                            : "No se pudo enviar la solicitud (no hay sesiones activas o falló el envío).",
                        "RemoteSentinel",
                        MessageBoxButtons.OK,
                        ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                };

                // Insertar antes de "Salir"
                int exitIndex = Math.Max(0, _menu.Items.Count - 1);
                _menu.Items.Insert(exitIndex, new ToolStripSeparator());
                _menu.Items.Insert(exitIndex, _miRequestTurn);
            }
        }

        // ---------- Utilidades privadas ----------

        /// <summary>
        /// Borra el beacon si el archivo pertenece a esta instancia (best-effort).
        /// </summary>
        private async Task CleanupBeaconAsync()
        {
            try
            {
                await PresenceService.ClearIfMineAsync(_cfg, _cfg.Local?.InstanceId ?? "");
            }
            catch { /* silencioso */ }
        }
    }
}
