# DataForeman .NET/Blazor Port

This directory contains the .NET/Blazor port of the DataForeman industrial telemetry platform.

## Projects

- **DataForeman.Api** - ASP.NET Core Web API backend
- **DataForeman.Web** - Blazor Server frontend with Syncfusion components
- **DataForeman.Shared** - Shared models and DTOs
- **DataForeman.Connectivity** - MQTT connectivity service for device communication

## Prerequisites

- .NET 10 SDK
- SQLite (used as the database)
- MQTT Broker (e.g., Mosquitto, EMQX, HiveMQ)

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

### Web Application (Blazor Server)

```bash
cd dotnet/src/DataForeman.Web
dotnet run
```

The web application will start on `http://localhost:5271` by default.

### Connectivity Service

```bash
cd dotnet/src/DataForeman.Connectivity
dotnet run
```

The connectivity service connects to an MQTT broker to receive device telemetry.

## Configuration

### API Configuration

The API can be configured through environment variables or `appsettings.json`:

- `DB_PATH` - SQLite database path (default: `dataforeman.db`)
- `JWT_SECRET` - JWT signing secret
- `Jwt:Issuer` - JWT issuer
- `Jwt:Audience` - JWT audience
- `ADMIN_EMAIL` - Admin user email (default: `admin@example.com`)
- `ADMIN_PASSWORD` - Admin user password (default: `password`)

### Web Configuration

The web application can be configured through `appsettings.json`:

- `ApiBaseUrl` - Base URL for the API server (default: `http://localhost:5000`)

### MQTT Configuration

The connectivity service can be configured through `appsettings.json`:

```json
{
  "Mqtt": {
    "Broker": "localhost",
    "Port": 1883,
    "ClientId": "dataforeman-connectivity",
    "Username": "",
    "Password": ""
  }
}
```

**MQTT Topics:**
- `dataforeman/telemetry/{deviceId}` - Device telemetry data
- `dataforeman/status/{deviceId}` - Device status updates
- `dataforeman/commands/{deviceId}` - Commands to devices

## Default Credentials

After first startup, the database is seeded with:
- **Email**: `admin@example.com`
- **Password**: `password`

## Features

### Implemented
- User authentication with JWT tokens (server-side session storage)
- Dashboard management
- Device/Connection management
- Tag metadata management
- **Flow Editor** - Visual node-based flow editor using Syncfusion Diagram
  - Node palette with triggers, data, math, logic, and control nodes
  - Drag and drop node creation
  - Node configuration panel
  - Flow settings dialog
  - Test mode and deployment controls
- **Chart Composer** - Time-series chart visualization using Syncfusion Charts
  - Tag selection and configuration
  - Time range controls (rolling, fixed, shifted modes)
  - Live data auto-refresh
  - Crosshair and zoom controls
  - Chart type selection (line, area, spline, step line)
- **MQTT Connectivity** - Device communication via MQTT
  - Auto-reconnect on disconnect
  - Topic-based message routing
  - Telemetry, status, and command handling
- Syncfusion UI components (Grid, Sidebar, Menu, Inputs, Dialogs, Diagram, Charts, etc.)
- Database seeding with:
  - Admin user
  - Roles (admin, viewer)
  - Poll groups
  - Units of measure

### Planned
- Full flow execution engine
- Real-time data streaming via SignalR
- OPC UA, EtherNet/IP, S7 protocol support

## Technology Stack

- **Backend**: ASP.NET Core 10
- **Frontend**: Blazor Server (with server-side session auth)
- **UI Components**: Syncfusion Blazor
- **Database**: SQLite with Entity Framework Core
- **Authentication**: JWT Bearer tokens with BCrypt password hashing
- **Messaging**: MQTT via MQTTnet library

## License

See the main project LICENSE file.
