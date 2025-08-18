using System.Text;
using System.Text.Json;
using Renci.SshNet;
using RemoteSentinel.Core.Models;
using RemoteSentinel.Core.Security;

namespace RemoteSentinel.Core.Services
{
    internal static class PresenceService
    {
        private const string PrimaryDir = "/var/lib/remotesentinel";
        private const string FallbackDir = "/tmp/remotesentinel";
        private const string FileName = "occupant.json";

        // Considera ocupado solo si el beacon es reciente
        private static readonly TimeSpan BeaconTtl = TimeSpan.FromSeconds(90);

        internal static async Task<bool> BeaconAsync(AppConfig cfg, string alias, string instanceId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string host = SecretProtector.Unprotect(cfg.Server.Host);
                    string user = SecretProtector.Unprotect(cfg.Server.Username);
                    string pass = SecretProtector.Unprotect(cfg.Server.Password);
                    if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user)) return false;

                    var payload = new OccupantInfo
                    {
                        Alias = alias ?? "",
                        InstanceId = instanceId ?? "",
                        LastSeenUtc = DateTime.UtcNow
                    };
                    byte[] jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

                    return TryUpload(host, cfg.Server.SshPort, user, pass, PrimaryDir, jsonBytes)
                        || TryUpload(host, cfg.Server.SshPort, user, pass, FallbackDir, jsonBytes);
                }
                catch { return false; }
            });
        }

        /// Lee el occupant reciente (o null si libre). Borra los obsoletos.
        internal static async Task<OccupantInfo?> GetCurrentOccupantAsync(AppConfig cfg, bool deleteIfStale = true)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string host = SecretProtector.Unprotect(cfg.Server.Host);
                    string user = SecretProtector.Unprotect(cfg.Server.Username);
                    string pass = SecretProtector.Unprotect(cfg.Server.Password);

                    using var sftp = new SftpClient(host, cfg.Server.SshPort, user, pass);
                    sftp.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
                    sftp.Connect();

                    foreach (var dir in new[] { PrimaryDir, FallbackDir })
                    {
                        var path = $"{dir}/{FileName}";
                        var info = TryReadJson(sftp, path);
                        if (info != null)
                        {
                            var age = DateTime.UtcNow - info.LastSeenUtc;
                            if (age <= BeaconTtl)
                            {
                                sftp.Disconnect();
                                return info;
                            }
                            else if (deleteIfStale)
                            {
                                SafeDelete(sftp, path);
                            }
                        }
                    }

                    sftp.Disconnect();
                    return null;
                }
                catch { return null; }
            });
        }

        /// Borra el beacon si nos pertenece (InstanceId coincide).
        internal static async Task<bool> ClearIfMineAsync(AppConfig cfg, string instanceId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string host = SecretProtector.Unprotect(cfg.Server.Host);
                    string user = SecretProtector.Unprotect(cfg.Server.Username);
                    string pass = SecretProtector.Unprotect(cfg.Server.Password);

                    using var sftp = new SftpClient(host, cfg.Server.SshPort, user, pass);
                    sftp.Connect();

                    bool removed = false;
                    foreach (var dir in new[] { PrimaryDir, FallbackDir })
                    {
                        var path = $"{dir}/{FileName}";
                        var info = TryReadJson(sftp, path);
                        if (info == null) { SafeDelete(sftp, path); continue; }
                        if (string.Equals(info.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                        {
                            SafeDelete(sftp, path);
                            removed = true;
                        }
                    }

                    sftp.Disconnect();
                    return removed;
                }
                catch { return false; }
            });
        }

        // ---------- helpers ----------
        private static bool TryUpload(string host, int port, string user, string pass, string dir, byte[] jsonBytes)
        {
            try
            {
                using var sftp = new SftpClient(host, port, user, pass);
                sftp.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
                sftp.Connect();

                try { sftp.CreateDirectory(dir); } catch { }
                var path = $"{dir}/{FileName}";
                var tmp = $"{path}.tmp";

                using (var ms = new MemoryStream(jsonBytes))
                    sftp.UploadFile(ms, tmp, true);

                try { if (sftp.Exists(path)) sftp.DeleteFile(path); } catch { }
                sftp.RenameFile(tmp, path);

                sftp.Disconnect();
                return true;
            }
            catch { return false; }
        }

        private static OccupantInfo? TryReadJson(SftpClient sftp, string path)
        {
            try
            {
                if (!sftp.Exists(path)) return null;
                using var ms = new MemoryStream();
                sftp.DownloadFile(path, ms);
                var json = Encoding.UTF8.GetString(ms.ToArray());
                return JsonSerializer.Deserialize<OccupantInfo>(json);
            }
            catch { return null; }
        }

        private static void SafeDelete(SftpClient sftp, string path)
        {
            try { if (sftp.Exists(path)) sftp.DeleteFile(path); } catch { }
        }
    }
}
