# Playwright Testing & Issue Detection Guide

## Overview

This document explains how to use Playwright to automatically test DataForeman, capture screenshots, identify issues, and verify fixes.

## Quick Start

### 1. Prerequisites

Ensure the DataForeman application is running:
```bash
# Start the full application stack
npm run start

# Or start with rebuild
npm run start:rebuild

# Application should be accessible at http://localhost:8080
```

### 2. Run Playwright Tests

```bash
# Using the helper script (recommended)
./run-playwright-tests.sh

# Or directly
npx playwright test

# Or install browsers first if needed
npx playwright install chromium
npx playwright test
```

### 3. View Results

After tests complete:
```bash
# View HTML report
npx playwright show-report

# Screenshots are in ./screenshots/
ls -la screenshots/

# Test results in ./test-results/
```

## What Gets Tested

### Automatic Testing (23 Tests)

1. **Login Flow** (2 tests)
   - Login form display
   - Authentication process

2. **Dashboard** (3 tests)
   - Dashboard cards rendering
   - Multi-axis chart display
   - Status bar indicators

3. **Charts Page** (6 tests)
   - Chart list and sidebar
   - Series configuration panel
   - Axes configuration
   - Custom date picker visibility
   - Scrollbar functionality
   - Adding series interaction

4. **Flows Page** (6 tests)
   - Flow list display
   - Node palette availability
   - Opening sample flows
   - Node drag and drop
   - Connector positioning
   - Save functionality

5. **Other Pages** (6 tests)
   - Connectivity page structure
   - Diagnostics display
   - Users management
   - Profile settings
   - Navigation menus
   - User dropdown

### Screenshot Capture

Every test captures screenshots at key points:
- Full page screenshots for visual verification
- Screenshots on test failures
- Named systematically (e.g., `03-charts-series-config.png`)

## Issue Detection

### Common Issues Found

Playwright tests will detect:

1. **CSS Overflow Problems**
   - Elements extending beyond containers
   - Missing scrollbars
   - Text not wrapping properly

2. **Positioning Issues**
   - Elements at wrong coordinates
   - Connectors at (0,0)
   - Nodes not dropping at cursor position

3. **Visibility Problems**
   - Hidden elements that should be visible
   - Collapsed sections
   - Missing UI components

4. **Interactive Issues**
   - Buttons not clickable
   - Dropdowns not opening
   - Forms not submitting

5. **Layout Problems**
   - Misaligned elements
   - Overlapping content
   - Responsive issues

### How Tests Report Issues

Tests use conditional checks with informative logging:

```javascript
if (await element.isVisible()) {
  console.log('✓ Element visible');
} else {
  console.log('⚠ Element not found');
}
```

This means:
- ✓ = Element working correctly
- ⚠ = Potential issue detected
- ❌ = Critical failure (test stops)

## Fixing Issues

### Workflow

1. **Run Tests**:
   ```bash
   ./run-playwright-tests.sh
   ```

2. **Review Output**:
   - Check console for ✓ and ⚠ messages
   - Look for test failures

3. **Examine Screenshots**:
   ```bash
   ls -la screenshots/
   open screenshots/03-charts-page.png  # or your image viewer
   ```

4. **Identify Issues**:
   - Visual problems in screenshots
   - Missing elements from console warnings
   - Failed assertions in test output

5. **Fix Code**:
   - Update Razor components
   - Fix CSS styles
   - Adjust JavaScript logic

6. **Re-run Tests**:
   ```bash
   ./run-playwright-tests.sh
   ```

7. **Verify Fix**:
   - Compare new screenshots
   - Check console output improved
   - All tests passing

### Example: Fixing Scrollbar Issue

**Issue Detected**:
```
⚠ Chart list scrollbar not visible
```

**Screenshot Shows**:
- Content overflowing
- No scrollbar present

**Fix in Charts.razor**:
```css
.charts-sidebar {
    max-height: calc(100vh - 200px);
    overflow-y: auto;  /* Add this */
}
```

**Re-run & Verify**:
```bash
npx playwright test tests/e2e/03-charts.spec.js
# Check new screenshot shows scrollbar
```

