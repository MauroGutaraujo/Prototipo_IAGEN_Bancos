namespace Reclamos.Infrastructure.Ocr;

/// <summary>Configuración del Agente OCR (sección <c>Ocr</c>).</summary>
public sealed class OcrOptions
{
    public const string Seccion = "Ocr";

    /// <summary>Ejecutable de Tesseract (en PATH o ruta absoluta).</summary>
    public string RutaTesseract { get; set; } = "tesseract";

    /// <summary>Carpeta con el <c>spa.traineddata</c> fijado (SPEC §2.1). Obligatoria.</summary>
    public string TessdataDir { get; set; } = "";

    public string Idioma { get; set; } = "spa";

    public int Psm { get; set; } = 6;

    public int TimeoutSegundos { get; set; } = 15;

    /// <summary>SHA-256 esperado del modelo de idioma; /health lo compara con el archivo real.</summary>
    public string ModeloSha256 { get; set; } = "";

    public PreprocesamientoOptions Preprocesamiento { get; set; } = new();
}

public enum ModoBinarizacion
{
    Ninguna,
    Umbral,
    Adaptativa,
}

/// <summary>Parámetros del preprocesamiento. Se eligen SOLO con el banco de desarrollo (seed 7).</summary>
public sealed class PreprocesamientoOptions
{
    /// <summary>Factor de escala antes de enderezar (1 = sin cambio).</summary>
    public float Escala { get; set; } = 1f;

    public bool Enderezar { get; set; } = true;

    /// <summary>Ángulo máximo (grados) que se explora al enderezar.</summary>
    public float AnguloMaximo { get; set; } = 3f;

    public float PasoAngulo { get; set; } = 0.5f;

    public ModoBinarizacion Binarizacion { get; set; } = ModoBinarizacion.Ninguna;

    /// <summary>Umbral de luminancia (0–1) para <see cref="ModoBinarizacion.Umbral"/>.</summary>
    public float Umbral { get; set; } = 0.5f;
}
