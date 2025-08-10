using System.Diagnostics;
using RemoteSentinel.Core.Models;
using RemoteSentinel.Core.Security;

namespace RemoteSentinel.Core.Services;

/// <summary>
/// Servicio encargado de iniciar y gestionar conexiones de Escritorio Remoto (RDP).
/// </summary>
internal sealed class RemoteDesktopLauncher
{
    private Process? _rdpProcess;
    private string _lastRdpFilePath = "";

    /// Indica si existe una conexión activa de escritorio remoto.
    internal bool IsConnected => _rdpProcess != null && !_rdpProcess.HasExited;

    /// Lanza la conexión RDP con la configuración indicada.
    internal bool Launch(AppConfig cfg)
    {
        string host = SecretProtector.Unprotect(cfg.Server.Host).Trim();
        string user = SecretProtector.Unprotect(cfg.Server.Username).Trim();
        string pass = SecretProtector.Unprotect(cfg.Server.Password).Trim();
        int port = cfg.Server.RdpPort > 0 ? cfg.Server.RdpPort : 3389;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            return false;

        string freeRdpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "wfreerdp.exe");
        if (File.Exists(freeRdpPath))
        {
            string args = $"/v:{host}:{port} /u:{EscapeCliArg(user)} /p:{EscapeCliArg(pass)} /cert:ignore /f /dynamic-resolution";
            return StartProcess(freeRdpPath, args);
        }

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

        // Generar archivo .rdp temporal con la configuración
        _lastRdpFilePath = Path.Combine(Path.GetTempPath(), $"RemoteSentinel_{host}.rdp");
        File.WriteAllText(_lastRdpFilePath,
            $"full address:s:{host}:{port}\r\nusername:s:{user}\r\nprompt for credentials:i:0\r\nadministrative session:i:0\r\nscreen mode id:i:2\r\nuse multimon:i:0\r\nredirectclipboard:i:1\r\nauthentication level:i:2\r\nenablecredsspsupport:i:1\r\n");

        return StartProcess("mstsc.exe", Quote(_lastRdpFilePath));
    }

    /// Limpia credenciales y archivos temporales al salir.
    internal void CleanupOnExit(string host)
    {
        // Elimina credenciales guardadas con cmdkey
        try
        {
            var del = new ProcessStartInfo
            {
                FileName = "cmdkey.exe",
                Arguments = "/delete:TERMSRV/" + host,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(del)?.WaitForExit(2000);
        }
        catch { }

        // Borra el archivo .rdp temporal si existe
        try { if (!string.IsNullOrEmpty(_lastRdpFilePath) && File.Exists(_lastRdpFilePath)) File.Delete(_lastRdpFilePath); } catch { }
    }

    /// Inicia un proceso con el ejecutable y argumentos especificados.
    private bool StartProcess(string exe, string args)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = exe, Arguments = args, UseShellExecute = true };
            _rdpProcess = Process.Start(psi);
            if (_rdpProcess != null)
            {
                _rdpProcess.EnableRaisingEvents = true;
                _rdpProcess.Exited += (_, __) => _rdpProcess = null;
                return true;
            }
        }
        catch { }
        return false;
    }

    /// Envuelve un string entre comillas para usarlo en argumentos de línea de comandos.
    private static string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

    /// Escapa comillas para argumentos CLI (por ejemplo, en wfreerdp).
    private static string EscapeCliArg(string s) => s.Replace("\"", "\\\"");
}
