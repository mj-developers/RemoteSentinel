namespace RemoteSentinel.Core.Models;

/// <summary>
/// Configuración local del cliente.
/// </summary>
internal sealed class LocalConfig
{
    /// Alias local que se mostrará al otro usuario al enviar la solicitud
    public string Alias { get; set; } = "";

    /// Identificador único de esta instalación del cliente
    public string InstanceId { get; set; } = "";
}