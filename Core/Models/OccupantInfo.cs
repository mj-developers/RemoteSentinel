namespace RemoteSentinel.Core.Models;

/// <summary>
/// Información publicada por el cliente que está usando el servidor.
/// </summary>
internal sealed class OccupantInfo
{
    /// Alias que verá el resto.
    public string Alias { get; set; } = "";

    /// Identificador único de la instancia local.
    public string InstanceId { get; set; } = "";

    /// Marca temporal en UTC del último latido/beacon.
    public DateTime LastSeenUtc { get; set; }
}
