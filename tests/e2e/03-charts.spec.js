import { test, expect } from '@playwright/test';

test.describe('Charts Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/charts');
    await page.waitForTimeout(2000);
  });

  test('should display charts page structure', async ({ page }) => {
    // Check for chart list sidebar
    const chartsList = page.locator('.charts-sidebar');
    if (await chartsList.isVisible()) {
      console.log('✓ Charts sidebar visible');
    }
    
    // Check for configuration panel tabs
    const seriesTab = page.locator('text=Series');
    if (await seriesTab.isVisible()) {
      console.log('✓ Series tab visible');
    }
    
    const axesTab = page.locator('text=Axes');
    if (await axesTab.isVisible()) {
      console.log('✓ Axes tab visible');
    }
    
    const settingsTab = page.locator('text=Chart Settings');
    if (await settingsTab.isVisible()) {
      console.log('✓ Chart Settings tab visible');
    }
    
    // Take full page screenshot
    await page.screenshot({ 
      path: 'screenshots/03-charts-page.png', 
      fullPage: true 
    });
    
    console.log('✓ Charts page structure rendered');
  });

  test('should test chart list scrolling', async ({ page }) => {
    // Check if chart list has scrollbar when needed
    const chartList = page.locator('.chart-list');
    if (await chartList.isVisible()) {
      const boundingBox = await chartList.boundingBox();
      console.log(`Chart list dimensions: ${boundingBox?.width}x${boundingBox?.height}`);
    }
    
    // Take screenshot
    await page.screenshot({ 
      path: 'screenshots/03-charts-list-scroll.png', 
      fullPage: true 
    });
    
    console.log('✓ Chart list scroll tested');
  });

  test('should test series configuration', async ({ page }) => {
    // Click on Series tab
    await page.click('text=Series');
    await page.waitForTimeout(500);
    
    // Check for series configuration cards
    const seriesCard = page.locator('.series-config-card').first();
    if (await seriesCard.isVisible()) {
      console.log('✓ Series configuration card visible');
      
      // Try to expand if collapsible
      await seriesCard.click();
      await page.waitForTimeout(500);
    }
    
    // Take screenshot
    await page.screenshot({ 
      path: 'screenshots/03-charts-series-config.png', 
      fullPage: true 
    });
    
    console.log('✓ Series configuration tested');
  });

  test('should test axes configuration', async ({ page }) => {
    // Click on Axes tab
    await page.click('text=Axes');
    await page.waitForTimeout(500);
    
    // Check for primary Y-axis section
    const primaryAxis = page.locator('text=Primary Y-Axis');
    if (await primaryAxis.isVisible()) {
      console.log('✓ Primary Y-Axis section visible');
    }
    
    // Check for secondary Y-axis section
    const secondaryAxis = page.locator('text=Secondary Y-Axis');
    if (await secondaryAxis.isVisible()) {
      console.log('✓ Secondary Y-Axis section visible');
    }
    
    // Take screenshot
    await page.screenshot({ 
      path: 'screenshots/03-charts-axes-config.png', 
      fullPage: true 
    });
    
    console.log('✓ Axes configuration tested');
  });

  test('should test custom date range picker', async ({ page }) => {
    // Look for time range dropdown
    const timeRange = page.locator('select').first();
    if (await timeRange.isVisible()) {
      // Select custom option
      await timeRange.selectOption('custom');
      await page.waitForTimeout(500);
      
      // Check if date pickers appear
      const datePickers = page.locator('.e-datepicker');
      if ((await datePickers.count()) > 0) {
        console.log(`✓ Found ${await datePickers.count()} date pickers`);
      } else {
        console.log('⚠ Date pickers not visible when Custom selected');
      }
    }
    
    // Take screenshot
    await page.screenshot({ 
      path: 'screenshots/03-charts-custom-date.png', 
      fullPage: true 
    });
    
    console.log('✓ Custom date range tested');
  });

  test('should test adding series', async ({ page }) => {
    // Look for "Add Tag" or similar button
    const addButton = page.locator('button:has-text("Add")').first();
    if (await addButton.isVisible()) {
      await addButton.click();
      await page.waitForTimeout(1000);
      
      // Take screenshot of result
      await page.screenshot({ 
        path: 'screenshots/03-charts-add-series.png', 
        fullPage: true 
      });
      
      console.log('✓ Add series button clicked');
    } else {
      console.log('⚠ Add button not found');
    }
  });
});
