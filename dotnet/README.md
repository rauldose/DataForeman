# DataForeman .NET/Blazor Port

This directory contains the .NET/Blazor port of the DataForeman industrial telemetry platform.

## Projects

- **DataForeman.Api** - ASP.NET Core Web API backend
- **DataForeman.Web** - Blazor WebAssembly frontend with Syncfusion components
- **DataForeman.Shared** - Shared models and DTOs
- **DataForeman.Connectivity** - Connectivity services (NATS, etc.)

## Prerequisites

- .NET 10 SDK
- SQLite (used as the database)

## Building

```bash
cd dotnet
dotnet build
```

## Running

### API Server

```bash
cd dotnet/src/DataForeman.Api
dotnet run
```

The API will start on `http://localhost:5000` by default.

### Web Application

```bash
cd dotnet/src/DataForeman.Web
dotnet run
```

The web application will be served and can be accessed through a browser.

## Configuration

### API Configuration

The API can be configured through environment variables or `appsettings.json`:

- `DB_PATH` - SQLite database path (default: `dataforeman.db`)
- `JWT_SECRET` - JWT signing secret
- `Jwt:Issuer` - JWT issuer
- `Jwt:Audience` - JWT audience

### Web Configuration

The web application can be configured through `wwwroot/appsettings.json`:

- `ApiBaseUrl` - Base URL for the API server

## Features

### Implemented
- User authentication with JWT tokens
- Dashboard management
- Device/Connection management
- Tag metadata management
- Syncfusion UI components (Grid, Inputs, Navigations, etc.)

### Planned
- Flow Studio
- Chart Composer
- Real-time data streaming via NATS
- OPC UA, EtherNet/IP, S7 protocol support

## Technology Stack

- **Backend**: ASP.NET Core 10
- **Frontend**: Blazor WebAssembly
- **UI Components**: Syncfusion Blazor
- **Database**: SQLite with Entity Framework Core
- **Authentication**: JWT Bearer tokens

## License

See the main project LICENSE file.
