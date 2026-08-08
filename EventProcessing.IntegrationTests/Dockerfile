FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EventProcessingSystem.sln ./
COPY EventProcessing.Core/EventProcessing.Core.csproj EventProcessing.Core/
COPY EventProcessing.Infrastructure/EventProcessing.Infrastructure.csproj EventProcessing.Infrastructure/
COPY EventProcessing.IngestionApi/EventProcessing.IngestionApi.csproj EventProcessing.IngestionApi/
COPY EventProcessing.AggregationService/EventProcessing.AggregationService.csproj EventProcessing.AggregationService/
COPY EventProcessing.IntegrationTests/EventProcessing.IntegrationTests.csproj EventProcessing.IntegrationTests/

RUN dotnet restore EventProcessing.IntegrationTests/EventProcessing.IntegrationTests.csproj

COPY . .
ENTRYPOINT ["dotnet", "test", "EventProcessing.IntegrationTests/EventProcessing.IntegrationTests.csproj", "--verbosity", "normal"]
