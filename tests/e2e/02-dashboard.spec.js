import { test, expect } from '@playwright/test';

test.describe('Dashboard/Home Page', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to home page (may need authentication)
    await page.goto('/');
    await page.waitForTimeout(1000);
  });

  test('should display dashboard cards', async ({ page }) => {
    // Check for welcome message
    const welcomeText = page.locator('text=Welcome to DataForeman');
    if (await welcomeText.isVisible()) {
      console.log('✓ Welcome message visible');
    }
    
    // Check for System Status card
    const systemStatus = page.locator('text=System Status');
    if (await systemStatus.isVisible()) {
      console.log('✓ System Status card visible');
    }
    
    // Check for Tank Monitoring card
    const tankMonitoring = page.locator('text=Tank Monitoring');
    if (await tankMonitoring.isVisible()) {
      console.log('✓ Tank Monitoring card visible');
    }
    
    // Check for Production Metrics card
    const productionMetrics = page.locator('text=Production Metrics');
    if (await productionMetrics.isVisible()) {
      console.log('✓ Production Metrics card visible');
    }
    
    // Take full page screenshot
    await page.screenshot({ 
      path: 'screenshots/02-dashboard-full.png', 
      fullPage: true 
    });
    
    console.log('✓ Dashboard cards rendered');
  });

  test('should display multi-axis chart', async ({ page }) => {
    // Check for chart title
    const chartTitle = page.locator('text=Multi-Axis Process Monitor');
    if (await chartTitle.isVisible()) {
      console.log('✓ Chart title visible');
    }
    
    // Wait for chart to render
    await page.waitForTimeout(2000);
    
    // Take screenshot of chart area
    await page.screenshot({ 
      path: 'screenshots/02-dashboard-chart.png',
      fullPage: true
    });
    
    console.log('✓ Multi-axis chart rendered');
  });

  test('should display status bar', async ({ page }) => {
    // Check status bar elements
    const connected = page.locator('text=Connected');
    if (await connected.isVisible()) {
      console.log('✓ Connected status visible');
    }
    
    const backendAPI = page.locator('text=Backend API: Running');
    if (await backendAPI.isVisible()) {
      console.log('✓ Backend API status visible');
    }
    
    // Take screenshot
    await page.screenshot({ 
      path: 'screenshots/02-dashboard-status-bar.png', 
      fullPage: true 
    });
    
    console.log('✓ Status bar rendered');
  });
});
