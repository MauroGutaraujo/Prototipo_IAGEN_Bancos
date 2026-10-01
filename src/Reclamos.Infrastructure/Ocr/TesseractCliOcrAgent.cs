using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Reclamos.Application.Agentes;
using Reclamos.Guardrails.Legal;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Reclamos.Infrastructure.Ocr;

/// <summary>
/// Agente OCR sobre la CLI de Tesseract: <c>tesseract stdin stdout -l spa --psm 6 --tessdata-dir &lt;dir&gt; tsv</c>.
/// Ante cualquier falla devuelve campos nulos (R1 deriva) y el motivo en <see cref="LecturaOcr.Error"/>.
/// </summary>
public sealed class TesseractCliOcrAgent : IOcrAgent
{
    private static readonly ResultadoOcr Vacio = new(null, null, null, null, 0m, 0m, 0m);

    private readonly OcrOptions _opciones;
    private readonly Lazy<Task<MotorOcr>> _motor;

    public TesseractCliOcrAgent(IOptions<OcrOptions> opciones)
    {
        _opciones = opciones.Value;
        if (string.IsNullOrWhiteSpace(_opciones.TessdataDir))
        {
            throw new InvalidOperationException(
                "Falta Ocr:TessdataDir: carpeta con el spa.traineddata fijado en la SPEC §2.1.");
        }
        _motor = new Lazy<Task<MotorOcr>>(DetectarMotorAsync);
    }

    /// <summary>Versión de Tesseract y SHA-256 del modelo (se detectan una vez).</summary>
    public Task<MotorOcr> MotorAsync(CancellationToken ct) => _motor.Value.WaitAsync(ct);

    public async Task<LecturaOcr> ExtraerAsync(string rutaImagen, CancellationToken ct)
    {
        var motor = await MotorAsync(ct);
        try
        {
            byte[] png;
            using (var img = await Image.LoadAsync<Rgba32>(rutaImagen, ct))
            using (var pre = Preprocesador.Preprocesar(img, _opciones.Preprocesamiento))
            using (var ms = new MemoryStream())
            {
                await pre.SaveAsPngAsync(ms, ct);
                png = ms.ToArray();
            }

            string[] args =
            [
                "stdin", "stdout",
                "-l", _opciones.Idioma,
                "--psm", _opciones.Psm.ToString(CultureInfo.InvariantCulture),
                "--tessdata-dir", _opciones.TessdataDir,
                "tsv",
            ];
            var (codigo, salida, error) = await EjecutarAsync(args, png, ct);
            if (codigo != 0)
                return new LecturaOcr(Vacio, salida, $"tesseract terminó con código {codigo}: {error.Trim()}", motor);

            return new LecturaOcr(ExtractorCampos.Extraer(TsvTesseract.Parsear(salida)), salida, null, motor);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new LecturaOcr(Vacio, "", $"{ex.GetType().Name}: {ex.Message}", motor);
        }
    }

    /// <summary>Ejecuta Tesseract con <paramref name="entrada"/> por stdin; respeta timeout y cancelación.</summary>
    private async Task<(int Codigo, string Salida, string Error)> EjecutarAsync(
        IEnumerable<string> args, byte[]? entrada, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(_opciones.RutaTesseract)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proceso = Process.Start(psi)
            ?? throw new InvalidOperationException($"No se pudo iniciar {_opciones.RutaTesseract}");
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(_opciones.TimeoutSegundos));

        try
        {
            var salida = proceso.StandardOutput.ReadToEndAsync(limite.Token);
            var error = proceso.StandardError.ReadToEndAsync(limite.Token);
            if (entrada is not null)
                await proceso.StandardInput.BaseStream.WriteAsync(entrada, limite.Token);
            proceso.StandardInput.Close();

            await proceso.WaitForExitAsync(limite.Token);
            return (proceso.ExitCode, await salida, await error);
        }
        catch (OperationCanceledException)
        {
            try { proceso.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            if (ct.IsCancellationRequested)
                throw;
            throw new TimeoutException($"Tesseract excedió {_opciones.TimeoutSegundos} s");
        }
    }

    private async Task<MotorOcr> DetectarMotorAsync()
    {
        var version = "desconocida";
        try
        {
            var (codigo, salida, error) = await EjecutarAsync(["--version"], null, CancellationToken.None);
            // Según la versión, Tesseract imprime la versión por stdout o por stderr.
            var primera = (salida + "\n" + error).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(l => l.StartsWith("tesseract", StringComparison.OrdinalIgnoreCase));
            if (codigo == 0 && primera is not null)
                version = primera;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // El ejecutable no está disponible: la lectura fallará y se registrará el error.
        }

        return new MotorOcr(version, await Sha256ModeloAsync(_opciones) ?? "no-encontrado");
    }

    /// <summary>SHA-256 (hex, minúsculas) del <c>{Idioma}.traineddata</c>; null si no existe.</summary>
    public static async Task<string?> Sha256ModeloAsync(OcrOptions opciones, CancellationToken ct = default)
    {
        var ruta = Path.Combine(opciones.TessdataDir, $"{opciones.Idioma}.traineddata");
        if (!File.Exists(ruta))
            return null;
        await using var archivo = File.OpenRead(ruta);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(archivo, ct));
    }
}
