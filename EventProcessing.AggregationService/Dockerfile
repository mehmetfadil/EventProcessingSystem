FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EventProcessingSystem.sln ./
COPY EventProcessing.Core/EventProcessing.Core.csproj EventProcessing.Core/
COPY EventProcessing.Infrastructure/EventProcessing.Infrastructure.csproj EventProcessing.Infrastructure/
COPY EventProcessing.AggregationService/EventProcessing.AggregationService.csproj EventProcessing.AggregationService/

RUN dotnet restore EventProcessing.AggregationService/EventProcessing.AggregationService.csproj

COPY . .
RUN dotnet publish EventProcessing.AggregationService/EventProcessing.AggregationService.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

EXPOSE 5094
ENV ASPNETCORE_URLS=http://+:5094
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "EventProcessing.AggregationService.dll"]
