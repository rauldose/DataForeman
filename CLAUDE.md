# CLAUDE.md - AI Assistant Guide for DataForeman

## Project Overview

**DataForeman** is an industrial telemetry collection and visualization platform built with .NET and Blazor. It connects to industrial devices (OPC UA, EtherNet/IP, S7), collects time-series data, and provides a modern web UI for visualization and data processing.

- **Status**: Beta (v0.4.3) - Active development, APIs may change
- **Repository**: https://github.com/orionK-max/DataForeman
- **Website**: https://www.DataForeman.app

### Core Capabilities
- Industrial device connectivity (EtherNet/IP, Siemens S7, OPC UA)
- Time-series data collection and storage (SQLite)
- Visual Flow Editor for real-time data processing
- Dashboards and chart visualization
- Multi-user permission system with feature-based RBAC

---

## Architecture

**Modular Monolith Pattern**: The Blazor frontend includes all backend services directly, enabling high-performance direct database access without HTTP overhead.

```
┌─────────────────────────────────────────────────┐
│              Blazor Server App                  │
│  ┌─────────────┐  ┌─────────────┐              │
│  │   Pages/    │  │  Services/  │              │
│  │  Components │◄─┤ DataService │              │
│  └─────────────┘  └──────┬──────┘              │
│                          │                      │
│                   ┌──────▼──────┐              │
│                   │  EF Core    │              │
│                   │  DbContext  │              │
│                   └──────┬──────┘              │
└──────────────────────────┼──────────────────────┘
                           │
                    ┌──────▼──────┐
                    │   SQLite    │
                    │  Database   │
                    └─────────────┘
```

---

## Repository Structure

```
DataForeman/
├── blazor-front/                    # Blazor Server frontend + integrated backend
│   ├── Components/
│   │   ├── Layout/
│   │   │   ├── MainLayout.razor     # Sidebar navigation + main content
│   │   │   ├── EmptyLayout.razor    # Login page layout
│   │   │   └── ReconnectModal.razor # Blazor Server reconnection UI
│   │   ├── Pages/
│   │   │   ├── Home.razor           # Dashboard overview
│   │   │   ├── Flows.razor          # Flow Studio (visual editor)
│   │   │   ├── Charts.razor         # Chart Composer
│   │   │   ├── Connectivity.razor   # Device/tag management
│   │   │   ├── Diagnostics.razor    # System diagnostics
│   │   │   ├── Login.razor          # Authentication page
│   │   │   ├── Users.razor          # Admin user management
│   │   │   └── Profile.razor        # User profile
│   │   ├── App.razor                # Root app component
│   │   └── Routes.razor             # Routing + auth cascading
│   ├── Services/
│   │   ├── DataService.cs           # Direct DB access (no HTTP)
│   │   ├── AuthStateProvider.cs     # JWT-based auth state
│   │   ├── AppStateService.cs       # Global state management
│   │   ├── NodePluginRegistry.cs    # Flow node definitions
│   │   └── SimulatorService.cs      # Simulated device data
│   ├── wwwroot/
│   │   └── app.css                  # Global styles
│   ├── Program.cs                   # App configuration + DI
│   └── DataForeman.BlazorUI.csproj  # .NET 10 project file
│
├── dotnet-backend/                  # Standalone .NET Web API (alternative)
│   └── src/
│       ├── DataForeman.API/         # REST API controllers
│       │   └── Controllers/
│       ├── DataForeman.Core/        # Domain entities + interfaces
│       │   └── Entities/
│       ├── DataForeman.Infrastructure/
│       │   └── Data/
│       │       ├── DataForemanDbContext.cs  # EF Core DbContext
│       │       └── Migrations/
│       ├── DataForeman.Auth/        # JWT + password services
│       ├── DataForeman.Drivers/     # Protocol driver implementations
│       ├── DataForeman.FlowEngine/  # Flow execution engine
│       └── DataForeman.RedisStreams/
│
├── blazor-front.tests/              # Playwright UI tests
│   └── DataForeman.BlazorUI.Tests/
│
└── docs/                            # Documentation
```

---

## Technology Stack

### Runtime
- **.NET 10** (latest)
- **Blazor Server** - Interactive server-side rendering
- **C#** with nullable reference types enabled

### Frontend (blazor-front/)
- **Syncfusion Blazor v32** - Enterprise UI components
- **Syncfusion Diagram** - Flow Studio visual editor
- **Syncfusion Charts** - Data visualization
- **Razor Components** (.razor files)
- CSS custom properties for theming (dark mode default)

### Backend Services (integrated in Blazor app)
- **Entity Framework Core 10** - ORM
- **SQLite** - Local database
- **BCrypt.Net** - Password hashing
- **System.IdentityModel.Tokens.Jwt** - JWT authentication

