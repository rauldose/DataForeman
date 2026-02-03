# CLAUDE.md - AI Assistant Guide for DataForeman

## Project Overview

**DataForeman** is an industrial telemetry collection and visualization platform. It connects to industrial devices (OPC UA, EtherNet/IP, S7), collects time-series data, and provides a modern web UI for visualization and data processing.

- **Status**: Beta (v0.4.3) - Active development, APIs may change
- **Repository**: https://github.com/orionK-max/DataForeman
- **Website**: https://www.DataForeman.app

### Core Capabilities
- Industrial device connectivity (EtherNet/IP, Siemens S7, OPC UA)
- Time-series data collection and storage (TimescaleDB)
- Visual Flow Editor for real-time data processing
- Dashboards and chart visualization
- Multi-user permission system

---

## Repository Structure

```
DataForeman/
├── core/                    # Node.js/Fastify backend API (port 3000)
│   ├── src/
│   │   ├── server.js       # Main entry point
│   │   ├── routes/         # API route handlers (15+ feature areas)
│   │   ├── nodes/          # Flow node implementations (11 categories)
│   │   ├── services/       # Business logic services
│   │   └── constants/      # Feature flags, permissions
│   └── migrations/         # PostgreSQL migrations
├── front/                   # React + Material UI frontend (port 5174 dev, 8080 prod)
│   ├── src/
│   │   ├── main.jsx        # Entry point
│   │   ├── pages/          # Page containers
│   │   ├── components/     # UI components by feature
│   │   ├── services/       # API clients
│   │   ├── hooks/          # Custom React hooks
│   │   └── contexts/       # React Context providers
│   └── public/             # Static assets
├── connectivity/            # Industrial protocol drivers (port 3100)
│   └── src/
│       └── drivers/        # EIP, S7, OPC UA implementations
├── ops/                     # Operational scripts and utilities
│   ├── start.sh            # Main startup script
│   ├── rotator-daemon.js   # Log rotation
│   └── validate-permissions.js
├── tests/                   # E2E tests (Playwright)
│   └── e2e/                # 5 spec files, 23 tests
├── spec/                    # JSON schemas for connectivity
├── docs/                    # Comprehensive documentation (30+ files)
├── blazor-front/            # Legacy Blazor frontend (alternative)
├── dotnet-backend/          # Legacy .NET backend (alternative)
└── windows-installer/       # Windows deployment (Inno Setup)
```

---

## Technology Stack

### Runtime
- **Node.js 22** (ESM-only modules - `"type": "module"`)
- **No TypeScript** - Pure JavaScript/JSX throughout

### Backend (core/)
- **Fastify** - REST API framework
- **PostgreSQL 16** - Main database
- **TimescaleDB** - Time-series storage
- **NATS 2** - Message broker with JetStream
- **jose** - JWT authentication
- **argon2** - Password hashing
- **Pino** - Structured JSON logging
- **node-pg-migrate** - Database migrations

### Frontend (front/)
- **React 18**
- **Material UI v5**
- **Vite** - Build tool
- **React Router v6**
- **Apache ECharts** - High-performance charts
- **ReactFlow** - Visual node editor
- **Emotion** - CSS-in-JS

### Industrial Protocols (connectivity/)
- **EtherNet/IP** - PyComm3 (Python 3)
- **Siemens S7** - node-snap7
- **OPC UA** - node-opcua

---

## Development Workflow

### Quick Start
```bash
# Clone and start (production-like)
npm start

# Development with hot-reload frontend
npm run dev

# Access
# Production: http://localhost:8080
# Development: http://localhost:5174
# Default login: admin@example.com / password
```

### Common Commands

| Command | Description |
|---------|-------------|
| `npm start` | Start all services in Docker |
| `npm run start:rebuild` | Rebuild images and start |
| `npm run dev` | Docker services + local frontend dev server |
| `npm run dev:front` | Frontend dev server only |
| `npm run dev:core` | Core API locally |
| `npm run lint` | Run ESLint |
| `npm run format` | Run Prettier |

