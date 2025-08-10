using System.Text;
using System.Text.Json;
using RemoteSentinel.Core.Models;
using RemoteSentinel.Core.Security;

namespace RemoteSentinel.Core.Services;

/// <summary>
/// Servicio para cargar y guardar la configuración de la aplicación desde el archivo JSON "appsettings.json".
/// </summary>
internal static class ConfigService
{
    /// Carga la configuración desde "appsettings.json".
    internal static AppConfig Load(out string path)
    {
        string basePath = AppContext.BaseDirectory;
        path = Path.Combine(basePath, "appsettings.json");
        string tplPath = Path.Combine(basePath, "appsettings.template.json");

        AppConfig cfg;

        // Caso 1: Si existe el archivo de configuración principal
        if (File.Exists(path))
        {
            // Leemos el contenido JSON y lo deserializamos a AppConfig
            string json = File.ReadAllText(path, Encoding.UTF8);
            cfg = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            bool migrated = false;
            // Migración de datos sensibles: si no están protegidos, se protegen
            if (!string.IsNullOrEmpty(cfg.Server.Password) && !SecretProtector.IsProtected(cfg.Server.Password)) { cfg.Server.Password = SecretProtector.Protect(cfg.Server.Password); migrated = true; }
            if (!string.IsNullOrEmpty(cfg.Server.Username) && !SecretProtector.IsProtected(cfg.Server.Username)) { cfg.Server.Username = SecretProtector.Protect(cfg.Server.Username); migrated = true; }
            if (!string.IsNullOrEmpty(cfg.Server.Host) && !SecretProtector.IsProtected(cfg.Server.Host)) { cfg.Server.Host = SecretProtector.Protect(cfg.Server.Host); migrated = true; }
            if (migrated) Save(cfg, path);
        }
        // Caso 2: Si no existe el archivo principal pero sí la plantilla
        else if (File.Exists(tplPath))
        {
            string json = File.ReadAllText(tplPath, Encoding.UTF8);
            cfg = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        // Caso 3: No existe nada, se crea una configuración vacía por defecto
        else
        {
            cfg = new();
        }

        if (cfg.Probe.IntervalSeconds < 2) cfg.Probe.IntervalSeconds = 5;
        if (cfg.Server.RdpPort <= 0) cfg.Server.RdpPort = 3389;
        return cfg;
    }

    /// Guarda la configuración en disco como JSON con formato indentado.
    internal static void Save(AppConfig cfg, string path)
    {
        string json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, Encoding.UTF8);
    }
}
