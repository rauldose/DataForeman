# DataForeman Blazor Frontend

Blazor Server frontend for DataForeman industrial data management platform, built with Syncfusion Blazor UI components.

## Architecture

```
blazor-front/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor       # Main app layout with sidebar
│   │   └── EmptyLayout.razor      # Empty layout for login page
│   ├── Pages/
│   │   ├── Home.razor             # Dashboard
│   │   ├── Flows.razor            # Flow Studio
│   │   ├── Charts.razor           # Chart Composer
│   │   ├── Connectivity.razor     # Connections & Tags
│   │   ├── Diagnostics.razor      # System diagnostics
│   │   ├── Login.razor            # Authentication
│   │   ├── Profile.razor          # User profile
│   │   └── Users.razor            # User management
│   ├── App.razor                  # Root component
│   └── Routes.razor               # Routing configuration
├── Services/
│   ├── DataService.cs             # Direct database access service
│   ├── AuthStateProvider.cs       # JWT auth state management
│   ├── AppStateService.cs         # Global app state
│   ├── NodePluginModels.cs        # Node plugin definitions
│   └── NodePluginRegistry.cs      # Node plugin registry
├── wwwroot/                       # Static files
├── Program.cs                     # App configuration
├── Dockerfile                     # Container image
└── appsettings.json              # Configuration
```

## Features

- **Dashboard**: System overview with drag-and-drop panels
- **Flow Studio**: Visual flow editor with Syncfusion Diagram
- **Chart Composer**: Real-time chart creation with line/bar/area types
- **Connectivity**: Connection management, tag browser, poll groups
- **Diagnostics**: System metrics, logs, and diagnostics
- **Authentication**: JWT-based auth with refresh tokens
- **RBAC**: Role-based access control for UI elements

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (matching project target framework)
- [Syncfusion Blazor License](https://www.syncfusion.com/account/downloads)

## Getting Started

### Running Locally

```bash
cd blazor-front

# Restore dependencies
dotnet restore

# Run the application
dotnet run
```

The app will be available at:
- HTTP: http://localhost:5050
- **Default credentials**: `admin@local` / `admin123`

### Configuration

This is a modular monolith application - all backend services are included in the Blazor app. No separate API server is required.

Configure optional settings in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=dataforeman.db"
  },
  "Jwt": {
    "SecretKey": "your-secret-key-min-32-chars-long!",
    "Issuer": "DataForeman",
    "Audience": "DataForeman.Clients",
    "AccessTokenExpirationHours": 2,
    "RefreshTokenExpirationDays": 7
  },
  "SyncfusionLicenseKey": "your-license-key"
}
```

Environment variables can override settings:
```bash
export ConnectionStrings__DefaultConnection="Data Source=dataforeman.db"
export SyncfusionLicenseKey=your-key
```

### Running with Docker

```bash
# Build the image
docker build -t dataforeman-ui .

# Run the container
docker run -p 8081:8081 \
  -e ApiBaseUrl=http://api:8080 \
  dataforeman-ui
```

## Authentication

The app uses JWT tokens stored in browser localStorage:
- `df_token` - Access token (1 hour expiry)
- `df_refresh_token` - Refresh token (7 days expiry)
- `df_user` - Cached user info

Tokens are automatically refreshed when expired.

## State Management

Global state is managed via `AppStateService`:

```csharp
@inject AppStateService AppState

// Access current user
var user = AppState.CurrentUser;

// Check permissions
if (AppState.HasPermission("flows", "create"))
{
    // Show create button
}

// Subscribe to state changes
AppState.OnChange += StateHasChanged;
```

## Syncfusion Components Used

- **SfGrid** - Data grids with sorting, filtering, paging
- **SfChart** - Line, bar, area charts
- **SfDiagram** - Flow editor with drag-and-drop
- **SfDashboardLayout** - Dashboard panels
- **SfTab** - Tabbed interfaces
- **SfDialog** - Modal dialogs
- **SfButton/SfTextBox/etc** - Form controls
- **SfDropDownList** - Dropdowns
- **SfSpinner** - Loading indicators

## API Integration

All API calls go through `ApiService`:

```csharp
@inject ApiService ApiService

// Get flows
var flows = await ApiService.GetFlowsAsync();

// Create chart
var result = await ApiService.CreateChartAsync(new CreateChartRequest(...));

// Auth operations
var loginResult = await ApiService.LoginAsync(email, password);
```

## Testing

Tests use Playwright for E2E testing:

```bash
cd blazor-front.tests/DataForeman.BlazorUI.Tests

# Install Playwright browsers
pwsh bin/Debug/playwright.ps1 install

# Run tests (requires app to be running)
dotnet test
```

## Development

### Adding a New Page

1. Create `Components/Pages/MyPage.razor`
2. Add `@page "/mypage"` directive
3. Add `@rendermode InteractiveServer` for interactivity
4. Add menu item in `MainLayout.razor` if needed
5. Add `@attribute [Authorize]` if authentication required

### API Endpoints

Add methods to `ApiService.cs`:

```csharp
public async Task<MyResponse?> GetMyDataAsync()
{
    try
    {
        return await _httpClient.GetFromJsonAsync<MyResponse>("api/myendpoint");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to get data");
        return null;
    }
}
```

### Permission Checks

Use `AppStateService` for permission checks:

```razor
@if (AppState.HasPermission("feature", "read"))
{
    <div>Protected content</div>
}
```

## License

See repository LICENSE file.
