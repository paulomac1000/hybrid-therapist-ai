# syntax=docker/dockerfile:1.6

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

RUN mkdir -p local-packages && \
    wget -q https://github.com/paulomac1000/hand-codec/releases/download/v0.4.0/HandCodec.0.4.0.nupkg -O local-packages/HandCodec.0.4.0.nupkg && \
    wget -q https://github.com/paulomac1000/hand-codec/releases/download/v0.4.0/HandRuntime.0.4.0.nupkg -O local-packages/HandRuntime.0.4.0.nupkg

COPY nuget.config ./
COPY HybridTherapist.sln ./
COPY src/ ./src/
COPY tests/ ./tests/

RUN dotnet restore HybridTherapist.sln
RUN dotnet publish src/HybridTherapist.Api/HybridTherapist.Api.csproj \
        -c Release -o /publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /publish ./
COPY config/ ./config/

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Models__StackYamlPath=/app/config/stack.yaml
EXPOSE 8080
USER app
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/v1/models || exit 1
ENTRYPOINT ["dotnet", "HybridTherapist.Api.dll"]
