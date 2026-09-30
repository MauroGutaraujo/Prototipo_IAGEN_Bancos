using Reclamos.Application;

namespace Reclamos.Application.Tests;

/// <summary>Application define interfaces; las implementaciones de IO viven en Infrastructure.</summary>
public class ArquitecturaTests
{
    [Theory]
    [InlineData("Reclamos.Infrastructure")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Pinecone")]
    public void Application_no_referencia(string prohibida)
    {
        var referencias = typeof(AssemblyMarker).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");

        Assert.DoesNotContain(referencias, r => r.StartsWith(prohibida, StringComparison.Ordinal));
    }
}
