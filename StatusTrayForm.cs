using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Renci.SshNet;
using System.Collections.Generic;
using WinTimer = System.Windows.Forms.Timer;

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

        public StatusTrayForm()
        {
            InitializeComponent();

            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            Opacity = 0;

            // Cargar config (sin crear json si no existe)
            _cfg = LoadConfig(out _cfgPath);

            // Iconos
            _iconGreen = BuildDotIcon(Color.LimeGreen);
            _iconRed = BuildDotIcon(Color.Red);
            _iconYellow = BuildDotIcon(Color.Goldenrod);

            // Menú
            _menu = new ContextMenuStrip();

            _miConfigure = new ToolStripMenuItem("Configurar credenciales…");
            _miConfigure.Click += async delegate
            {
                if (_isCredsDialogOpen) return;
                _isCredsDialogOpen = true;
                _miConfigure.Enabled = false;

                try
                {
                    string host = UnprotectString(_cfg.Server.Host);
                    string user = UnprotectString(_cfg.Server.Username);
                    string pass = UnprotectString(_cfg.Server.Password);

                    using (var dlg = new CredentialsForm(host, user, pass))
                    {
                        if (dlg.ShowDialog(null) == DialogResult.OK)
                        {
                            _cfg.Server.Host = ProtectString(dlg.Host);
                            _cfg.Server.Username = ProtectString(dlg.Username);
                            _cfg.Server.Password = ProtectString(dlg.Password);
                            SaveConfig(_cfg, _cfgPath); // se crea aquí si no existía

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

            // --- item "Salir" seguro ---
            _menu.Items.Add("Salir", null, delegate
            {
                try
                {
                    // Desacopla el menú del icono de bandeja
                    if (_tray != null) _tray.ContextMenuStrip = null;

                    // Cierra el menú
                    if (_menu != null) _menu.Close(ToolStripDropDownCloseReason.ItemClicked);
                }
                catch { }

                // Salir en el siguiente tick para evitar "Collection was modified"
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

            // Refrescar estado del item cuando se abre el menú
            _menu.Opening += delegate
            {
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); } } catch { }
            // No disponer el _menu aquí para evitar carreras con eventos internos
            try { if (_timer != null) _timer.Dispose(); } catch { }
            try { if (_iconGreen != null) _iconGreen.Dispose(); } catch { }
            try { if (_iconRed != null) _iconRed.Dispose(); } catch { }
            try { if (_iconYellow != null) _iconYellow.Dispose(); } catch { }
            base.OnFormClosing(e);
        }

        // ---------- Credenciales ----------
        private bool EnsureCredentialsInteractive()
        {
            string host = UnprotectString(_cfg.Server.Host);
            string user = UnprotectString(_cfg.Server.Username);
            string pass = UnprotectString(_cfg.Server.Password);

            if (!string.IsNullOrWhiteSpace(host) &&
                !string.IsNullOrWhiteSpace(user) &&
                !string.IsNullOrWhiteSpace(pass))
                return true;

            if (_isCredsDialogOpen) return false;
            _isCredsDialogOpen = true;
            if (_miConfigure != null) _miConfigure.Enabled = false;

            try
            {
                // Primera vez: vacío (no pre-rellenar)
                using (var dlg = new CredentialsForm())
                {
                    if (dlg.ShowDialog(null) != DialogResult.OK) return false;

                    _cfg.Server.Host = ProtectString(dlg.Host);
                    _cfg.Server.Username = ProtectString(dlg.Username);
                    _cfg.Server.Password = ProtectString(dlg.Password);
                    SaveConfig(_cfg, _cfgPath); // crear al aceptar
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
                var r = await Task.Run(new Func<(bool Ok, int ActiveSessions, string Error)>(ProbeServer));
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
            }
            catch (Exception ex)
            {
                if (_tray != null) { _tray.Icon = _iconYellow; _tray.Text = "RemoteSentinel: error (" + ex.Message + ")"; }
            }
        }

        private (bool Ok, int ActiveSessions, string Error) ProbeServer()
        {
            try
            {
                string host = UnprotectString(_cfg.Server.Host);
                string user = UnprotectString(_cfg.Server.Username);
                string pass = UnprotectString(_cfg.Server.Password);

                if (string.IsNullOrWhiteSpace(host)) return (false, 0, "Host vacío");
                if (string.IsNullOrWhiteSpace(user)) return (false, 0, "Usuario vacío");
                if (string.IsNullOrWhiteSpace(_cfg.Probe.Command)) return (false, 0, "Probe.Command vacío en appsettings.json");

                var methods = new List<AuthenticationMethod>();
                if (!string.IsNullOrEmpty(pass))
                    methods.Add(new PasswordAuthenticationMethod(user, pass));

                var info = new ConnectionInfo(host, _cfg.Server.SshPort, user, methods.ToArray());
                using (var ssh = new SshClient(info))
                {
                    ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);

                    ssh.Connect();
                    using (var cmd = ssh.CreateCommand(_cfg.Probe.Command))
                    {
                        string output = (cmd.Execute() ?? "").Trim();
                        ssh.Disconnect();

                        int n;
                        if (int.TryParse(output, out n)) return (true, n, "");
                        if (string.Equals(output, "libre", StringComparison.OrdinalIgnoreCase)) return (true, 0, "");
                        if (string.Equals(output, "ocupado", StringComparison.OrdinalIgnoreCase)) return (true, 1, "");
                        return (false, 0, "Salida inesperada: '" + output + "'");
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, 0, ex.Message);
            }
        }

        // ---------- Utilidades ----------
        private static Icon BuildDotIcon(Color color)
        {
            using (var bmp = new Bitmap(16, 16))
            {
                using (var g = Graphics.FromImage(bmp))
                using (var br = new SolidBrush(color))
                using (var pen = new Pen(Color.Black))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillEllipse(br, 2, 2, 12, 12);
                    g.DrawEllipse(pen, 2, 2, 12, 12);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        // ---------- Config ----------
        private static AppConfig LoadConfig(out string cfgPath)
        {
            string basePath = AppContext.BaseDirectory;
            cfgPath = Path.Combine(basePath, "appsettings.json");
            string tplPath = Path.Combine(basePath, "appsettings.template.json");

            AppConfig cfg;

            if (File.Exists(cfgPath))
            {
                // Cargar desde json existente
                string json = File.ReadAllText(cfgPath, Encoding.UTF8);
                cfg = JsonSerializer.Deserialize<AppConfig>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new AppConfig();

                // Migración a DPAPI solo si existe fichero real
                bool migrated = false;
                if (!string.IsNullOrEmpty(cfg.Server.Password) && !IsProtected(cfg.Server.Password)) { cfg.Server.Password = ProtectString(cfg.Server.Password); migrated = true; }
                if (!string.IsNullOrEmpty(cfg.Server.Username) && !IsProtected(cfg.Server.Username)) { cfg.Server.Username = ProtectString(cfg.Server.Username); migrated = true; }
                if (!string.IsNullOrEmpty(cfg.Server.Host) && !IsProtected(cfg.Server.Host)) { cfg.Server.Host = ProtectString(cfg.Server.Host); migrated = true; }
                if (migrated) SaveConfig(cfg, cfgPath);
            }
            else if (File.Exists(tplPath))
            {
                // Cargar defaults desde plantilla SOLO en memoria (no se escribe nada)
                string json = File.ReadAllText(tplPath, Encoding.UTF8);
                cfg = JsonSerializer.Deserialize<AppConfig>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new AppConfig();
            }
            else
            {
                // Sin plantilla: usa defaults de los POCO
                cfg = new AppConfig();
            }

            if (cfg.Probe.IntervalSeconds < 2) cfg.Probe.IntervalSeconds = 5;
            return cfg;
        }

        private static void SaveConfig(AppConfig cfg, string path)
        {
            string json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        // ---------- DPAPI ----------
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RemoteSentinel|v1");
        private static bool IsProtected(string v)
        {
            return v != null && v.StartsWith("enc:", StringComparison.Ordinal);
        }
        private static string ProtectString(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            byte[] cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            return "enc:" + Convert.ToBase64String(cipher);
        }
        private static string UnprotectString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (!IsProtected(value)) return value;
            byte[] raw = Convert.FromBase64String(value.Substring(4));
            byte[] plain = ProtectedData.Unprotect(raw, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }

        // ---------- POCOs ----------
        private sealed class AppConfig
        {
            public ServerConfig Server { get; set; } = new ServerConfig();
            public ProbeConfig Probe { get; set; } = new ProbeConfig();
        }
        private sealed class ServerConfig
        {
            public string Host { get; set; } = "";     // enc:BASE64
            public int SshPort { get; set; } = 22;
            public string Username { get; set; } = ""; // enc:BASE64
            public string Password { get; set; } = ""; // enc:BASE64
        }
        private sealed class ProbeConfig
        {
            public int IntervalSeconds { get; set; } = 5;
            public string Command { get; set; } = "";
        }
    }
}
