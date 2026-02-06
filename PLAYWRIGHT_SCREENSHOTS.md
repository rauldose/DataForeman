# Playwright Screenshot Tests for State Machine & Trend Visualizer

## Overview

This document explains how to use Playwright to capture screenshots of the newly implemented visual features:
- **State Machine Builder**: Visual node editor with drag-and-drop
- **Trend Visualizer**: Horizontal timeline with trace visualization

## Prerequisites

1. **Node.js and npm** installed (v20+)
2. **DataForeman.App** Blazor application running
3. **Playwright** installed (`npm install`)

## Quick Start

### 1. Start the Blazor Application

```bash
cd src/DataForeman.App
dotnet run
```

The app should be accessible at `http://localhost:5000`

### 2. Run Screenshot Tests

In a separate terminal, from the repository root:

```bash
./capture-screenshots.sh
```

This script will:
- Check if the application is running
- Install Playwright browsers if needed
- Run the screenshot tests
- Save images to `./screenshots/` directory

## Manual Test Execution

### Run All E2E Tests

```bash
BASE_URL=http://localhost:5000 npx playwright test
```

### Run Only Screenshot Tests

```bash
BASE_URL=http://localhost:5000 npx playwright test tests/e2e/06-state-machine-and-trends.spec.js
```

### Run with UI Mode (Interactive)

```bash
BASE_URL=http://localhost:5000 npx playwright test --ui
```

### Run with Debug Mode

```bash
BASE_URL=http://localhost:5000 npx playwright test --debug
```

## Test Structure

### Test File
`tests/e2e/06-state-machine-and-trends.spec.js`

### Tests Included

1. **State Machines - List View**
   - Navigates to `/state-machines`
   - Captures grid view
   - Output: `screenshots/10-state-machines-list.png`

2. **State Machine Builder - Visual Editor**
   - Creates new state machine
   - Shows visual diagram editor
   - Output: `screenshots/11-state-machine-editor.png`

3. **Trends - List View**
   - Navigates to `/trends`
   - Captures grid view
   - Output: `screenshots/12-trends-list.png`

4. **Trend Visualizer - Timeline View**
   - Creates new trend
   - Adds series
   - Refreshes data
   - Output: `screenshots/13-trend-timeline.png`

5. **Navigation Menu**
   - Verifies menu items exist
   - Output: `screenshots/14-navigation-menu.png`

6. **State Machine Interactions**
   - Tests drag-and-drop (if possible)
   - Output: `screenshots/15-state-machine-with-nodes.png`

## Screenshot Outputs

All screenshots are saved to the `screenshots/` directory:

```
screenshots/
├── 10-state-machines-list.png      # State machines grid view
├── 11-state-machine-editor.png     # Visual node editor
├── 11b-state-machine-editor-detail.png  # Editor detail view
├── 12-trends-list.png              # Trends grid view
├── 13-trend-timeline.png           # Horizontal timeline
├── 13b-trend-timeline-detail.png   # Timeline detail view
├── 14-navigation-menu.png          # Navigation with new items
└── 15-state-machine-with-nodes.png # State machine with nodes
```

## Configuration

### Playwright Config
File: `playwright.config.js`

Key settings:
- **baseURL**: `http://localhost:5000` (Blazor app) or use `BASE_URL` env var
- **timeout**: 60 seconds per test
- **viewport**: 1920x1080 (Desktop Chrome)
- **screenshots**: Saved on test completion

### Customizing Base URL

Set environment variable before running tests:

```bash
# For Blazor app (port 5000)
export BASE_URL=http://localhost:5000
npx playwright test

# For frontend app (port 8080)
export BASE_URL=http://localhost:8080
npx playwright test
```

## Troubleshooting

### Application Not Running

**Error**: Connection refused or timeout

**Solution**: Ensure the application is running:
```bash
cd src/DataForeman.App
dotnet run
```

### Playwright Not Installed

**Error**: `playwright: command not found`

**Solution**: Install dependencies:
```bash
npm install
npx playwright install chromium
```

### Screenshots Not Generated

**Error**: Screenshots directory empty

**Solution**: 
1. Check test output for errors
2. Verify application is accessible at configured URL
3. Try running with `--debug` flag to see what's happening

### Timeout Issues

**Error**: Test timeout after 60 seconds

**Solution**: 
1. Increase timeout in test file
2. Check if Blazor app is slow to load
3. Verify network connectivity

### Element Not Found

**Error**: Selector not found

**Solution**: 
1. The page structure might have changed
2. Update selectors in test file
3. Use `--debug` mode to inspect page

## Advanced Usage

### Generate HTML Report

After running tests:
```bash
npx playwright show-report
```

### Record New Tests

```bash
BASE_URL=http://localhost:5000 npx playwright codegen
```

### Run Tests in Headed Mode

```bash
BASE_URL=http://localhost:5000 npx playwright test --headed
```

### Run Specific Test

```bash
BASE_URL=http://localhost:5000 npx playwright test -g "State Machine Builder"
```

## Integration with CI/CD

### GitHub Actions Example

```yaml
name: Playwright Tests
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-node@v3
        with:
          node-version: 20
      - name: Install dependencies
        run: npm install
      - name: Install Playwright
        run: npx playwright install --with-deps chromium
      - name: Start Blazor App
        run: |
          cd src/DataForeman.App
          dotnet run &
          sleep 10
      - name: Run tests
        run: BASE_URL=http://localhost:5000 npx playwright test
      - name: Upload screenshots
        uses: actions/upload-artifact@v3
        if: always()
        with:
          name: screenshots
          path: screenshots/
```

## Next Steps

1. **Review Screenshots**: Check generated images match expected UI
2. **Update Tests**: Add more interactions if needed
3. **Automate**: Integrate into CI/CD pipeline
4. **Document**: Update project README with screenshot examples

## References

- [Playwright Documentation](https://playwright.dev)
- [Playwright Test API](https://playwright.dev/docs/api/class-test)
- [Screenshot API](https://playwright.dev/docs/screenshots)
- [DataForeman Documentation](../README.md)