### Docker Services
- **db**: PostgreSQL (port 5432)
- **tsdb**: TimescaleDB (port 5433)
- **nats**: Message broker (port 4222)
- **core**: API server (port 3000)
- **connectivity**: Protocol drivers (port 3100)
- **front**: Nginx + React (port 8080)
- **rotator**: Log rotation daemon

### Viewing Logs
```bash
# Docker logs
docker compose logs -f core
docker compose logs -f connectivity

# File logs
tail -f logs/core/core.current
tail -f logs/connectivity/connectivity.current
```

---

## Code Conventions

### JavaScript/ESM
- All files use ESM modules (`import`/`export`)
- No TypeScript - use JSDoc for type hints if needed
- camelCase for functions/variables
- PascalCase for React components and classes

### Formatting (Prettier)
```json
{
  "semi": true,
  "singleQuote": true,
  "trailingComma": "es5",
  "printWidth": 100
}
```

### ESLint Rules
- `eslint:recommended` + `plugin:react/recommended`
- Unused vars allowed if prefixed with `_`
- `no-console: off` - Console logging allowed
- `react/prop-types: off` - No PropTypes required

### File Organization
- **Backend routes**: `core/src/routes/<feature>.js`
- **Frontend pages**: `front/src/pages/<PageName>.jsx`
- **Frontend components**: `front/src/components/<feature>/<Component>.jsx`
- **Services**: `*/src/services/<service>.js`

---

## Key Patterns

### Backend Route Protection
```javascript
// All protected routes use this pattern
app.post('/api/resource', {
  preHandler: [
    app.authenticate,
    app.permissions.requirePermission('feature', 'create')
  ]
}, handler);
```

### Frontend Permission Checks
```javascript
import { usePermissions } from '../contexts/PermissionContext';

function Component() {
  const { can } = usePermissions();

  return (
    <>
      {can('feature', 'create') && <CreateButton />}
    </>
  );
}
```

### Flow Node Structure
Flow nodes are in `core/src/nodes/<category>/`:
- `base/` - Core nodes (Input, Output)
- `math/` - Mathematical operations
- `logic/` - Boolean logic
- `comparison/` - Comparisons
- `data/` - Data manipulation
- `scripts/` - JavaScript execution
- `tags/` - Tag read/write
- `triggers/` - Conditions
- `utility/` - Helpers

### Data Flow
1. Connectivity reads devices → publishes to NATS (`df.telemetry.raw.*`)
2. Core subscribes → writes to TimescaleDB + updates in-memory cache
3. Flows read from cache (~5ms) for real-time processing
4. Results can write back to tags or devices

### Quality Codes
- `0` = Good quality
- `1+` = Bad/uncertain
- OPC UA passes native 32-bit statusCode

---

## Testing

### E2E Tests (Playwright)
```bash
# Install
npm install
npx playwright install chromium

# Run tests (requires services running)
npx playwright test

# Interactive UI mode
npx playwright test --ui

# Run specific test file
npx playwright test tests/e2e/01-login.spec.js
```

### Test Files
- `tests/e2e/01-login.spec.js` - Authentication
- `tests/e2e/02-dashboard.spec.js` - Dashboard
- `tests/e2e/03-charts.spec.js` - Chart Composer
- `tests/e2e/04-flows.spec.js` - Flow Studio
- `tests/e2e/05-other-pages.spec.js` - Navigation, Diagnostics

### Backend Tests (Vitest)
```bash
cd core
npm run test:watch
npm run test:coverage
```

---

## Database

### Migrations
Located in `core/migrations/` and `core/migrations-tsdb/`.

**Strategy**:
- Beta: One migration per release, can modify during development
- Stable (v1.0+): All migrations locked, changes require new files
- Naming: `XXX_v0.Y_release.sql`

Migrations run automatically on startup.

### Schema Overview
- **users** - User accounts and permissions
- **dashboards** - Dashboard configurations
- **charts** - Chart definitions
- **flows** - Flow definitions
- **devices** - Connectivity device configs
- **tags** - Tag definitions
- **poll_groups** - Polling configurations
- **measurements** (TimescaleDB) - Time-series data

