using System.Security.Cryptography;
using System.Text.Json;

namespace Reclamos.Infrastructure.Rag;

/// <summary>Fragmento de knowledge/fragmentos.jsonl (texto público o simulado).</summary>
public sealed record FragmentoCorpus(
    string Id, string Norma, string Articulo, string Tipo, string Vigencia, string Fuente, string Texto);

/// <summary>Corpus normativo versionado; se verifica su SHA-256 antes de usarlo.</summary>
public sealed class CorpusNormativo
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private CorpusNormativo(IReadOnlyList<FragmentoCorpus> fragmentos, string sha256)
    {
        Fragmentos = fragmentos;
        Sha256 = sha256;
        PorId = fragmentos.ToDictionary(f => f.Id);
    }

    public IReadOnlyList<FragmentoCorpus> Fragmentos { get; }

    public IReadOnlyDictionary<string, FragmentoCorpus> PorId { get; }

    public string Sha256 { get; }

    /// <summary>Carga el corpus. Si <paramref name="sha256Esperado"/> no está vacío y no coincide, falla.</summary>
    public static CorpusNormativo Cargar(string ruta, string sha256Esperado)
    {
        var completa = ResolverRuta(ruta);
        var bytes = File.ReadAllBytes(completa);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.IsNullOrWhiteSpace(sha256Esperado) && !string.Equals(sha, sha256Esperado, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"El corpus {completa} tiene SHA-256 {sha}, distinto del configurado en Rag:CorpusSha256 ({sha256Esperado}).");
        }

        var fragmentos = System.Text.Encoding.UTF8.GetString(bytes)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => JsonSerializer.Deserialize<FragmentoCorpus>(l, Json)
                ?? throw new InvalidOperationException("Línea vacía en el corpus"))
            .ToList();
        return new CorpusNormativo(fragmentos, sha);
    }

    /// <summary>Ruta absoluta tal cual; relativa: se busca subiendo desde el directorio actual y el de la app.</summary>
    public static string ResolverRuta(string ruta)
    {
        if (Path.IsPathRooted(ruta))
            return ruta;
        foreach (var inicio in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(inicio); dir is not null; dir = dir.Parent)
            {
                var candidata = Path.Combine(dir.FullName, ruta);
                if (File.Exists(candidata))
                    return candidata;
            }
        }
        throw new FileNotFoundException($"No se encontró el corpus normativo: {ruta}");
    }
}
