namespace RemoteSentinel.Core.Models;
using System.Collections.Generic;

/// <summary>
/// Resultado devuelto por la sonda tras ejecutar una comprobación en el servidor.
/// </summary>
internal sealed class ProbeResult
{
    /// Indica si la comprobación se realizó correctamente.
    public bool Ok { get; set; }

    /// Mensaje de error en caso de que la comprobación falle.
    public string Error { get; set; } = "";

    /// Número de sesiones activas detectadas en el servidor.
    public int ActiveSessions { get; set; }

    /// Nombre del servidor remoto al que se conecta la sonda.
    public string RemoteAlias { get; set; } = "";

    /// Lista detallada de sesiones detectadas.
    public List<SessionInfo> Sessions { get; set; } = new();
}