---

## Environment Variables

Key variables in `.env.example`:

```bash
# Authentication
JWT_SECRET=your-secret-key
ADMIN_EMAIL=admin@example.com
ADMIN_PASSWORD=your-password
AUTH_DEV_TOKEN=1              # Bypass auth in dev

# Databases
PGHOST=db
PGPASSWORD=your-password
TSDB_HOST=tsdb
TSDB_PASSWORD=your-password

# Logging
LOG_LEVEL=info                # info/debug/warn/error
LOG_ROTATE_PERIOD_MINUTES=1440
LOG_RETENTION_DAYS=14

# Connectivity
CONNECTIVITY_NETWORK_MODE=host  # Linux only, for EIP autodiscovery
PYCOMM3_ARRAY_MODE=batch        # batch or individual
```

---

## Important Files

| Purpose | Location |
|---------|----------|
| Main API entry | `core/src/server.js` |
| Frontend entry | `front/src/main.jsx` |
| API routes | `core/src/routes/*.js` |
| Flow nodes | `core/src/nodes/` |
| DB migrations | `core/migrations/` |
| Docker config | `docker-compose.yml` |
| Environment | `.env.example` |
| Playwright config | `playwright.config.js` |
| ESLint config | `.eslintrc.json` |
| Prettier config | `.prettierrc.json` |

---

## CI/CD

### GitHub Actions (`.github/workflows/`)
- **release.yml** - Main release pipeline
  - Creates GitHub releases
  - Builds Windows installer (Inno Setup)
  - Creates source archives
- **validate-schemas.yml** - Validates connectivity schemas

### Release Process
Triggered by pushing a version tag:
```bash
git tag v0.4.4
git push origin v0.4.4
```

---

## Common Tasks

### Adding a New API Endpoint
1. Create/update route file in `core/src/routes/<feature>.js`
2. Add permission protection:
   ```javascript
   app.get('/api/new-endpoint', {
     preHandler: [app.authenticate, app.permissions.requirePermission('feature', 'read')]
   }, handler);
   ```
3. Register route in `core/src/server.js` if new file
4. Run `node ops/validate-permissions.js` to verify

### Adding a New Frontend Page
1. Create page in `front/src/pages/<PageName>.jsx`
2. Add route in `front/src/App.jsx`
3. Add permission check if needed:
   ```javascript
   const { can } = usePermissions();
   if (!can('feature', 'read')) return <Navigate to="/" />;
   ```

### Adding a New Flow Node
1. Create node in `core/src/nodes/<category>/<NodeName>.js`
2. Export from `core/src/nodes/<category>/index.js`
3. Register in `core/src/nodes/registry.js`

### Running Permission Validation
```bash
node ops/validate-permissions.js --verbose
```

---

## Security Considerations

- **Never commit `.env`** - Contains secrets
- **JWT tokens** expire in 14 days with auto-refresh
- **Passwords** hashed with argon2
- **Permission checks** required on all protected routes
- **CORS** configured for frontend origin
- **Helmet** adds security headers
- **SQL injection** prevented via parameterized queries

---

## Troubleshooting

### Containers Won't Start
```bash
# Fix permissions and restart
npm start
# Or manually:
./fix-permissions.sh && docker compose up -d
```

### Permission Denied Errors
```bash
./fix-permissions.sh
docker compose restart
```

### Database Issues
```bash
# Check logs
docker compose logs db
docker compose logs tsdb

# Reset everything (DANGER: deletes data)
docker compose down -v
npm start
```

### Frontend Not Loading
```bash
# Check if services are running
docker compose ps

# Check nginx logs
docker compose logs front
```

---

## Useful Documentation

- [Quick Start Guide](QUICK-START.md)
- [Troubleshooting Guide](TROUBLESHOOTING.md)
- [Flow Studio User Guide](docs/flows-user-guide.md)
- [API Registry](docs/api-registry.md)
- [Database Migrations](docs/database-migrations.md)
- [Permission System Developer Guide](docs/permission-system-developer-guide.md)
- [Windows Installation](docs/windows-installation.md)
