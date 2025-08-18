namespace RemoteSentinel.Core.Models;

/// <summary>
/// Información de una sesión detectada en el servidor.
/// </summary>
internal sealed class SessionInfo
{
    /// Nombre de usuario que inició sesión
    public string User { get; set; } = "";

    /// Identificador numérico de la sesión
    public int Id { get; set; }

    /// Estado de la sesión
    public string State { get; set; } = "";
}
