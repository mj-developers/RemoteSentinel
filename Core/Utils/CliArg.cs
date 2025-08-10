using System.Text;

namespace RemoteSentinel.Core.Utils
{
    /// <summary>
    /// Utilidades para componer argumentos de línea de comandos tanto en Windows como para switches estilo FreeRDP.
    /// </summary>
    internal static class CliArg
    {
        /// Envuelve el valor en comillas si es necesario y escapa correctamente.
        /// </summary>
        internal static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";

            bool needsQuotes = false;
            foreach (char c in value)
            {
                if (char.IsWhiteSpace(c) || c == '"') { needsQuotes = true; break; }
            }
            if (!needsQuotes) return value;

            var sb = new StringBuilder();
            sb.Append('"');

            int backslashes = 0;
            foreach (char c in value)
            {
                if (c == '\\')
                {
                    backslashes++;
                }
                else if (c == '"')
                {
                    // Dobla las barras y escapa la comilla
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                    backslashes = 0;
                }
                else
                {
                    if (backslashes > 0)
                    {
                        sb.Append('\\', backslashes);
                        backslashes = 0;
                    }
                    sb.Append(c);
                }
            }
            // Barras finales antes de la comilla de cierre
            if (backslashes > 0) sb.Append('\\', backslashes * 2);

            sb.Append('"');
            return sb.ToString();
        }

        /// Construye un switch estilo FreeRDP con valor asegurando comillas y escape interno de ".
        internal static string FreeRdpKV(string key, string value)
        {
            value ??= string.Empty;
            var escaped = value.Replace("\"", "\\\"");
            return $"{key}:\"{escaped}\"";
        }

        /// Construye un switch simple clave:valor (sin comillas).
        internal static string KV(string key, string value) => $"{key}:{value}";
    }
}