## Advanced Usage

### Run Specific Tests

```bash
# Single test file
npx playwright test tests/e2e/03-charts.spec.js

# Single test case
npx playwright test -g "should display charts page structure"

# Multiple files matching pattern
npx playwright test tests/e2e/03-charts.spec.js tests/e2e/04-flows.spec.js
```

### Interactive Testing

```bash
# UI Mode - Visual test runner
npx playwright test --ui

# Debug Mode - Step through tests
npx playwright test --debug

# Headed Mode - See browser
npx playwright test --headed
```

### Test Filtering

```bash
# Run only failed tests
npx playwright test --last-failed

# Update screenshots (if test uses them)
npx playwright test --update-snapshots

# Specific project
npx playwright test --project=chromium
```

### Continuous Testing

```bash
# Watch mode - re-run on file changes
npx playwright test --watch
```

## Understanding Results

### HTML Report

Open the HTML report:
```bash
npx playwright show-report
```

Shows:
- Test status (passed/failed/skipped)
- Execution time
- Screenshots and videos
- Detailed error messages
- Stack traces

### Console Output

```
Running 23 tests using 1 worker

✓ Login Page › should display login form (2s)
✓ Dashboard/Home Page › should display dashboard cards (3s)
⚠ Charts Page › should test custom date range picker (5s)
  - Warning: Date pickers not visible when Custom selected

23 passed (1m 45s)
```

### Screenshots Directory

```bash
ls -la screenshots/
# Organized by page and feature
# Easy to review visually
# Compare before/after fixes
```

## Troubleshooting

### Application Not Running

```
Error: connect ECONNREFUSED 127.0.0.1:8080
```

**Solution**:
```bash
# Start application first
npm run start
# Wait for "Listening on http://localhost:8080"
# Then run tests
```

### Browser Not Installed

```
Executable doesn't exist at /path/to/chromium
```

**Solution**:
```bash
npx playwright install chromium
```

### Timeout Errors

```
TimeoutError: waiting for selector to be visible
```

**Solutions**:
- Increase timeout in `playwright.config.js`
- Add explicit waits: `await page.waitForTimeout(2000)`
- Check if element actually exists in page

### Authentication Issues

Some pages require login. Tests handle this by:
- Testing login flow first
- Storing authentication state
- Reusing for subsequent tests

If tests fail on authenticated pages:
```bash
# Run login test first
npx playwright test tests/e2e/01-login.spec.js
# Then run other tests
```

## Best Practices

### Writing New Tests

1. **Use Descriptive Names**:
   ```javascript
   test('should display series configuration panel', async ({ page }) => {
   ```

2. **Add Informative Logging**:
   ```javascript
   console.log('✓ Series configuration visible');
   ```

3. **Capture Screenshots**:
   ```javascript
   await page.screenshot({ path: 'screenshots/feature.png', fullPage: true });
   ```

4. **Use Conditional Checks**:
   ```javascript
   if (await element.isVisible()) {
     console.log('✓ Element found');
   } else {
     console.log('⚠ Element not found');
   }
   ```

5. **Wait for Dynamic Content**:
   ```javascript
   await page.waitForTimeout(1000);  // For async rendering
   await page.waitForSelector('.chart');  // For specific elements
   ```

### Running in CI

For continuous integration:
```bash
# .github/workflows/playwright.yml
- name: Run Playwright tests
  run: |
    npm install
    npx playwright install --with-deps chromium
    npm run start &
    npx playwright test
```

## Resources

- [Playwright Documentation](https://playwright.dev/docs/intro)
- [Test API Reference](https://playwright.dev/docs/api/class-test)
- [Assertions](https://playwright.dev/docs/test-assertions)
- [Best Practices](https://playwright.dev/docs/best-practices)
- [Debugging Guide](https://playwright.dev/docs/debug)

## Summary

Playwright provides:
- ✅ Automated testing of all pages
- ✅ Visual verification via screenshots
- ✅ Issue detection and reporting
- ✅ Reproducible test cases
- ✅ Easy verification of fixes
- ✅ Confidence in deployments

Run tests regularly to catch issues early and ensure the application works correctly across all pages.
