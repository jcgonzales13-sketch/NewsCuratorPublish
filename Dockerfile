FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["PublishNews.sln", "./"]
COPY ["src/AiNewsCurator.Domain/AiNewsCurator.Domain.csproj", "src/AiNewsCurator.Domain/"]
COPY ["src/AiNewsCurator.Application/AiNewsCurator.Application.csproj", "src/AiNewsCurator.Application/"]
COPY ["src/AiNewsCurator.Infrastructure/AiNewsCurator.Infrastructure.csproj", "src/AiNewsCurator.Infrastructure/"]
COPY ["src/AiNewsCurator.Api/AiNewsCurator.Api.csproj", "src/AiNewsCurator.Api/"]
COPY ["src/AiNewsCurator.Worker/AiNewsCurator.Worker.csproj", "src/AiNewsCurator.Worker/"]
COPY ["tests/AiNewsCurator.UnitTests/AiNewsCurator.UnitTests.csproj", "tests/AiNewsCurator.UnitTests/"]
COPY ["tests/AiNewsCurator.IntegrationTests/AiNewsCurator.IntegrationTests.csproj", "tests/AiNewsCurator.IntegrationTests/"]

RUN dotnet restore "src/AiNewsCurator.Api/AiNewsCurator.Api.csproj"

COPY . .
RUN dotnet publish "src/AiNewsCurator.Api/AiNewsCurator.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DATABASE_PATH=/var/data/ainews/ainews.db

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "AiNewsCurator.Api.dll"]
