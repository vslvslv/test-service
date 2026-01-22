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
RUN dotnet publish "TestService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Set IPv4 binding for Kubernetes compatibility
# This ensures the app listens on 0.0.0.0:80 instead of [::]:80 (IPv6 only)
ENV ASPNETCORE_URLS=http://0.0.0.0:80

# Copy published app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "TestService.Api.dll"]
