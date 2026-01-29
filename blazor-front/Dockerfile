# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file
COPY DataForeman.BlazorUI.csproj ./
RUN dotnet restore

# Copy source code
COPY . .

# Build and publish
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r dataforeman && useradd -r -g dataforeman dataforeman

# Copy published files
COPY --from=build /app/publish .

# Set ownership
RUN chown -R dataforeman:dataforeman /app

# Switch to non-root user
USER dataforeman

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8081
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port
EXPOSE 8081

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8081/ || exit 1

# Entry point
ENTRYPOINT ["dotnet", "DataForeman.BlazorUI.dll"]
