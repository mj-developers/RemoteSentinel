namespace RemoteSentinel.Core.Models;

/// <summary>
/// Clase que almacena la configuración necesaria para conectarse a un servidor remoto.
/// </summary>
internal sealed class ServerConfig
{
    /// Dirección o nombre del host del servidor remoto (IP o dominio).
    public string Host { get; set; } = "";

    /// Puerto SSH usado para conexiones seguras.
    public int SshPort { get; set; } = 22;

    /// Puerto RDP usado para conexiones de escritorio remoto.
    public int RdpPort { get; set; } = 3389;

    /// Nombre de usuario para la autenticación en el servidor.
    public string Username { get; set; } = "";

    /// Contraseña para la autenticación en el servidor.
    public string Password { get; set; } = "";
}
