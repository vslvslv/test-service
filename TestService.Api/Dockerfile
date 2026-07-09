# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["TestService.Api/TestService.Api.csproj", "TestService.Api/"]
RUN dotnet restore "TestService.Api/TestService.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/TestService.Api"
RUN dotnet build "TestService.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
# Git commit surfaced by GET /api/info. Injected by CI / the publish script
# (--build-arg GIT_SHA=$(git rev-parse --short HEAD)). When empty, the SDK's
# built-in git integration or the RAILWAY_GIT_COMMIT_SHA runtime env var still
# provides the commit, so the ${GIT_SHA:+...} guard avoids overriding it with "".
ARG GIT_SHA=
RUN dotnet publish "TestService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false ${GIT_SHA:+/p:SourceRevisionId=$GIT_SHA}

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 443

# Install curl for healthcheck (runtime image has no wget)
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "TestService.Api.dll"]
