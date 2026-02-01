# Playwright E2E Testing for DataForeman

This directory contains Playwright end-to-end tests for the DataForeman application.

## Setup

1. Install dependencies (if not already installed):
```bash
npm install
```

2. Install Playwright browsers:
```bash
npx playwright install chromium
```

## Running Tests

### Prerequisites
Make sure the DataForeman application is running on `http://localhost:8080` before running tests.

Start the application:
```bash
npm run start
```

### Run All Tests
```bash
# Using the helper script
./run-playwright-tests.sh

# Or directly with npx
npx playwright test
```

### Run Specific Test File
```bash
npx playwright test tests/e2e/03-charts.spec.js
```

### Run Tests in UI Mode (Interactive)
```bash
npx playwright test --ui
```

### Run Tests in Debug Mode
```bash
npx playwright test --debug
```

### View Test Report
```bash
npx playwright show-report
```

## Test Structure

### Test Files
- `01-login.spec.js` - Login page and authentication flow
- `02-dashboard.spec.js` - Dashboard/home page with cards and charts
- `03-charts.spec.js` - Charts page with configuration panels
- `04-flows.spec.js` - Flow Studio with node editor
- `05-other-pages.spec.js` - Connectivity, Diagnostics, Users, Profile pages

### Screenshots
All tests capture screenshots which are saved to `./screenshots/` directory:
- Full page screenshots for visual verification
- Screenshots captured on test failures
- Named systematically for easy identification

### Test Coverage

Each test file covers:
- **Visual Elements**: Verifies UI components are visible
- **Layout**: Checks page structure and positioning
- **Interactions**: Tests clicks, selections, and user actions
- **Functionality**: Validates feature behavior
- **Responsive Design**: Tests at 1920x1080 viewport

## Detected Issues

Common issues to look for:
- CSS overflow and scrollbar problems
- Text truncation and ellipsis
- Element positioning (especially connectors)
- Date picker visibility
- Series configuration panel layout
- Node drop positioning accuracy

## Configuration

Main configuration in `playwright.config.js`:
- Base URL: `http://localhost:8080`
- Timeout: 60 seconds per test
- Browser: Chromium (Desktop Chrome)
- Viewport: 1920x1080
- Screenshots: On failure
- Videos: Retained on failure
- Traces: On first retry

## Troubleshooting

### Application Not Running
If tests fail to connect, make sure the application is running:
```bash
# Check if app is accessible
curl http://localhost:8080

# Start application
npm run start
```

### Browser Not Installed
If you see "Executable doesn't exist" error:
```bash
npx playwright install chromium
```

### Timeout Issues
Increase timeouts in `playwright.config.js` if needed:
```javascript
timeout: 120 * 1000,  // 2 minutes
```

### Authentication Issues
Some pages may require authentication. Tests handle this by:
- Testing login flow first
- Using authenticated context for subsequent tests
- Checking for login redirects

## Best Practices

1. **Wait for Elements**: Use `waitForTimeout()` or `waitForSelector()` for dynamic content
2. **Explicit Checks**: Use conditional checks with `if (await element.isVisible())`
3. **Screenshots**: Capture screenshots at key points for debugging
4. **Console Logs**: Add descriptive console.log messages for test progress
5. **Error Handling**: Tests should not fail if optional elements are missing

## Continuous Integration

To run tests in CI:
```bash
CI=1 npx playwright test
```

This enables:
- Stricter test.only detection
- Automatic retries (2x)
- No interactive prompts

## Resources

- [Playwright Documentation](https://playwright.dev)
- [Playwright API Reference](https://playwright.dev/docs/api/class-playwright)
- [Best Practices](https://playwright.dev/docs/best-practices)
