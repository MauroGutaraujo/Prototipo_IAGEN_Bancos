using Reclamos.Guardrails.Legal;

namespace Reclamos.Application.Agentes;

/// <summary>Agente Legal (simbólico): decide la ruta. El LLM nunca decide la procedencia.</summary>
public interface ILegalAgent
{
    DictamenLegal Decidir(EntradaLegal entrada);
}

/// <summary>Adaptador sobre <see cref="AgenteLegal"/> de Reclamos.Guardrails.</summary>
public sealed class LegalAgent(AgenteLegal agente) : ILegalAgent
{
    public DictamenLegal Decidir(EntradaLegal entrada) => agente.Decidir(entrada);
}
