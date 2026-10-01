using Reclamos.Domain.Enums;
using static Reclamos.Guardrails.Tests.Legal.Datos;

namespace Reclamos.Guardrails.Tests.Legal;

public class R9Tests
{
    [Fact]
    public void Operacion_autenticada_desde_dispositivo_registrado_es_improcedente()
    {
        var r = Decidir(Entrada(Tx(), Intencion.C2));
        Assert.Equal(new(Ruta.Improcedente, "R9", "OPERACION_AUTENTICADA"), r);
    }
}
