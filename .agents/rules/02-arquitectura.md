---
trigger: always_on
description: "Arquitectura híbrida neuro-simbólica y límites entre componentes."
---
# Arquitectura

- El LLM no toma decisiones de procedencia. La ruta (Derivar/Procedente/Improcedente) la decide solo `Reclamos.Guardrails` con reglas R1–R9.
- `Reclamos.Guardrails` y `Reclamos.Domain` no dependen de IO, EF Core, HTTP ni Semantic Kernel.
- El pipeline es secuencial: OCR → clasificación → Agente Legal → CRAG → generación → guardrail de salida. Cada etapa mide y persiste su tiempo.
- Pinecone guarda solo texto normativo público. Nunca se vectorizan datos de expedientes.
- El proveedor del LLM es configuración (`models.json`), no código. Cambiar de modelo no debe requerir cambios en Application ni Guardrails.
- La condición de ablación T2 (`Rag:Enabled=false`) solo desactiva la recuperación; todo lo demás queda igual.
