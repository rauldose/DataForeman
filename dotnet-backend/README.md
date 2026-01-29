# DataForeman .NET Backend

.NET 8+ Web API backend for DataForeman industrial data management platform.

## Architecture

The solution is organized into the following projects:

```
dotnet-backend/
├── src/
│   ├── DataForeman.API/           # ASP.NET Core Web API
│   ├── DataForeman.Core/          # Domain entities and interfaces
│   ├── DataForeman.Infrastructure/ # Data access (EF Core, SQLite)
│   ├── DataForeman.Auth/          # JWT authentication and RBAC
│   ├── DataForeman.Drivers/       # Protocol drivers (OPC UA, EIP, S7)
│   ├── DataForeman.RedisStreams/  # Redis Streams for telemetry
│   └── DataForeman.FlowEngine/    # Flow processing engine
├── tests/
│   └── DataForeman.API.Tests/     # Unit and integration tests
└── Dockerfile
```

## Features

- **REST API**: Full API parity with Node.js backend
- **JWT Authentication**: Secure token-based auth with refresh tokens
- **RBAC**: Role-based access control for all endpoints
- **SQLite**: Local metadata and time-series cache
- **Redis Streams**: Real-time telemetry messaging
- **Protocol Drivers**: OPC UA, EtherNet/IP, S7 stubs
- **Flow Engine**: .NET flow processing engine
- **Swagger/OpenAPI**: Interactive API documentation

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Redis](https://redis.io/) (optional, for telemetry streaming)

## Getting Started

### Running Locally

```bash
cd dotnet-backend

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the API
dotnet run --project src/DataForeman.API
```

The API will be available at:
- HTTP: http://localhost:5000
- Swagger UI: http://localhost:5000/swagger

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Running with Docker

```bash
# Build the image
docker build -t dataforeman-api .

# Run the container
docker run -p 8080:8080 -v dataforeman-data:/app/data dataforeman-api
```

## Configuration

Configuration is managed through `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=dataforeman.db"
  },
  "Jwt": {
    "Key": "your-secret-key",
    "Issuer": "DataForeman",
    "Audience": "DataForeman",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  },
  "Redis": {
    "ConnectionString": "localhost:6379",
    "TelemetryStream": "df:telemetry:raw",
    "ConsumerGroup": "df-processors"
  },
  "FlowEngine": {
    "Enabled": true,
    "ExecutionStream": "df:flows:execute"
  }
}
```

Environment variables can override settings using double-underscore notation:
```bash
export ConnectionStrings__DefaultConnection="Data Source=/data/dataforeman.db"
export Redis__ConnectionString="redis:6379"
```

## API Endpoints

### Authentication
- `POST /api/auth/login` - Login with email/password
- `POST /api/auth/register` - Register new user
- `POST /api/auth/refresh` - Refresh access token
- `POST /api/auth/logout` - Logout (revoke refresh token)
- `GET /api/auth/me` - Get current user info
- `POST /api/auth/password` - Change password

### Dashboards
- `GET /api/dashboards` - List dashboards
- `GET /api/dashboards/{id}` - Get dashboard
- `POST /api/dashboards` - Create dashboard
- `PUT /api/dashboards/{id}` - Update dashboard
- `DELETE /api/dashboards/{id}` - Delete dashboard

### Flows
- `GET /api/flows` - List flows
- `GET /api/flows/{id}` - Get flow
- `POST /api/flows` - Create flow
- `PUT /api/flows/{id}` - Update flow
- `POST /api/flows/{id}/deploy` - Deploy/undeploy flow
- `DELETE /api/flows/{id}` - Delete flow
- `GET /api/flows/{id}/executions` - Get execution history
- `GET /api/flows/node-types` - Get available node types

### Connectivity
- `GET /api/connectivity/connections` - List connections
- `POST /api/connectivity/connections` - Create connection
- `PUT /api/connectivity/connections/{id}` - Update connection
- `DELETE /api/connectivity/connections/{id}` - Delete connection
- `GET /api/connectivity/tags` - List tags
- `POST /api/connectivity/tags` - Create tag
- `PUT /api/connectivity/tags/{id}` - Update tag
- `DELETE /api/connectivity/tags/{id}` - Delete tag
- `GET /api/connectivity/poll-groups` - List poll groups
- `GET /api/connectivity/units` - List units of measure

### Charts
- `GET /api/charts` - List charts
- `GET /api/charts/{id}` - Get chart
- `POST /api/charts` - Create chart
- `PUT /api/charts/{id}` - Update chart
- `DELETE /api/charts/{id}` - Delete chart

### Users (Admin)
- `GET /api/users` - List users
- `GET /api/users/{id}` - Get user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user
- `GET /api/users/{id}/permissions` - Get user permissions
- `PUT /api/users/{id}/permissions` - Update user permissions

### Jobs
- `GET /api/jobs` - List background jobs
- `GET /api/jobs/{id}` - Get job details
- `POST /api/jobs` - Create job
- `POST /api/jobs/{id}/cancel` - Cancel job
- `DELETE /api/jobs/{id}` - Delete job
- `GET /api/jobs/stats` - Get job statistics

### Diagnostics
- `GET /api/diagnostics/health` - Health check
- `GET /api/diagnostics/metrics` - System metrics
- `GET /api/diagnostics/info` - System information
- `GET /api/diagnostics/audit` - Audit log
- `GET /api/diagnostics/settings` - System settings
- `PUT /api/diagnostics/settings/{key}` - Update setting

## Protocol Drivers

### OPC UA Driver
```csharp
var config = new OpcUaDriverConfig
{
    ConnectionId = Guid.NewGuid(),
    EndpointUrl = "opc.tcp://localhost:4840",
    SecurityPolicy = "None",
    SecurityMode = "None"
};

var driver = driverFactory.CreateDriver(config);
await driver.ConnectAsync();
var value = await driver.ReadTagAsync("ns=2;s=Demo.Temperature");
```

### EtherNet/IP Driver
```csharp
var config = new EtherNetIpDriverConfig
{
    ConnectionId = Guid.NewGuid(),
    Host = "192.168.1.100",
    Slot = 0
};

var driver = driverFactory.CreateDriver(config);
await driver.ConnectAsync();
var value = await driver.ReadTagAsync("MainProgram.Counter");
```

### S7 Driver
```csharp
var config = new S7DriverConfig
{
    ConnectionId = Guid.NewGuid(),
    Host = "192.168.1.200",
    Rack = 0,
    Slot = 2,
    PlcType = "S7-1500"
};

var driver = driverFactory.CreateDriver(config);
await driver.ConnectAsync();
var value = await driver.ReadTagAsync("DB1.DBD0");
```

## Flow Engine

The flow engine processes flow definitions by executing nodes in topological order:

```csharp
var flow = new FlowDefinition
{
    Id = Guid.NewGuid(),
    Name = "Temperature Alert",
    Nodes = new List<FlowNode>
    {
        new() { Id = "trigger", Type = "trigger-schedule" },
        new() { Id = "read", Type = "tag-input" },
        new() { Id = "compare", Type = "compare-greater" },
        new() { Id = "output", Type = "debug-log" }
    },
    Edges = new List<FlowEdge>
    {
        new() { Source = "trigger", Target = "read" },
        new() { Source = "read", Target = "compare" },
        new() { Source = "compare", Target = "output" }
    }
};

var result = await flowEngine.ExecuteAsync(flow);
```

## Development

### Adding a New Node Type

1. Create a class implementing `INodeExecutor`:
```csharp
public class MyCustomExecutor : NodeExecutorBase
{
    public override string NodeType => "my-custom-node";

    public override Task<NodeExecutionResult> ExecuteAsync(
        FlowNode node, 
        FlowExecutionContext context)
    {
        // Your logic here
        return Task.FromResult(NodeExecutionResult.Ok(result));
    }
}
```

2. Register in `FlowExecutionEngine`:
```csharp
RegisterExecutor(new MyCustomExecutor());
```

### Adding a New Protocol Driver

1. Create a configuration class extending `DriverConfigBase`
2. Implement `IProtocolDriver` interface
3. Update `DriverFactory.CreateDriver()` method

## License

See repository LICENSE file.
