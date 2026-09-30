---
trigger: glob
globs: "**/*.cs, **/*.csproj"
description: "Convenciones C# / .NET 10 del proyecto."
---
# C# y .NET

- .NET 10, C# 14, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` en Domain y Guardrails.
- `async/await` en todo IO, siempre con `CancellationToken`.
- Inyección de dependencias por interfaces definidas en Application (`IOcrAgent`, `IIntentClassifier`, `ILegalAgent`, `INormativeRetriever`, `IResolutionGenerator`, `IOutputGuardrail`).
- Configuración tipada con `IOptions<T>` (`OcrOptions`, `GuardrailOptions`, `RagOptions`, `LlmOptions`).
- Tiempos con `Stopwatch.GetTimestamp()` / `Stopwatch.GetElapsedTime()`.
- Decimales monetarios con `decimal`, nunca `double`.
- Antes de agregar un paquete NuGet, consulta su versión estable actual y su documentación; no inventes APIs de Semantic Kernel ni de Pinecone.
- Tests con xUnit y Moq; un archivo de prueba por regla del guardrail.
