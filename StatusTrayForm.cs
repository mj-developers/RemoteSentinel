using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Renci.SshNet;
using WinTimer = System.Windows.Forms.Timer;

namespace RemoteSentinel;

public partial class StatusTrayForm : Form
{
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly WinTimer _timer;
    private readonly Icon _iconGreen;
    private readonly Icon _iconRed;
    private readonly Icon _iconYellow;

    private readonly string _cfgPath;
    private AppConfig _cfg;

    public StatusTrayForm()
    {
        InitializeComponent();

        // Ventana "fantasma"
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Opacity = 0;

        // Cargar/crear configuraci�n
        _cfg = LoadConfig(out _cfgPath);

        // Primera ejecuci�n: pedir contrase�a -> abrir JSON -> cerrar app
        if (string.IsNullOrWhiteSpace(_cfg.Server.Password))
        {
            const string msg =
                "Introduce la contrase�a de inicio de sesi�n para el usuario 'root' del servidor.\n\n" +
                "La aplicaci�n se cerrar� ahora. Vuelve a abrirla despu�s de guardar.";
            MessageBox.Show(this, msg, "RemoteSentinel",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            TryOpenConfigFile(_cfgPath);
            BeginInvoke(new Action(() => Application.Exit()));
            return;
        }

        // Iconos
        _iconGreen = BuildDotIcon(Color.LimeGreen);
        _iconRed = BuildDotIcon(Color.Red);
        _iconYellow = BuildDotIcon(Color.Goldenrod);

        // Men� de bandeja (sin "Comprobar ahora")
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Abrir configuraci�n", null, (_, __) => TryOpenConfigFile(_cfgPath));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Salir", null, (_, __) => Application.Exit());

        // Icono de bandeja
        _tray = new NotifyIcon
        {
            Visible = true,
            Icon = _iconYellow,
            Text = "RemoteSentinel",
            ContextMenuStrip = _menu
        };

        // Timer de sondeo
        _timer = new WinTimer { Interval = Math.Max(2000, _cfg.Probe.IntervalSeconds * 1000) };
        _timer.Tick += async (_, __) => await CheckAndUpdateAsync();
        _timer.Start();

        _ = CheckAndUpdateAsync();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _menu?.Dispose();
        _timer?.Dispose();
        _iconGreen?.Dispose();
        _iconRed?.Dispose();
        _iconYellow?.Dispose();
        base.OnFormClosing(e);
    }

    private async Task CheckAndUpdateAsync()
    {
        try
        {
            var r = await Task.Run(ProbeServer);
            if (!r.Ok)
            {
                _tray.Icon = _iconYellow;
                _tray.Text = $"RemoteSentinel: error ({r.Error})";
                return;
            }

            var ocupado = r.ActiveSessions > 0;
            _tray.Icon = ocupado ? _iconRed : _iconGreen;
            _tray.Text = ocupado
                ? $"RemoteSentinel: {r.ActiveSessions} sesi�n(es) activa(s)"
                : "RemoteSentinel: libre";
        }
        catch (Exception ex)
        {
            _tray.Icon = _iconYellow;
            _tray.Text = $"RemoteSentinel: error ({ex.Message})";
        }
    }

    private (bool Ok, int ActiveSessions, string Error) ProbeServer()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_cfg.Server.Host))
                return (false, 0, "Host vac�o");

            if (string.IsNullOrWhiteSpace(_cfg.Probe.Command))
                return (false, 0, "Probe.Command vac�o en appsettings.json");

            var methods = new System.Collections.Generic.List<AuthenticationMethod>();
            if (!string.IsNullOrEmpty(_cfg.Server.Password))
                methods.Add(new PasswordAuthenticationMethod(_cfg.Server.Username, _cfg.Server.Password));
            // Futuro: clave privada -> PrivateKeyAuthenticationMethod

            var info = new ConnectionInfo(_cfg.Server.Host, _cfg.Server.SshPort, _cfg.Server.Username, methods.ToArray());
            using var ssh = new SshClient(info) { ConnectionInfo = { Timeout = TimeSpan.FromSeconds(5) } };

            ssh.Connect();
            using var cmd = ssh.CreateCommand(_cfg.Probe.Command);
            var output = (cmd.Execute() ?? "").Trim();
            ssh.Disconnect();

            if (int.TryParse(output, out var n)) return (true, n, "");
            if (string.Equals(output, "libre", StringComparison.OrdinalIgnoreCase)) return (true, 0, "");
            if (string.Equals(output, "ocupado", StringComparison.OrdinalIgnoreCase)) return (true, 1, "");
            return (false, 0, $"Salida inesperada: '{output}'");
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }

    private static Icon BuildDotIcon(Color color)
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        using var br = new SolidBrush(color);
        using var pen = new Pen(Color.Black);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillEllipse(br, 2, 2, 12, 12);
        g.DrawEllipse(pen, 2, 2, 12, 12);
        return Icon.FromHandle(bmp.GetHicon());
    }

    private static void TryOpenConfigFile(string path)
    {
        try
        {
            // Abrir el JSON con el editor asociado (Notepad, VSCode, etc.)
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            // Si falla abrir el archivo, abrir la carpeta
            Process.Start("explorer.exe", Path.GetDirectoryName(path)!);
        }
    }

    // ---------- Config SOLO desde template ----------
    private static AppConfig LoadConfig(out string cfgPath)
    {
        var basePath = AppContext.BaseDirectory;
        cfgPath = Path.Combine(basePath, "appsettings.json");
        var tplPath = Path.Combine(basePath, "appsettings.template.json");

        // Copiar desde template si no existe
        if (!File.Exists(cfgPath))
        {
            if (!File.Exists(tplPath))
            {
                MessageBox.Show(
                    "Falta appsettings.template.json en la carpeta de la aplicaci�n.\n" +
                    "Marca el archivo en el proyecto como Content + Copy always.",
                    "RemoteSentinel",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                throw new FileNotFoundException("No se encontr� appsettings.template.json");
            }

            File.Copy(tplPath, cfgPath, overwrite: false);
        }

        // Leer configuraci�n
        var json = File.ReadAllText(cfgPath);
        var cfg = JsonSerializer.Deserialize<AppConfig>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? throw new InvalidOperationException("No se pudo deserializar appsettings.json");

        if (cfg.Probe == null) cfg.Probe = new ProbeConfig();
        if (cfg.Probe.IntervalSeconds < 2) cfg.Probe.IntervalSeconds = 5;

        return cfg;
    }

    // ---------- POCOs ----------
    private sealed class AppConfig
    {
        public ServerConfig Server { get; set; } = new();
        public ProbeConfig Probe { get; set; } = new();
    }

    private sealed class ServerConfig
    {
        public string Host { get; set; } = "";
        public int SshPort { get; set; } = 22;
        public string Username { get; set; } = "root";
        public string Password { get; set; } = "";
    }

    private sealed class ProbeConfig
    {
        public int IntervalSeconds { get; set; } = 5;
        public string Command { get; set; } = ""; // siempre vendr� del template
    }
}