### Protocol Drivers
- **OPC UA** - node-opcua compatible
- **EtherNet/IP** - Allen-Bradley PLCs
- **Siemens S7** - S7-1200/1500

---

## Development Workflow

### Quick Start
```bash
cd blazor-front

# Restore dependencies
dotnet restore

# Run the application
dotnet run

# Access at https://localhost:5001 or http://localhost:5000
# Default login: admin@local / admin123
```

### Common Commands

| Command | Description |
|---------|-------------|
| `dotnet restore` | Restore NuGet packages |
| `dotnet build` | Build the project |
| `dotnet run` | Run with hot reload |
| `dotnet watch run` | Run with automatic rebuild |
| `dotnet test` | Run tests |
| `dotnet ef migrations add <Name>` | Add EF migration |
| `dotnet ef database update` | Apply migrations |

### Configuration

**appsettings.json**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=dataforeman.db"
  },
  "Jwt": {
    "SecretKey": "your-secret-key-min-32-chars",
    "Issuer": "DataForeman",
    "Audience": "DataForeman",
    "AccessTokenExpirationHours": 1,
    "RefreshTokenExpirationDays": 7
  },
  "SyncfusionLicenseKey": "your-license-key"
}
```

---

## Code Conventions

### C# Style
- PascalCase for public members, classes, methods
- camelCase for private fields and local variables
- Prefix private fields with `_` (e.g., `_dataService`)
- Nullable reference types enabled (`#nullable enable`)
- XML documentation comments on public APIs

### File Organization
- **Pages**: `Components/Pages/<PageName>.razor`
- **Layouts**: `Components/Layout/<LayoutName>.razor`
- **Services**: `Services/<ServiceName>.cs`
- **Entities**: `dotnet-backend/src/DataForeman.Core/Entities/`
- **Controllers**: `dotnet-backend/src/DataForeman.API/Controllers/`

### Razor Component Conventions
- PascalCase for component files (Login.razor, FlowStudio.razor)
- Lowercase URL routes (`@page "/login"`)
- Parameters use PascalCase with `[Parameter]` attribute
- Inject services at top of code block

```razor
@page "/flows"
@inject DataService DataService
@inject AppStateService AppState

<h1>Flows</h1>

@code {
    private List<Flow>? _flows;

    protected override async Task OnInitializedAsync()
    {
        _flows = await DataService.GetFlowsAsync();
    }
}
```

---

## Key Services

### DataService.cs
Direct database access without HTTP layer. Contains all CRUD operations:

```csharp
// User management
Task<User?> ValidateUserAsync(string email, string password)
Task<User?> CreateUserAsync(string email, string password, string displayName)
Task<bool> UpdateUserAsync(User user)
Task<bool> DeleteUserAsync(Guid userId)

// Flows
Task<List<Flow>> GetFlowsAsync()
Task<Flow?> GetFlowAsync(Guid id)
Task<Flow> CreateFlowAsync(Flow flow)
Task<bool> DeployFlowAsync(Guid id)
Task<bool> UndeployFlowAsync(Guid id)

// Charts, Dashboards, Connections, Tags...
```

### AuthStateProvider.cs
Custom Blazor authentication state provider:

```csharp
// JWT token management
Task<string> GenerateTokenAsync(User user)
Task<bool> ValidateTokenAsync(string token)
Task<AuthenticationState> GetAuthenticationStateAsync()

// Token storage (localStorage via JS interop)
Task SetTokenAsync(string accessToken, string refreshToken)
Task<string?> GetTokenAsync()
Task ClearTokensAsync()
```

### AppStateService.cs
Centralized state management with event-based notifications:

```csharp
// Current user and permissions
User? CurrentUser { get; }
bool HasPermission(string feature, string operation)

// Telemetry cache for real-time values
Dictionary<string, TagValue> TelemetryCache { get; }

// Statistics
int ConnectionCount { get; }
int FlowCount { get; }
int TagCount { get; }

// State change events
event Action OnStateChanged;
```

---

## Key Patterns

### Permission System (Feature-Based RBAC)

**Entity Structure**:
```csharp
public class UserPermission
{
    public Guid UserId { get; set; }
    public string Feature { get; set; }  // e.g., "flows", "charts"
    public bool CanCreate { get; set; }
    public bool CanRead { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
}
```

**Checking Permissions in Components**:
```razor
@inject AppStateService AppState

@if (AppState.HasPermission("flows", "create"))
{
    <button @onclick="CreateFlow">New Flow</button>
}
```

**API Authorization Policies**:
```csharp
// In Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("FlowManagement", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") ||
            context.User.HasClaim("permission", "flows:update")));
});
```

