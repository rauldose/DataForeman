import { test, expect } from '@playwright/test';

test.describe('State Machine Builder and Trend Visualizer', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to home page first
    await page.goto('/');
    await page.waitForTimeout(2000);
  });

  test('State Machines - List View', async ({ page }) => {
    console.log('📊 Testing State Machines list view...');
    
    // Navigate to state machines page
    await page.goto('/state-machines');
    await page.waitForTimeout(3000);
    
    // Wait for page content to load
    await page.waitForSelector('text=State Machines', { timeout: 10000 });
    
    // Take screenshot of list view
    await page.screenshot({ 
      path: 'screenshots/10-state-machines-list.png', 
      fullPage: true 
    });
    
    console.log('✓ State Machines list view screenshot captured');
  });

  test('State Machine Builder - Visual Editor', async ({ page }) => {
    console.log('🎨 Testing State Machine visual editor...');
    
    // Navigate to state machines page
    await page.goto('/state-machines');
    await page.waitForTimeout(2000);
    
    // Click "New State Machine" button
    const newButton = page.locator('button:has-text("New State Machine")');
    if (await newButton.isVisible()) {
      await newButton.click();
      await page.waitForTimeout(3000);
      
      // Wait for diagram to load
      await page.waitForSelector('.state-palette, .node-palette', { timeout: 10000 });
      
      // Take screenshot of editor
      await page.screenshot({ 
        path: 'screenshots/11-state-machine-editor.png', 
        fullPage: true 
      });
      
      console.log('✓ State Machine visual editor screenshot captured');
      
      // Try to interact with palette (optional)
      const paletteItems = page.locator('.palette-item');
      const itemCount = await paletteItems.count();
      if (itemCount > 0) {
        console.log(`✓ Found ${itemCount} palette items`);
        
        // Take a second screenshot after a moment
        await page.waitForTimeout(1000);
        await page.screenshot({ 
          path: 'screenshots/11b-state-machine-editor-detail.png', 
          fullPage: true 
        });
      }
    } else {
      console.log('⚠️  New State Machine button not found');
    }
  });

  test('Trends - List View', async ({ page }) => {
    console.log('📈 Testing Trends list view...');
    
    // Navigate to trends page
    await page.goto('/trends');
    await page.waitForTimeout(3000);
    
    // Wait for page content to load
    await page.waitForSelector('text=Trend', { timeout: 10000 });
    
    // Take screenshot of list view
    await page.screenshot({ 
      path: 'screenshots/12-trends-list.png', 
      fullPage: true 
    });
    
    console.log('✓ Trends list view screenshot captured');
  });

  test('Trend Visualizer - Timeline View', async ({ page }) => {
    console.log('📉 Testing Trend timeline visualizer...');
    
    // Navigate to trends page
    await page.goto('/trends');
    await page.waitForTimeout(2000);
    
    // Click "New Trend" button
    const newButton = page.locator('button:has-text("New Trend")');
    if (await newButton.isVisible()) {
      await newButton.click();
      await page.waitForTimeout(3000);
      
      // Wait for trend editor to load
      await page.waitForSelector('.timeline-container, .chart-container, .viewer-content', { timeout: 10000 });
      
      // Try to add a series
      const addSeriesButton = page.locator('button:has-text("Add Series")');
      if (await addSeriesButton.isVisible()) {
        // Add first series
        await addSeriesButton.click();
        await page.waitForTimeout(500);
        
        // Add second series
        await addSeriesButton.click();
        await page.waitForTimeout(500);
        
        // Add third series
        await addSeriesButton.click();
        await page.waitForTimeout(1000);
        
        console.log('✓ Added 3 series');
        
        // Try to click Refresh button to generate data
        const refreshButton = page.locator('button:has-text("Refresh")');
        if (await refreshButton.isVisible()) {
          await refreshButton.click();
          await page.waitForTimeout(2000);
          console.log('✓ Refreshed data');
        }
      }
      
      // Take screenshot of timeline
      await page.screenshot({ 
        path: 'screenshots/13-trend-timeline.png', 
        fullPage: true 
      });
      
      console.log('✓ Trend timeline screenshot captured');
      
      // Take a zoomed screenshot focusing on the timeline area
      const timelineElement = page.locator('.timeline-container, .chart-container').first();
      if (await timelineElement.isVisible()) {
        await timelineElement.screenshot({
          path: 'screenshots/13b-trend-timeline-detail.png'
        });
        console.log('✓ Trend timeline detail screenshot captured');
      }
    } else {
      console.log('⚠️  New Trend button not found');
    }
  });

  test('Navigation - State Machines and Trends Menu Items', async ({ page }) => {
    console.log('🧭 Testing navigation to new features...');
    
    await page.goto('/');
    await page.waitForTimeout(2000);
    
    // Check if State Machines menu item exists
    const stateMachinesLink = page.locator('text=State Machines').first();
    if (await stateMachinesLink.isVisible()) {
      console.log('✓ State Machines menu item found');
      
      // Hover over it
      await stateMachinesLink.hover();
      await page.waitForTimeout(500);
    }
    
    // Check if Trends menu item exists
    const trendsLink = page.locator('text=Trends').first();
    if (await trendsLink.isVisible()) {
      console.log('✓ Trends menu item found');
      
      // Hover over it
      await trendsLink.hover();
      await page.waitForTimeout(500);
    }
    
    // Take screenshot showing navigation
    await page.screenshot({ 
      path: 'screenshots/14-navigation-menu.png', 
      fullPage: true 
    });
    
    console.log('✓ Navigation menu screenshot captured');
  });

  test('State Machine - Create and Connect States', async ({ page }) => {
    console.log('🔗 Testing state creation and connection...');
    
    await page.goto('/state-machines');
    await page.waitForTimeout(2000);
    
    // Click New State Machine
    const newButton = page.locator('button:has-text("New State Machine")');
    if (await newButton.isVisible()) {
      await newButton.click();
      await page.waitForTimeout(3000);
      
      // Check if diagram canvas exists
      const diagramCanvas = page.locator('#state-diagram-space, .diagram-container');
      if (await diagramCanvas.isVisible()) {
        console.log('✓ Diagram canvas found');
        
        // Try to find palette items
        const normalState = page.locator('.palette-item').first();
        if (await normalState.isVisible()) {
          console.log('✓ Palette items found');
          
          // Get canvas position
          const canvasBox = await diagramCanvas.boundingBox();
          if (canvasBox) {
            // Simulate drag from palette to canvas (approximate)
            // Note: This is a simplified drag simulation
            await normalState.hover();
            await page.mouse.down();
            await page.mouse.move(canvasBox.x + 200, canvasBox.y + 150);
            await page.mouse.up();
            await page.waitForTimeout(1000);
            
            console.log('✓ Attempted to drag state to canvas');
          }
        }
        
        // Take screenshot after interaction
        await page.screenshot({ 
          path: 'screenshots/15-state-machine-with-nodes.png', 
          fullPage: true 
        });
        
        console.log('✓ State machine with nodes screenshot captured');
      }
    }
  });
});
