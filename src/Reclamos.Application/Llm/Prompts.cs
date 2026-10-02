using System.Text.RegularExpressions;

namespace Reclamos.Application.Llm;

/// <summary>Prompt versionado: secciones SISTEMA y USUARIO (SISTEMA puede faltar).</summary>
public sealed record PlantillaPrompt(string Version, string? Sistema, string Usuario)
{
    private static readonly Regex Comentario = new(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex Encabezado = new(@"^#\s+(SISTEMA|USUARIO)\b[^\n]*\n", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Lee un archivo de prompts/ con encabezados <c># SISTEMA</c> y <c># USUARIO</c>.</summary>
    public static PlantillaPrompt Parsear(string version, string contenido)
    {
        var texto = Comentario.Replace(contenido.Replace("\r\n", "\n"), "");
        var encabezados = Encabezado.Matches(texto);
        string? sistema = null, usuario = null;
        for (var i = 0; i < encabezados.Count; i++)
        {
            var inicio = encabezados[i].Index + encabezados[i].Length;
            var fin = i + 1 < encabezados.Count ? encabezados[i + 1].Index : texto.Length;
            var cuerpo = texto[inicio..fin].Trim();
            if (encabezados[i].Groups[1].Value == "SISTEMA")
                sistema = cuerpo;
            else
                usuario = cuerpo;
        }
        return new PlantillaPrompt(version, sistema,
            usuario ?? throw new FormatException($"El prompt {version} no tiene sección # USUARIO"));
    }
}

/// <summary>
/// Renderizador mínimo: <c>{{variable}}</c> y secciones <c>{{#lista}}…{{/lista}}</c>.
/// Los valores se insertan tal cual (sin volver a interpretar llaves). Una variable ausente es un error.
/// </summary>
public static class Renderizador
{
    // Sección cuyas etiquetas de apertura y cierre ocupan su propio renglón (como en mustache).
    private static readonly Regex Seccion = new(
        @"^[ \t]*\{\{#(\w+)\}\}[ \t]*\n(.*?)^[ \t]*\{\{/\1\}\}[ \t]*(\n|$)",
        RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex Variable = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    public static string Renderizar(
        string plantilla,
        IReadOnlyDictionary<string, string> valores,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? listas = null)
    {
        // Las secciones ya renderizadas se reservan con un marcador para que su contenido
        // (p. ej. el texto de un fragmento) no se vuelva a interpretar como plantilla.
        var reservados = new List<string>();
        var texto = Seccion.Replace(plantilla.Replace("\r\n", "\n"), m =>
        {
            var nombre = m.Groups[1].Value;
            if (listas is null || !listas.TryGetValue(nombre, out var items))
                throw new KeyNotFoundException($"Falta la lista '{nombre}' para el prompt");
            reservados.Add(string.Concat(items.Select(item => Variables(m.Groups[2].Value, item))));
            return $"\u0000{reservados.Count - 1}\u0000";
        });
        texto = Variables(texto, valores);
        return Regex.Replace(texto, "\u0000(\\d+)\u0000", m => reservados[int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)]);
    }

    private static string Variables(string texto, IReadOnlyDictionary<string, string> valores) =>
        Variable.Replace(texto, m => valores.TryGetValue(m.Groups[1].Value, out var v)
            ? v
            : throw new KeyNotFoundException($"Falta la variable '{m.Groups[1].Value}' para el prompt"));
}

/// <summary>Lee los prompts versionados de la carpeta prompts/.</summary>
public sealed class RepositorioPrompts(string directorio)
{
    public PlantillaPrompt Cargar(string version) =>
        PlantillaPrompt.Parsear(version, File.ReadAllText(Path.Combine(directorio, $"{version}.md")));

    public string LeerTexto(string archivo) => File.ReadAllText(Path.Combine(directorio, archivo));
}
