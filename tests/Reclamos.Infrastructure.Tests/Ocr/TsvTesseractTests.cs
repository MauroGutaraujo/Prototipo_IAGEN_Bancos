using Reclamos.Infrastructure.Ocr;

namespace Reclamos.Infrastructure.Tests.Ocr;

public class TsvTesseractTests
{
    private const string Encabezado =
        "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext";

    private static string Fila(int bloque, int parrafo, int linea, int palabra, string conf, string texto, int nivel = 5) =>
        $"{nivel}\t1\t{bloque}\t{parrafo}\t{linea}\t{palabra}\t0\t0\t10\t10\t{conf}\t{texto}";

    [Fact]
    public void Agrupa_palabras_por_linea_en_orden_de_lectura()
    {
        var tsv = string.Join('\n',
            Encabezado,
            Fila(1, 1, 1, 0, "-1", "", nivel: 4),
            Fila(1, 1, 1, 2, "91.5", "150.50"),
            Fila(1, 1, 1, 1, "96", "S/"),
            Fila(1, 1, 2, 1, "88.25", "Fecha:"),
            Fila(2, 1, 1, 1, "70", "Hora"));

        var lineas = TsvTesseract.Parsear(tsv);

        Assert.Equal(["S/ 150.50", "Fecha:", "Hora"], lineas.Select(l => l.Texto));
        Assert.Equal([96m, 91.5m], lineas[0].Palabras.Select(p => p.Conf));
        Assert.Equal(88.25m, lineas[1].Palabras[0].Conf);
    }

    [Fact]
    public void Ignora_conf_menos_uno_texto_vacio_y_filas_malformadas()
    {
        var tsv = string.Join("\r\n",
            Encabezado,
            Fila(1, 1, 1, 1, "-1", "fantasma"),
            Fila(1, 1, 1, 2, "95", "   "),
            "5\t1\t1\t1\t1",
            Fila(1, 1, 1, 3, "abc", "x"),
            Fila(1, 1, 1, 4, "93", "Monto"),
            "");

        var linea = Assert.Single(TsvTesseract.Parsear(tsv));
        Assert.Equal("Monto", linea.Texto);
    }

    [Fact]
    public void Tsv_vacio_no_tiene_lineas()
    {
        Assert.Empty(TsvTesseract.Parsear(""));
        Assert.Empty(TsvTesseract.Parsear(Encabezado));
    }
}
