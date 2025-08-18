using System.Diagnostics;
using RemoteSentinel.Core.Models;
using RemoteSentinel.Core.Security;

namespace RemoteSentinel.Core.Services
{
    /// <summary>
    /// Servicio encargado de iniciar y gestionar conexiones de Escritorio Remoto (RDP).
    /// </summary>
    internal sealed class RemoteDesktopLauncher
    {
        private Process? _rdpProcess;
        private string _lastRdpFilePath = "";
        private string _host = "";

        // 👉 Nuevo: evento para avisar que la sesión RDP terminó
        internal event EventHandler? Disconnected;

        internal bool IsConnected => _rdpProcess is { HasExited: false };

        internal bool Launch(AppConfig cfg)
        {
            string host = SecretProtector.Unprotect(cfg.Server.Host).Trim();
            string user = SecretProtector.Unprotect(cfg.Server.Username).Trim();
            string pass = SecretProtector.Unprotect(cfg.Server.Password).Trim();
            int port = cfg.Server.RdpPort > 0 ? cfg.Server.RdpPort : 3389;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
                return false;

            _host = host;

            string freeRdpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "wfreerdp.exe");
            if (File.Exists(freeRdpPath))
            {
                string args = $"/v:{host}:{port} /u:{EscapeCliArg(user)} /p:{EscapeCliArg(pass)} /cert:ignore /f /dynamic-resolution";
                return StartProcess(freeRdpPath, args);
            }

            // Guardar credenciales para mstsc
            try
            {
                var add = new ProcessStartInfo
                {
                    FileName = "cmdkey.exe",
                    Arguments = $"/generic:TERMSRV/{host} /user:{Quote(user)} /pass:{Quote(pass)}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(add)?.WaitForExit(3000);
            }
            catch { }

            // Generar .rdp temporal
            _lastRdpFilePath = Path.Combine(Path.GetTempPath(), $"RemoteSentinel_{host}.rdp");
            File.WriteAllText(_lastRdpFilePath,
                $"full address:s:{host}:{port}\r\nusername:s:{user}\r\nprompt for credentials:i:0\r\nadministrative session:i:0\r\nscreen mode id:i:2\r\nuse multimon:i:0\r\nredirectclipboard:i:1\r\nauthentication level:i:2\r\nenablecredsspsupport:i:1\r\n");

            return StartProcess("mstsc.exe", Quote(_lastRdpFilePath));
        }

        /// Limpia credenciales y .rdp (sin parámetros)
        internal void CleanupOnExit()
        {
            // cmdkey
            try
            {
                if (!string.IsNullOrEmpty(_host))
                {
                    var del = new ProcessStartInfo
                    {
                        FileName = "cmdkey.exe",
                        Arguments = "/delete:TERMSRV/" + _host,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(del)?.WaitForExit(2000);
                }
            }
            catch { }

            // .rdp temporal
            try
            {
                if (!string.IsNullOrEmpty(_lastRdpFilePath) && File.Exists(_lastRdpFilePath))
                    File.Delete(_lastRdpFilePath);
            }
            catch { }
        }

        private bool StartProcess(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo { FileName = exe, Arguments = args, UseShellExecute = true };
                _rdpProcess = Process.Start(psi);
                if (_rdpProcess != null)
                {
                    _rdpProcess.EnableRaisingEvents = true;
                    _rdpProcess.Exited += (_, __) =>
                    {
                        try { CleanupOnExit(); } catch { }
                        _rdpProcess = null;
                        try { Disconnected?.Invoke(this, EventArgs.Empty); } catch { }
                    };

                    // Failsafe al cerrar la app
                    AppDomain.CurrentDomain.ProcessExit += (_, __) =>
                    {
                        try { CleanupOnExit(); } catch { }
                    };
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        private static string EscapeCliArg(string s) => s.Replace("\"", "\\\"");
    }
}
