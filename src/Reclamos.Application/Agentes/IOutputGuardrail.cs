using Reclamos.Guardrails.Salida;

namespace Reclamos.Application.Agentes;

/// <summary>Guardrail de salida (simbólico): verifica el borrador antes de emitirlo.</summary>
public interface IOutputGuardrail
{
    VeredictoGuardrail Verificar(EntradaGuardrailSalida entrada);
}

/// <summary>Adaptador sobre <see cref="GuardrailSalida"/> de Reclamos.Guardrails.</summary>
public sealed class OutputGuardrail(GuardrailSalida guardrail) : IOutputGuardrail
{
    public VeredictoGuardrail Verificar(EntradaGuardrailSalida entrada) => guardrail.Verificar(entrada);
}
