# DataForeman .NET/Blazor Port

This directory contains the .NET/Blazor port of the DataForeman industrial telemetry platform.

## Projects

- **DataForeman.Api** - ASP.NET Core Web API backend
- **DataForeman.Web** - Blazor Server frontend with Syncfusion components
- **DataForeman.Shared** - Shared models and DTOs
- **DataForeman.Connectivity** - Connectivity services (placeholder for protocol drivers)

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

### Web Application (Blazor Server)

```bash
cd dotnet/src/DataForeman.Web
dotnet run
```

The web application will start on `http://localhost:5271` by default.

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
- Syncfusion UI components (Grid, Sidebar, Menu, Inputs, Dialogs, Diagram, Charts, etc.)
- Database seeding with:
  - Admin user
  - Roles (admin, viewer)
  - Poll groups
  - Units of measure

### Planned
- Full flow execution engine
- Real-time data streaming
- OPC UA, EtherNet/IP, S7 protocol support

## Messaging (NATS vs MQTT)

The original DataForeman uses **NATS** for inter-service communication between:
- Core API server
- Connectivity service (protocol drivers)
- Ingestor service (data storage)

**What NATS does:**
- Publishes telemetry data from devices
- Sends real-time status updates
- Request/reply communication for commands
- Pub/sub for log streaming

**MQTT Alternative:**
NATS can be replaced with MQTT if preferred. MQTT is better suited for:
- IoT devices with limited resources
- Unreliable network connections
- Broader ecosystem support in industrial automation

To implement MQTT support, the `DataForeman.Connectivity` project would use the `MQTTnet` library instead of `NATS.Client`.

## Technology Stack

- **Backend**: ASP.NET Core 10
- **Frontend**: Blazor Server (with server-side session auth)
- **UI Components**: Syncfusion Blazor
- **Database**: SQLite with Entity Framework Core
- **Authentication**: JWT Bearer tokens with BCrypt password hashing

## License

See the main project LICENSE file.
