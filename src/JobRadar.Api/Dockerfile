# Build context = repo root (projects reference each other across src/).
# docker build -f src/JobRadar.Api/Dockerfile -t jobradar-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the project graph first so this layer caches on unchanged deps.
COPY global.json ./
COPY src/JobRadar.Domain/JobRadar.Domain.csproj src/JobRadar.Domain/
COPY src/JobRadar.Application/JobRadar.Application.csproj src/JobRadar.Application/
COPY src/JobRadar.Infrastructure/JobRadar.Infrastructure.csproj src/JobRadar.Infrastructure/
COPY src/JobRadar.Api/JobRadar.Api.csproj src/JobRadar.Api/
RUN dotnet restore src/JobRadar.Api/JobRadar.Api.csproj

COPY src/ src/
RUN dotnet publish src/JobRadar.Api/JobRadar.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
EXPOSE 8080
# Railway (and most PaaS) inject $PORT — bind Kestrel to it, default 8080 locally.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet JobRadar.Api.dll"]
