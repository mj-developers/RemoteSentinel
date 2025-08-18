namespace RemoteSentinel.Core.Models;

/// <summary>
/// Clase que actúa como contenedor principal de la configuración de la aplicación.
/// </summary>
internal sealed class AppConfig
{
    /// Configuración del servidor al que se conecta o monitoriza la aplicación.
    public ServerConfig Server { get; set; } = new();

    /// Configuración de la sonda que se utiliza para monitorizar el estado del servidor.
    public ProbeConfig Probe { get; set; } = new();

    /// Configuración local del cliente (alias visible y un identificador único de instalación).
    public LocalConfig Local { get; set; } = new();
}