### JWT Authentication Flow

1. User submits email/password to Login.razor
2. DataService validates credentials with BCrypt
3. AuthStateProvider generates JWT + refresh token
4. Tokens stored in localStorage via JS interop
5. Subsequent requests include JWT in Authorization header
6. Token refresh happens automatically on expiry

### Flow Execution Engine

**Topological Sort Pattern**:
```csharp
public class FlowExecutionEngine
{
    public async Task<FlowExecutionResult> ExecuteAsync(
        FlowDefinition flow,
        Dictionary<string, object> parameters)
    {
        // 1. Validate no circular dependencies
        // 2. Sort nodes by dependency order
        // 3. Execute each node with outputs from previous nodes
        // 4. Return aggregated results
    }
}
```

**Built-in Node Executors**:
- Triggers: ManualTrigger, ScheduleTrigger
- I/O: TagInput, TagOutput
- Math: Add, Subtract, Multiply, Divide
- Compare: Equal, Greater, Less
- Logic: If
- Debug: DebugLog
- Script: CSharpScript (inline C# code)

---

## Database

### Entity Framework Core with SQLite

**DbContext Location**: `dotnet-backend/src/DataForeman.Infrastructure/Data/DataForemanDbContext.cs`

**Key Entities**:
- `User` - Accounts with BCrypt password hash
- `UserPermission` - Feature-based permissions (composite key)
- `RefreshToken` - JWT refresh token tracking
- `Flow` - Node-based flow definitions (JSON)
- `FlowExecution` - Execution history
- `ChartConfig` - Chart definitions with axes/series
- `Connection` - Device connection configs
- `TagMetadata` - Tag registry
- `Dashboard` - Dashboard layouts

### Seed Data (OnModelCreating)

The DbContext seeds comprehensive demo data:
- **Admin user**: admin@local / admin123
- **Poll groups**: 10 predefined rates (50ms to 60000ms)
- **Units**: 22 units (°C, °F, Pa, V, A, W, rpm, etc.)
- **Sample connections**: Production-PLC-01, OPC-Server-Main, Demo Simulator
- **Sample tags**: 15 simulator tags (temperature, pressure, level, etc.)
- **Sample flows**: 3 example flows with different patterns
- **Sample charts**: 3 charts with multi-axis configuration

### Migration Strategy
- Beta: One migration per release, can modify during development
- Naming: `XXX_vY.Z_release`
- Auto-run on startup via `EnsureCreated()`

---

## Testing

### Playwright UI Tests (blazor-front.tests/)

```bash
cd blazor-front.tests/DataForeman.BlazorUI.Tests

# Run all tests
dotnet test

# Run specific test file
dotnet test --filter "FullyQualifiedName~PageNavigationTests"
```

**Test Files**:
- `ApiIntegrationTests.cs` - API endpoint testing
- `PageNavigationTests.cs` - Page routing
- `FlowStudioTests.cs` - Flow editor interactions
- `ChartComposerTests.cs` - Chart creation
- `NodePropertiesTests.cs` - Node configuration

**Test Pattern**:
```csharp
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class PageNavigationTests : PageTest
{
    [Test]
    public async Task NavigateToFlows_ShowsFlowList()
    {
        await Page.GotoAsync("https://localhost:5001/flows");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var grid = Page.Locator(".e-grid");
        await Expect(grid).ToBeVisibleAsync();
    }
}
```

### Backend Unit Tests

```bash
cd dotnet-backend/tests/DataForeman.API.Tests
dotnet test
```

**Test Areas**:
- `AuthServiceTests.cs` - JWT token generation/validation
- `FlowEngineTests.cs` - Flow execution logic
- `DriverTests.cs` - Protocol driver behavior

---

## CI/CD

### GitHub Actions (`.github/workflows/`)

**blazor-ci.yml** - Blazor frontend:
```yaml
on:
  push:
    paths: ['blazor-front/**']

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build -c Release
```

**dotnet-ci.yml** - .NET backend:
```yaml
on:
  push:
    paths: ['dotnet-backend/**']

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - run: dotnet restore
      - run: dotnet build -c Release
      - run: dotnet test --collect:"XPlat Code Coverage"
```

---

## Common Tasks

### Adding a New Page

1. Create page in `Components/Pages/<PageName>.razor`:
```razor
@page "/new-page"
@attribute [Authorize]
@inject DataService DataService

<h1>New Page</h1>

@code {
    protected override async Task OnInitializedAsync()
    {
        // Load data
    }
}
```

2. Add navigation link in `MainLayout.razor`

3. Add permission check if needed:
```razor
@if (AppState.HasPermission("feature", "read"))
{
    <NavLink href="new-page">New Page</NavLink>
}
```

### Adding a New Entity

1. Create entity in `DataForeman.Core/Entities/`:
```csharp
public class NewEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
```

2. Add DbSet to `DataForemanDbContext.cs`:
```csharp
public DbSet<NewEntity> NewEntities { get; set; }
```

3. Configure in `OnModelCreating`:
```csharp
modelBuilder.Entity<NewEntity>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
});
```

4. Add migration:
```bash
dotnet ef migrations add AddNewEntity
```

5. Add CRUD methods to `DataService.cs`

### Adding a New Flow Node

1. Create executor in `FlowEngine/NodeExecutors.cs`:
```csharp
public class MyNodeExecutor : INodeExecutor
{
    public string NodeType => "myNode";

    public async Task<NodeResult> ExecuteAsync(
        NodeContext context,
        Dictionary<string, object> inputs)
    {
        // Node logic here
        return new NodeResult { Success = true, Output = result };
    }
}
```

2. Register in `FlowExecutionEngine`:
```csharp
RegisterExecutor(new MyNodeExecutor());
```

3. Add to `NodePluginRegistry.cs` for UI:
```csharp
new NodeDefinition
{
    Type = "myNode",
    Category = "Custom",
    DisplayName = "My Node",
    Inputs = new[] { "input1" },
    Outputs = new[] { "output1" }
}
```

### Adding a New API Endpoint

1. Create/update controller in `Controllers/`:
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NewController : ControllerBase
{
    private readonly DataForemanDbContext _context;

    public NewController(DataForemanDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Policy = "FeatureManagement")]
    public async Task<ActionResult<List<NewEntity>>> GetAll()
    {
        return await _context.NewEntities.ToListAsync();
    }
}
```

2. Add authorization policy if needed in `Program.cs`

---

## Security Considerations

- **Password hashing**: BCrypt with automatic salt
- **JWT signing**: HMAC-SHA256 with minimum 32-char key
- **Token validation**: Issuer, Audience, Lifetime checks
- **Refresh tokens**: Tracked with revocation support
- **CORS**: Configured for frontend origins only
- **Permission checks**: On every protected endpoint/component
- **User agent tracking**: Refresh tokens store UA for audit
- **Never commit secrets**: Use appsettings.Development.json locally

---

## Important Files

| Purpose | Location |
|---------|----------|
| App entry point | `blazor-front/Program.cs` |
| Root component | `blazor-front/Components/App.razor` |
| Main layout | `blazor-front/Components/Layout/MainLayout.razor` |
| Data service | `blazor-front/Services/DataService.cs` |
| Auth provider | `blazor-front/Services/AuthStateProvider.cs` |
| State service | `blazor-front/Services/AppStateService.cs` |
| DbContext | `dotnet-backend/src/DataForeman.Infrastructure/Data/DataForemanDbContext.cs` |
| Flow engine | `dotnet-backend/src/DataForeman.FlowEngine/FlowExecutionEngine.cs` |
| JWT service | `dotnet-backend/src/DataForeman.Auth/JwtTokenService.cs` |
| API controllers | `dotnet-backend/src/DataForeman.API/Controllers/` |
| UI tests | `blazor-front.tests/DataForeman.BlazorUI.Tests/` |

---

## Syncfusion Components Used

| Component | Usage |
|-----------|-------|
| `SfGrid` | Data tables (flows, charts, users, tags) |
| `SfDiagramComponent` | Flow Studio visual editor |
| `SfChart` | Chart Composer visualizations |
| `SfTab` | Tabbed interfaces |
| `SfDialog` | Modal dialogs |
| `SfTextBox` | Text inputs |
| `SfDropDownList` | Select dropdowns |
| `SfButton` | Buttons |
| `SfToast` | Notifications |

**License**: Syncfusion requires a license key in `appsettings.json`

---

## Troubleshooting

### Database Issues
```bash
# Delete and recreate database
rm dataforeman.db
dotnet run  # Auto-creates with seed data
```

### Syncfusion License Errors
Add valid license key to `appsettings.json`:
```json
{
  "SyncfusionLicenseKey": "YOUR_LICENSE_KEY"
}
```

### JWT Token Issues
```csharp
// Clear tokens and re-authenticate
await AuthStateProvider.ClearTokensAsync();
NavigationManager.NavigateTo("/login");
```

### Blazor Server Reconnection
If SignalR disconnects, the `ReconnectModal.razor` component shows automatically. Refresh the page to reconnect.

---

## Useful Documentation

- [Syncfusion Blazor Documentation](https://blazor.syncfusion.com/documentation/)
- [Entity Framework Core Docs](https://learn.microsoft.com/en-us/ef/core/)
- [Blazor Server Authentication](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/)
- [JWT Authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/)
