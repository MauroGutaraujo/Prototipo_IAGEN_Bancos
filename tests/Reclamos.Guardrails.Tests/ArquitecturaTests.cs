using System.Reflection;
using Reclamos.Domain.Entidades;
using Reclamos.Guardrails;

namespace Reclamos.Guardrails.Tests;

/// <summary>Domain y Guardrails no dependen de IO, EF Core, HTTP ni Semantic Kernel.</summary>
public class ArquitecturaTests
{
    private static readonly string[] Prohibidas =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.SemanticKernel",
        "Microsoft.AspNetCore",
        "Pinecone",
        "SixLabors",
    ];

    public static TheoryData<string> Ensamblados => new()
    {
        typeof(Expediente).Assembly.GetName().Name!,
        typeof(AssemblyMarker).Assembly.GetName().Name!,
    };

    [Theory]
    [MemberData(nameof(Ensamblados))]
    public void No_referencia_infraestructura(string ensamblado)
    {
        var referencias = Assembly.Load(ensamblado).GetReferencedAssemblies().Select(a => a.Name ?? "");

        Assert.DoesNotContain(referencias, r => Prohibidas.Any(p => r.StartsWith(p, StringComparison.Ordinal)));
    }
}
