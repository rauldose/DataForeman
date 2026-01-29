# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY DataForeman.sln ./
COPY src/DataForeman.API/DataForeman.API.csproj src/DataForeman.API/
COPY src/DataForeman.Core/DataForeman.Core.csproj src/DataForeman.Core/
COPY src/DataForeman.Infrastructure/DataForeman.Infrastructure.csproj src/DataForeman.Infrastructure/
COPY src/DataForeman.Auth/DataForeman.Auth.csproj src/DataForeman.Auth/
COPY src/DataForeman.Drivers/DataForeman.Drivers.csproj src/DataForeman.Drivers/
COPY src/DataForeman.RedisStreams/DataForeman.RedisStreams.csproj src/DataForeman.RedisStreams/
COPY src/DataForeman.FlowEngine/DataForeman.FlowEngine.csproj src/DataForeman.FlowEngine/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . .

# Build and publish
RUN dotnet publish src/DataForeman.API/DataForeman.API.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r dataforeman && useradd -r -g dataforeman dataforeman

# Copy published files
COPY --from=build /app/publish .

# Create data directory
RUN mkdir -p /app/data && chown -R dataforeman:dataforeman /app

# Switch to non-root user
USER dataforeman

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection=Data Source=/app/data/dataforeman.db

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "DataForeman.API.dll"]
