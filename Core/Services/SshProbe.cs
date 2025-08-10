using Renci.SshNet;
using RemoteSentinel.Core.Models;
using RemoteSentinel.Core.Security;

namespace RemoteSentinel.Core.Services;

/// <summary>
/// Servicio para realizar una comprobación SSH en el servidor configurado y determinar el número de sesiones activas.
/// </summary>
internal static class SshProbe
{
    /// Realiza una prueba de conexión SSH y ejecuta el comando configurado para obtener el número de sesiones activas.
    internal static (bool Ok, int ActiveSessions, string Error) Probe(AppConfig cfg)
    {
        try
        {
            string host = SecretProtector.Unprotect(cfg.Server.Host);
            string user = SecretProtector.Unprotect(cfg.Server.Username);
            string pass = SecretProtector.Unprotect(cfg.Server.Password);

            if (string.IsNullOrWhiteSpace(host)) return (false, 0, "Host vacío");
            if (string.IsNullOrWhiteSpace(user)) return (false, 0, "Usuario vacío");
            if (string.IsNullOrWhiteSpace(cfg.Probe.Command)) return (false, 0, "Probe.Command vacío en appsettings.json");

            var methods = new List<AuthenticationMethod>();
            if (!string.IsNullOrEmpty(pass))
                methods.Add(new PasswordAuthenticationMethod(user, pass));

            var info = new ConnectionInfo(host, cfg.Server.SshPort, user, methods.ToArray());
            using var ssh = new SshClient(info);
            ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);

            ssh.Connect();
            using var cmd = ssh.CreateCommand(cfg.Probe.Command);
            string output = (cmd.Execute() ?? "").Trim();
            ssh.Disconnect();

            if (int.TryParse(output, out int n)) return (true, n, "");
            if (string.Equals(output, "libre", StringComparison.OrdinalIgnoreCase)) return (true, 0, "");
            if (string.Equals(output, "ocupado", StringComparison.OrdinalIgnoreCase)) return (true, 1, "");
            return (false, 0, $"Salida inesperada: '{output}'");
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }
}
