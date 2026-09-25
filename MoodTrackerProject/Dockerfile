# Build context: ./MoodTrackerProject (see docker-compose.yml). Tests live outside this context on purpose (T5).

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
# Data Protection keys (admin ticket cookies) persist here via a named volume, so a container
# restart does not sign every admin out. Created root-side so the volume inherits app ownership.
USER root
RUN mkdir -p /keys && chown app:app /keys
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MoodTrackerProject.csproj", "./"]
RUN dotnet restore "MoodTrackerProject.csproj"
COPY . .
RUN dotnet build "MoodTrackerProject.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MoodTrackerProject.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MoodTrackerProject.dll"]
