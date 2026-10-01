using System.Text.Json;
using Reclamos.Application.Rag;
using Reclamos.Infrastructure.Rag;

namespace Reclamos.Infrastructure.Tests.Rag;

public class CorpusNormativoTests
{
    private static JsonElement Manifiesto() =>
        JsonDocument.Parse(File.ReadAllText(CorpusNormativo.ResolverRuta("knowledge/manifest.json"))).RootElement;

    [Fact]
    public void Carga_el_corpus_versionado_y_verifica_su_sha()
    {
        var m = Manifiesto();
        var corpus = CorpusNormativo.Cargar("knowledge/fragmentos.jsonl", m.GetProperty("fragmentos_sha256").GetString()!);

        Assert.Equal(m.GetProperty("n_fragmentos").GetInt32(), corpus.Fragmentos.Count);
        Assert.Equal(corpus.Fragmentos.Count, corpus.PorId.Count); // ids únicos
        Assert.All(corpus.Fragmentos, f => Assert.False(string.IsNullOrWhiteSpace(f.Texto)));
        Assert.Contains("sbs-04036-2022-art-7", corpus.PorId.Keys);
        Assert.Contains("ley-29571-art-24", corpus.PorId.Keys);
        Assert.All(corpus.Fragmentos, f => Assert.Matches(@"^[a-z0-9-]+-art-[a-z0-9-]+$", f.Id));
    }

    [Fact]
    public void Solo_la_politica_es_simulada()
    {
        var corpus = CorpusNormativo.Cargar("knowledge/fragmentos.jsonl", "");
        Assert.All(corpus.Fragmentos.Where(f => f.Tipo == "simulada"), f => Assert.StartsWith("pir-", f.Id));
        Assert.All(corpus.Fragmentos.Where(f => f.Tipo == "oficial"), f => Assert.StartsWith("https://", f.Fuente));
    }

    [Fact]
    public void Sha_distinto_falla()
    {
        Assert.Throws<InvalidOperationException>(() => CorpusNormativo.Cargar("knowledge/fragmentos.jsonl", new string('0', 64)));
    }

    [Fact]
    public void Ruta_inexistente_falla()
    {
        Assert.Throws<FileNotFoundException>(() => CorpusNormativo.ResolverRuta("knowledge/no-existe.jsonl"));
    }

    [Fact]
    public void El_namespace_se_deriva_del_sha_del_corpus()
    {
        var sha = Manifiesto().GetProperty("fragmentos_sha256").GetString()!;
        Assert.Equal($"corpus-{sha[..12]}", RagOptions.Namespace(sha));
    }
}
