using Renci.SshNet;
using RemoteSentinel.Core.Models;
using RemoteSentinel.Core.Security;

namespace RemoteSentinel.Core.Services;

/// <summary>
/// Servicio para enviar mensajes a usuarios/sesiones del servidor remoto utilizando 'msg.exe' ejecutado en el host vía SSH.
/// </summary>
internal static class MessageService
{
    /// Envía un mensaje a una sesión concreta por su ID (acepta int o int?).
    internal static async Task<bool> SendToSessionAsync(AppConfig cfg, int? sessionId, string message)
    {
        int id = sessionId ?? 0;
        if (id <= 0) return false;

        string host = SecretProtector.Unprotect(cfg.Server.Host);
        string safe = EscapeMessage(message);

        string cmd = $"msg {id} /server:{host} \"{safe}\"";

        var (ok, _, _) = await RunAsync(cfg, cmd);
        return ok;
    }

    /// Envía un mensaje a un usuario
    internal static async Task<bool> SendToUserAsync(AppConfig cfg, string usernameOrDomainUser, string message)
    {
        string host = SecretProtector.Unprotect(cfg.Server.Host);
        string safe = EscapeMessage(message);

        string cmd = $"msg {usernameOrDomainUser} /server:{host} \"{safe}\"";

        var (ok, _, _) = await RunAsync(cfg, cmd);
        return ok;
    }

    /// Envía el mensaje a todas las sesiones activas proporcionadas.
    internal static async Task<(bool Ok, int SentCount, int FailCount)> SendToActiveSessionsAsync(AppConfig cfg, IEnumerable<SessionInfo> sessions, string message)
    {
        int sent = 0, fail = 0;

        foreach (var s in sessions)
        {
            if (!"Active".Equals(s.State, StringComparison.OrdinalIgnoreCase)) continue;

            bool ok = await SendToSessionAsync(cfg, s.Id, message);
            if (ok) sent++; else fail++;
        }

        return (sent > 0, sent, fail);
    }

    // ----------------------------------------------------------------
    // Utilidades privadas
    // ----------------------------------------------------------------

    /// Ejecuta un comando por SSH en el servidor configurado.
    private static async Task<(bool Ok, string StdOut, string StdErr)> RunAsync(AppConfig cfg, string command)
    {
        return await Task.Run(() =>
        {
            try
            {
                string host = SecretProtector.Unprotect(cfg.Server.Host);
                string user = SecretProtector.Unprotect(cfg.Server.Username);
                string pass = SecretProtector.Unprotect(cfg.Server.Password);

                if (string.IsNullOrWhiteSpace(host)) return (false, "", "Host vacío");
                if (string.IsNullOrWhiteSpace(user)) return (false, "", "Usuario vacío");

                var methods = new List<AuthenticationMethod>();
                if (!string.IsNullOrEmpty(pass))
                    methods.Add(new PasswordAuthenticationMethod(user, pass));

                // En tu ServerConfig, SshPort es int (no nullable)
                var info = new ConnectionInfo(host, cfg.Server.SshPort, user, methods.ToArray());

                using var ssh = new SshClient(info);
                ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);

                ssh.Connect();

                using var cmd = ssh.CreateCommand(command);
                string output = (cmd.Execute() ?? "").Trim();

                // 0 = éxito (si ExitStatus es null, asumimos no éxito: -1)
                int code = cmd.ExitStatus ?? -1; // ← cambio clave

                ssh.Disconnect();

                return (code == 0, output, code == 0 ? "" : $"ExitCode={code}");
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        });
    }

    /// Escapa comillas dobles para construir la cadena del comando msg.
    private static string EscapeMessage(string text)
        => (text ?? string.Empty).Replace("\"", "\\\"");
}
