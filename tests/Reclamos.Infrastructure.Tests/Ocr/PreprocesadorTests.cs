using Reclamos.Infrastructure.Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Reclamos.Infrastructure.Tests.Ocr;

public class PreprocesadorTests
{
    /// <summary>Imagen blanca con franjas negras horizontales (simula renglones de texto).</summary>
    private static Image<Rgba32> Renglones()
    {
        var img = new Image<Rgba32>(400, 300, Color.White);
        img.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < acc.Height; y++)
            {
                if (y % 30 < 8 && y > 20 && y < 280)
                {
                    var fila = acc.GetRowSpan(y);
                    for (var x = 40; x < 360; x++)
                        fila[x] = new Rgba32(0, 0, 0);
                }
            }
        });
        return img;
    }

    private static PreprocesamientoOptions Opciones(ModoBinarizacion modo = ModoBinarizacion.Ninguna) =>
        new() { Enderezar = true, AnguloMaximo = 3f, PasoAngulo = 0.5f, Binarizacion = modo };

    [Fact]
    public void Imagen_derecha_no_se_rota()
    {
        using var img = Renglones();
        Assert.Equal(0f, Preprocesador.EstimarInclinacion(img, Opciones()));
    }

    [Fact]
    public void Imagen_inclinada_se_corrige_con_el_angulo_opuesto()
    {
        using var img = Renglones();
        img.Mutate(x => x.Rotate(2f).BackgroundColor(Color.White));

        var angulo = Preprocesador.EstimarInclinacion(img, Opciones());

        Assert.InRange(angulo, -2.5f, -1.5f);
    }

    [Fact]
    public void Sin_enderezar_no_se_estima()
    {
        using var img = Renglones();
        img.Mutate(x => x.Rotate(2f).BackgroundColor(Color.White));
        var opciones = Opciones();
        opciones.Enderezar = false;
        Assert.Equal(0f, Preprocesador.EstimarInclinacion(img, opciones));
    }

    [Theory]
    [InlineData(ModoBinarizacion.Umbral)]
    [InlineData(ModoBinarizacion.Adaptativa)]
    public void Binarizacion_deja_solo_blanco_y_negro(ModoBinarizacion modo)
    {
        using var img = Renglones();
        img.Mutate(x => x.GaussianBlur(1.5f));

        using var salida = Preprocesador.Preprocesar(img, Opciones(modo));

        var valores = new HashSet<byte>();
        salida.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < acc.Height; y++)
                foreach (var p in acc.GetRowSpan(y))
                    valores.Add(p.PackedValue);
        });
        Assert.Subset(new HashSet<byte> { 0, 255 }, valores);
    }

    [Fact]
    public void Escala_cambia_el_tamano()
    {
        using var img = Renglones();
        var opciones = Opciones();
        opciones.Escala = 1.5f;
        opciones.Enderezar = false;

        using var salida = Preprocesador.Preprocesar(img, opciones);

        Assert.Equal((600, 450), (salida.Width, salida.Height));
    }
}
