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

    [Fact]
    public void Guardrails_no_referencia_infraestructura()
    {
        var referencias = typeof(AssemblyMarker).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");

        Assert.DoesNotContain(referencias, r => Prohibidas.Any(p => r.StartsWith(p, StringComparison.Ordinal)));
    }

    [Fact]
    public void Domain_no_referencia_infraestructura()
    {
        var referencias = typeof(Expediente).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");

        Assert.DoesNotContain(referencias, r => Prohibidas.Any(p => r.StartsWith(p, StringComparison.Ordinal)));
    }
}
