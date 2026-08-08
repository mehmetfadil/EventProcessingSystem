FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EventProcessingSystem.sln ./
COPY EventProcessing.Core/EventProcessing.Core.csproj EventProcessing.Core/
COPY EventProcessing.Infrastructure/EventProcessing.Infrastructure.csproj EventProcessing.Infrastructure/
COPY EventProcessing.IngestionApi/EventProcessing.IngestionApi.csproj EventProcessing.IngestionApi/

RUN dotnet restore EventProcessing.IngestionApi/EventProcessing.IngestionApi.csproj

COPY . .
RUN dotnet publish EventProcessing.IngestionApi/EventProcessing.IngestionApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

EXPOSE 5084
ENV ASPNETCORE_URLS=http://+:5084
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "EventProcessing.IngestionApi.dll"]
