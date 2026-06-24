# Build context = repo root.
# docker build -f src/JobRadar.Worker/Dockerfile -t jobradar-worker .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/JobRadar.Domain/JobRadar.Domain.csproj src/JobRadar.Domain/
COPY src/JobRadar.Application/JobRadar.Application.csproj src/JobRadar.Application/
COPY src/JobRadar.Infrastructure/JobRadar.Infrastructure.csproj src/JobRadar.Infrastructure/
COPY src/JobRadar.Worker/JobRadar.Worker.csproj src/JobRadar.Worker/
RUN dotnet restore src/JobRadar.Worker/JobRadar.Worker.csproj

COPY src/ src/
RUN dotnet publish src/JobRadar.Worker/JobRadar.Worker.csproj -c Release -o /app --no-restore

# No HTTP surface — the plain runtime image is enough (no ASP.NET).
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "JobRadar.Worker.dll"]
