using Microsoft.Extensions.Options;
using Reclamos.Infrastructure.Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Reclamos.Infrastructure.Tests.Ocr;

/// <summary>Comportamiento ante fallas; no requiere Tesseract instalado.</summary>
public sealed class TesseractCliOcrAgentTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ocr-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private TesseractCliOcrAgent Agente(string ruta = "tesseract-que-no-existe") =>
        new(Options.Create(new OcrOptions { RutaTesseract = ruta, TessdataDir = _dir, TimeoutSegundos = 5 }));

    private string ImagenBlanca()
    {
        var ruta = Path.Combine(_dir, "blanca.png");
        using var img = new Image<Rgba32>(50, 50, Color.White);
        img.SaveAsPng(ruta);
        return ruta;
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Sin_tessdata_configurado_no_se_construye(string dir)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new TesseractCliOcrAgent(Options.Create(new OcrOptions { TessdataDir = dir })));
    }

    [Fact]
    public async Task Imagen_inexistente_devuelve_campos_nulos_y_error()
    {
        var lectura = await Agente().ExtraerAsync(Path.Combine(_dir, "no-existe.jpg"), TestContext.Current.CancellationToken);

        Assert.NotNull(lectura.Error);
        Assert.Null(lectura.Resultado.Monto);
        Assert.Null(lectura.Resultado.FechaHora);
        Assert.Null(lectura.Resultado.Codigo);
        Assert.Equal("", lectura.TsvCrudo);
    }

    [Fact]
    public async Task Ejecutable_inexistente_devuelve_error_sin_lanzar()
    {
        var lectura = await Agente().ExtraerAsync(ImagenBlanca(), TestContext.Current.CancellationToken);

        Assert.NotNull(lectura.Error);
        Assert.Null(lectura.Resultado.Codigo);
        Assert.Equal("desconocida", lectura.Motor.VersionTesseract);
    }

    [Fact]
    public async Task Cancelacion_del_llamador_se_propaga()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Agente().ExtraerAsync(ImagenBlanca(), cts.Token));
    }

    [Fact]
    public async Task Modelo_ausente_se_registra_como_no_encontrado()
    {
        var lectura = await Agente().ExtraerAsync(ImagenBlanca(), TestContext.Current.CancellationToken);
        Assert.Equal("no-encontrado", lectura.Motor.ModeloSha256);
    }
}
