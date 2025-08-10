namespace RemoteSentinel.Core.Models;

/// <summary>
/// Clase que define la configuración de la sonda (probe) que ejecuta comprobaciones periódicas.
/// </summary>
internal sealed class ProbeConfig
{
    /// Intervalo de tiempo (en segundos) entre cada ejecución de la sonda.
    public int IntervalSeconds { get; set; } = 5;

    /// Comando que la sonda ejecutará en cada ciclo de comprobación.
    public string Command { get; set; } = "";
}
