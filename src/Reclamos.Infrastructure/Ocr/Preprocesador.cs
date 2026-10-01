using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Reclamos.Infrastructure.Ocr;

/// <summary>
/// Preprocesamiento del voucher antes de Tesseract (SPEC §2.1): escala, enderezado simple,
/// escala de grises y binarización. Los parámetros se eligen con el banco de desarrollo (seed 7).
/// </summary>
public static class Preprocesador
{
    /// <summary>Ancho al que se reduce la copia usada para estimar la inclinación.</summary>
    private const int AnchoEstimacion = 400;

    public static Image<L8> Preprocesar(Image<Rgba32> original, PreprocesamientoOptions opciones)
    {
        using var img = original.Clone(x =>
        {
            if (opciones.Escala != 1f)
            {
                x.Resize((int)Math.Round(original.Width * opciones.Escala), (int)Math.Round(original.Height * opciones.Escala));
            }
        });

        var angulo = EstimarInclinacion(img, opciones);
        img.Mutate(x =>
        {
            if (angulo != 0f)
                x.Rotate(angulo).BackgroundColor(Color.White);
            x.Grayscale();
            switch (opciones.Binarizacion)
            {
                case ModoBinarizacion.Umbral:
                    x.BinaryThreshold(opciones.Umbral);
                    break;
                case ModoBinarizacion.Adaptativa:
                    x.AdaptiveThreshold();
                    break;
            }
        });
        return img.CloneAs<L8>();
    }

    /// <summary>
    /// Ángulo (convención de <c>Rotate</c> de ImageSharp) que, aplicado a la imagen, deja los
    /// renglones más horizontales: maximiza la varianza del perfil de proyección horizontal.
    /// Devuelve 0 si no se endereza.
    /// </summary>
    public static float EstimarInclinacion(Image<Rgba32> img, PreprocesamientoOptions opciones)
    {
        if (!opciones.Enderezar || opciones.AnguloMaximo <= 0f || opciones.PasoAngulo <= 0f)
            return 0f;

        var factor = Math.Min(1f, AnchoEstimacion / (float)img.Width);
        using var reducida = img.Clone(x => x
            .Resize((int)Math.Round(img.Width * factor), (int)Math.Round(img.Height * factor))
            .Grayscale());

        var mejorAngulo = 0f;
        var mejorPuntaje = Puntaje(reducida, 0f);
        var pasos = (int)Math.Floor(opciones.AnguloMaximo / opciones.PasoAngulo);
        for (var i = 1; i <= pasos; i++)
        {
            foreach (var angulo in new[] { -i * opciones.PasoAngulo, i * opciones.PasoAngulo })
            {
                var puntaje = Puntaje(reducida, angulo);
                if (puntaje > mejorPuntaje)
                {
                    (mejorAngulo, mejorPuntaje) = (angulo, puntaje);
                }
            }
        }
        return mejorAngulo;
    }

    /// <summary>Varianza de la "tinta" por fila tras rotar: renglones horizontales ⇒ perfil más contrastado.</summary>
    private static double Puntaje(Image<Rgba32> img, float angulo)
    {
        using var rotada = img.Clone(x =>
        {
            if (angulo != 0f)
                x.Rotate(angulo).BackgroundColor(Color.White);
        });

        var filas = new double[rotada.Height];
        rotada.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < acc.Height; y++)
            {
                double tinta = 0;
                foreach (var p in acc.GetRowSpan(y))
                    tinta += 255 - p.R; // la imagen ya está en escala de grises (R = G = B)
                filas[y] = tinta;
            }
        });

        var media = filas.Average();
        return filas.Sum(t => (t - media) * (t - media)) / filas.Length;
    }
}
