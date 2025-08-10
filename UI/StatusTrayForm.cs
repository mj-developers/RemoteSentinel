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

namespace RemoteSentinel
{
    /// <summary>
    /// Formulario principal que gestiona el icono en la bandeja del sistema, mostrando el estado del servidor remoto y permitiendo conectarse o configurar credenciales.
    /// </summary>
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

        public StatusTrayForm()
        {
            InitializeComponent();

            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            Opacity = 0;

            // Cargar config (sin crear json si no existe)
            _cfg = ConfigService.Load(out _cfgPath);

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

            // Configurar credenciales
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

                    using (var dlg = new UI.CredentialsForm(host, user, pass))
                    {
                        if (dlg.ShowDialog(null) == DialogResult.OK)
                        {
                            _cfg.Server.Host = SecretProtector.Protect(dlg.Host);
                            _cfg.Server.Username = SecretProtector.Protect(dlg.Username);
                            _cfg.Server.Password = SecretProtector.Protect(dlg.Password);
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

            // Salir
            _menu.Items.Add("Salir", null, delegate
            {
                try
                {
                    if (_tray != null) _tray.ContextMenuStrip = null;
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

        /// Limpieza de recursos y credenciales temporales al cerrar la app.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Limpieza credenciales de cmdkey (por si usamos MSTSC) y .rdp temporal
            try
            {
                string host = SecretProtector.Unprotect(_cfg.Server.Host);
                if (!string.IsNullOrWhiteSpace(host))
                {
                    _rdp.CleanupOnExit(host);
                }
            }
            catch { }

            try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); } } catch { }
            try { if (_timer != null) _timer.Dispose(); } catch { }
            try { if (_iconGreen != null) _iconGreen.Dispose(); } catch { }
            try { if (_iconRed != null) _iconRed.Dispose(); } catch { }
            try { if (_iconYellow != null) _iconYellow.Dispose(); } catch { }
            base.OnFormClosing(e);
        }

        /// Actualiza el texto y estado del menú "Conectarse al servidor".
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

        /// Ejecuta la conexión al servidor remoto (RDP) si hay credenciales.
        private async Task HandleConnectClickAsync()
        {
            if (_rdp.IsConnected) return; // Evitar reconexión

            // Recuperar credenciales desencriptadas
            string host = SecretProtector.Unprotect(_cfg.Server.Host);
            string user = SecretProtector.Unprotect(_cfg.Server.Username);
            string pass = SecretProtector.Unprotect(_cfg.Server.Password);
            
            // Validar que están completas
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                MessageBox.Show(this, "Faltan credenciales. Configúralas primero.", "RemoteSentinel",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool ok = _rdp.Launch(_cfg);
            UpdateConnectMenuText();
            await Task.CompletedTask;
        }

        /// Comprueba si hay credenciales, y si no, muestra el diálogo para configurarlas.
        private bool EnsureCredentialsInteractive()
        {
            string host = SecretProtector.Unprotect(_cfg.Server.Host);
            string user = SecretProtector.Unprotect(_cfg.Server.Username);
            string pass = SecretProtector.Unprotect(_cfg.Server.Password);

            if (!string.IsNullOrWhiteSpace(host) &&
                !string.IsNullOrWhiteSpace(user) &&
                !string.IsNullOrWhiteSpace(pass))
                return true;

            // Evitar abrir más de un diálogo a la vez
            if (_isCredsDialogOpen) return false;
            _isCredsDialogOpen = true;
            if (_miConfigure != null) _miConfigure.Enabled = false;

            try
            {
                // Primera vez: vacío
                using (var dlg = new UI.CredentialsForm())
                {
                    if (dlg.ShowDialog(null) != DialogResult.OK) return false;

                    _cfg.Server.Host = SecretProtector.Protect(dlg.Host);
                    _cfg.Server.Username = SecretProtector.Protect(dlg.Username);
                    _cfg.Server.Password = SecretProtector.Protect(dlg.Password);
                    ConfigService.Save(_cfg, _cfgPath);
                    return true;
                }
            }
            finally
            {
                _isCredsDialogOpen = false;
                if (_miConfigure != null) _miConfigure.Enabled = true;
            }
        }

        /// Comprueba el estado del servidor mediante SSH y actualiza icono y texto.
        private async Task CheckAndUpdateAsync()
        {
            try
            {
                var r = await Task.Run(() => SshProbe.Probe(_cfg));

                // Si la comprobación falla, mostrar icono amarillo y mensaje de error
                if (!r.Ok)
                {
                    if (_tray != null) { _tray.Icon = _iconYellow; _tray.Text = "RemoteSentinel: error (" + r.Error + ")"; }
                    return;
                }

                bool ocupado = r.ActiveSessions > 0;
                if (_tray != null)
                {
                    _tray.Icon = ocupado ? _iconRed : _iconGreen;
                    _tray.Text = ocupado
                        ? "RemoteSentinel: " + r.ActiveSessions + " sesión(es) activa(s)"
                        : "RemoteSentinel: libre";
                }

                // Mantener sincronía con el texto del menú
                UpdateConnectMenuText();
            }
            catch (Exception ex)
            {
                // Error general → icono amarillo con mensaje
                if (_tray != null) { _tray.Icon = _iconYellow; _tray.Text = "RemoteSentinel: error (" + ex.Message + ")"; }
            }
        }
    }
}
