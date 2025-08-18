using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using RemoteSentinel.Core.Models;
using RemoteSentinel.Core.Security;

namespace RemoteSentinel.Core.Services
{
    /// <summary>
    /// Sonda SSH: ejecuta el comando de estado configurado y, si hay sesiones activas,
    /// intenta leer el alias publicado por el cliente (beacon) en Linux:
    ///   /var/lib/remotesentinel/occupant.json  o  /tmp/remotesentinel/occupant.json
    /// </summary>
    internal static class SshProbe
    {
        private const string PrimaryPath = "/var/lib/remotesentinel/occupant.json";
        private const string FallbackPath = "/tmp/remotesentinel/occupant.json";

        // TTL del beacon para considerarlo válido
        private static readonly TimeSpan BeaconTtl = TimeSpan.FromSeconds(90);

        /// Realiza una prueba de conexión SSH y devuelve el estado.
        internal static ProbeResult Probe(AppConfig cfg)
        {
            var result = new ProbeResult
            {
                Ok = false,
                Error = "",
                ActiveSessions = 0,
                RemoteAlias = "",
                Sessions = new List<SessionInfo>() // (en Linux no rellenamos 'quser' por ahora)
            };

            try
            {
                string host = SecretProtector.Unprotect(cfg.Server.Host);
                string user = SecretProtector.Unprotect(cfg.Server.Username);
                string pass = SecretProtector.Unprotect(cfg.Server.Password);

                if (string.IsNullOrWhiteSpace(host)) { result.Error = "Host vacío"; return result; }
                if (string.IsNullOrWhiteSpace(user)) { result.Error = "Usuario vacío"; return result; }
                if (string.IsNullOrWhiteSpace(cfg.Probe.Command)) { result.Error = "Probe.Command vacío en appsettings.json"; return result; }

                var methods = new List<AuthenticationMethod>();
                if (!string.IsNullOrEmpty(pass))
                    methods.Add(new PasswordAuthenticationMethod(user, pass));

                var info = new ConnectionInfo(host, cfg.Server.SshPort, user, methods.ToArray());

                using var ssh = new SshClient(info);
                ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
                ssh.Connect();

                // 1) Ejecutar el comando de estado (tu script devuelve número / "libre" / "ocupado")
                using (var cmd = ssh.CreateCommand(cfg.Probe.Command))
                {
                    string output = (cmd.Execute() ?? "").Trim();

                    if (int.TryParse(output, out int n))
                    {
                        result.ActiveSessions = n;
                        result.Ok = true;
                    }
                    else if (string.Equals(output, "libre", StringComparison.OrdinalIgnoreCase))
                    {
                        result.ActiveSessions = 0;
                        result.Ok = true;
                    }
                    else if (string.Equals(output, "ocupado", StringComparison.OrdinalIgnoreCase))
                    {
                        result.ActiveSessions = 1;
                        result.Ok = true;
                    }
                    else
                    {
                        result.Error = $"Salida inesperada: '{output}'";
                        result.Ok = false;
                    }
                }

                // 2) Si hay sesiones activas, intentar leer el beacon por SFTP (con TTL y limpieza de obsoletos)
                if (result.ActiveSessions > 0)
                {
                    var occ = TryReadFreshOccupantViaSftp(host, cfg.Server.SshPort, user, pass, out bool _);
                    if (occ != null && !string.IsNullOrWhiteSpace(occ.Alias))
                    {
                        result.RemoteAlias = occ.Alias.Trim();
                    }
                }
                else
                {
                    // Opcional: si no hay sesiones, limpia beacons obsoletos si existen (no marca ocupado)
                    TryReadFreshOccupantViaSftp(host, cfg.Server.SshPort, user, pass, out bool _);
                }

                ssh.Disconnect();
                return result;
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.ActiveSessions = 0;
                result.Error = ex.Message;
                return result;
            }
        }

        // ----------------- Helpers SFTP -----------------

        /// <summary>
        /// Intenta leer el occupant.json de primario o fallback.
        /// Devuelve el OccupantInfo solo si es reciente (<= TTL). Si está obsoleto, lo borra.
        /// </summary>
        private static OccupantInfo? TryReadFreshOccupantViaSftp(string host, int port, string user, string? pass, out bool deletedStale)
        {
            deletedStale = false;
            try
            {
                using var sftp = new SftpClient(host, port, user, pass);
                sftp.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
                sftp.Connect();

                var occ = TryReadJson(sftp, PrimaryPath) ?? TryReadJson(sftp, FallbackPath);

                if (occ != null)
                {
                    var age = DateTime.UtcNow - occ.LastSeenUtc;
                    if (age > BeaconTtl)
                    {
                        // Borrar obsoleto en ambas rutas por seguridad
                        SafeDelete(sftp, PrimaryPath);
                        SafeDelete(sftp, FallbackPath);
                        deletedStale = true;
                        occ = null;
                    }
                }

                sftp.Disconnect();
                return occ;
            }
            catch
            {
                return null;
            }
        }

        private static OccupantInfo? TryReadJson(SftpClient sftp, string path)
        {
            try
            {
                if (!sftp.Exists(path)) return null;
                using var ms = new System.IO.MemoryStream();
                sftp.DownloadFile(path, ms);
                var json = Encoding.UTF8.GetString(ms.ToArray());
                if (string.IsNullOrWhiteSpace(json)) return null;

                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<OccupantInfo>(json, opts);
            }
            catch
            {
                return null;
            }
        }

        private static void SafeDelete(SftpClient sftp, string path)
        {
            try { if (sftp.Exists(path)) sftp.DeleteFile(path); } catch { }
        }
    }
}
