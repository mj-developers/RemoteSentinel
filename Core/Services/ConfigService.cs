using System.Text;
using System.Text.Json;
using RemoteSentinel.Core.Models;
using RemoteSentinel.Core.Security;

namespace RemoteSentinel.Core.Services;

/// <summary>
/// Servicio de configuración:
/// - Lee la config de %APPDATA%\RemoteSentinel\appsettings.json
/// - En primera ejecución, copia appsettings.template.json desde la carpeta de instalación (Program Files).
/// - Nunca escribe en Program Files.
/// </summary>
internal static class ConfigService
{
    private static string UserConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RemoteSentinel");

    private static string UserConfigPath =>
        Path.Combine(UserConfigDir, "appsettings.json");

    private static string InstalledTemplatePath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.template.json");

    /// Carga la configuración. Devuelve en 'path' la ruta del JSON de usuario.
    internal static AppConfig Load(out string path)
    {
        Directory.CreateDirectory(UserConfigDir);

        // Si no existe el JSON de usuario, copiar el template instalado (si existe)
        if (!File.Exists(UserConfigPath))
        {
            try
            {
                if (File.Exists(InstalledTemplatePath))
                {
                    File.Copy(InstalledTemplatePath, UserConfigPath, overwrite: false);
                }
                else
                {
                    // crea vacío si tampoco hay template
                    File.WriteAllText(UserConfigPath, "{}", Encoding.UTF8);
                }
            }
            catch
            {
                // última red: intenta crear vacío para no romper el arranque
                try { if (!File.Exists(UserConfigPath)) File.WriteAllText(UserConfigPath, "{}", Encoding.UTF8); } catch { }
            }
        }

        path = UserConfigPath;

        // Leer JSON de usuario
        AppConfig cfg;
        try
        {
            string json = File.ReadAllText(UserConfigPath, Encoding.UTF8);
            cfg = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch
        {
            cfg = new();
        }

        // Migración de datos sensibles → proteger si vinieran en claro
        bool migrated = false;
        try
        {
            if (!string.IsNullOrEmpty(cfg.Server.Password) && !SecretProtector.IsProtected(cfg.Server.Password)) { cfg.Server.Password = SecretProtector.Protect(cfg.Server.Password); migrated = true; }
            if (!string.IsNullOrEmpty(cfg.Server.Username) && !SecretProtector.IsProtected(cfg.Server.Username)) { cfg.Server.Username = SecretProtector.Protect(cfg.Server.Username); migrated = true; }
            if (!string.IsNullOrEmpty(cfg.Server.Host) && !SecretProtector.IsProtected(cfg.Server.Host)) { cfg.Server.Host = SecretProtector.Protect(cfg.Server.Host); migrated = true; }
            if (migrated) Save(cfg, path); // guarda ya migrado en %APPDATA%
        }
        catch { /* best-effort */ }

        // Defaults mínimos
        if (cfg.Probe.IntervalSeconds < 2) cfg.Probe.IntervalSeconds = 5;
        if (cfg.Server.RdpPort <= 0) cfg.Server.RdpPort = 3389;

        return cfg;
    }

    /// Guarda SIEMPRE en %APPDATA%\RemoteSentinel\appsettings.json (se ignora Program Files).
    internal static void Save(AppConfig cfg, string _ /*path ignorado intencionalmente*/)
    {
        try
        {
            Directory.CreateDirectory(UserConfigDir);
            string json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(UserConfigPath, json, Encoding.UTF8);
        }
        catch
        {
            // Puedes loguear si tienes logger; aquí silencioso para no romper UI
        }
    }
}
