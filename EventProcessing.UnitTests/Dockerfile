FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EventProcessingSystem.sln ./
COPY EventProcessing.Core/EventProcessing.Core.csproj EventProcessing.Core/
COPY EventProcessing.Infrastructure/EventProcessing.Infrastructure.csproj EventProcessing.Infrastructure/
COPY EventProcessing.IngestionApi/EventProcessing.IngestionApi.csproj EventProcessing.IngestionApi/
COPY EventProcessing.AggregationService/EventProcessing.AggregationService.csproj EventProcessing.AggregationService/
COPY EventProcessing.UnitTests/EventProcessing.UnitTests.csproj EventProcessing.UnitTests/

RUN dotnet restore EventProcessing.UnitTests/EventProcessing.UnitTests.csproj

COPY . .
ENTRYPOINT ["dotnet", "test", "EventProcessing.UnitTests/EventProcessing.UnitTests.csproj", "--verbosity", "normal"]
