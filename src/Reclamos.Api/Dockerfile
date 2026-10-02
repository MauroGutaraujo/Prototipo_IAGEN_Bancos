# API del prototipo con Tesseract y el modelo spa fijado (SPEC §2.1). Contexto de build: raíz del repositorio.
#   docker build -f src/Reclamos.Api/Dockerfile -t reclamos-api .
# Imágenes "noble" (Ubuntu 24.04): el mismo Tesseract de apt que usa CI.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Reclamos.Api/Reclamos.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
ARG TESSDATA_URL=https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/923915d4ced2a7235221788285785a29c4a42d4a/spa.traineddata
ARG TESSDATA_SHA256=6f2e04d02774a18f01bed44b1111f2cd7f3ba7ac9dc4373cd3f898a40ea6b464
RUN apt-get update \
    && apt-get install -y --no-install-recommends tesseract-ocr curl ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /opt/tessdata \
    && curl -sSfL -o /opt/tessdata/spa.traineddata "$TESSDATA_URL" \
    && echo "$TESSDATA_SHA256  /opt/tessdata/spa.traineddata" | sha256sum -c -
WORKDIR /app
COPY --from=build /app .
COPY prompts ./prompts
COPY config ./config
COPY knowledge/fragmentos.jsonl knowledge/manifest.json ./knowledge/
ENV ASPNETCORE_URLS=http://+:8080 \
    Ocr__TessdataDir=/opt/tessdata \
    Banco__DirectorioDatos=/datos
EXPOSE 8080
ENTRYPOINT ["dotnet", "Reclamos.Api.dll"]
