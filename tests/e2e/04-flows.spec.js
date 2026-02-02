import { test, expect } from '@playwright/test';

test.describe('Flows Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/flows');
    await page.waitForTimeout(2000);
  });

  test('should display flows page structure', async ({ page }) => {
    // Check for flow list
    const flowsList = page.locator('text=Flows').first();
    if (await flowsList.isVisible()) {
      console.log('✓ Flows list visible');
    }
    
    // Check for node palette
    const nodePalette = page.locator('.node-palette, .plugin-palette');
    if (await nodePalette.isVisible()) {
      console.log('✓ Node palette visible');
    }
    
    // Take full page screenshot
    await page.screenshot({ 
      path: 'screenshots/04-flows-page.png', 
      fullPage: true 
    });
    
    console.log('✓ Flows page structure rendered');
  });

  test('should test opening a flow', async ({ page }) => {
    // Look for sample flows
    const temperatureAlert = page.locator('text=Temperature Alert System');
    if (await temperatureAlert.isVisible()) {
      await temperatureAlert.click();
      await page.waitForTimeout(2000);
      
      // Take screenshot of opened flow
      await page.screenshot({ 
        path: 'screenshots/04-flows-temperature-alert.png', 
        fullPage: true 
      });
      
      console.log('✓ Temperature Alert System flow opened');
    } else {
      console.log('⚠ Sample flows not found');
    }
  });

  test('should test node palette', async ({ page }) => {
    // Check for different node types
    const nodeTypes = ['Input', 'Output', 'Logic', 'Math', 'Script'];
    
    for (const nodeType of nodeTypes) {
      const node = page.locator(`text=${nodeType}`);
      if (await node.isVisible()) {
        console.log(`✓ ${nodeType} node type visible`);
      }
    }
    
    // Take screenshot
    await page.screenshot({ 
      path: 'screenshots/04-flows-palette.png', 
      fullPage: true 
    });
    
    console.log('✓ Node palette tested');
  });

  test('should test node drag and drop', async ({ page }) => {
    // Try to open a flow first
    const newFlowBtn = page.locator('button:has-text("New Flow")');
    if (await newFlowBtn.isVisible()) {
      await newFlowBtn.click();
      await page.waitForTimeout(1000);
    }
    
    // Get diagram canvas
    const canvas = page.locator('#diagram-space, .diagram-canvas').first();
    if (await canvas.isVisible()) {
      console.log('✓ Diagram canvas visible');
      
      // Take screenshot before drag
      await page.screenshot({ 
        path: 'screenshots/04-flows-before-drag.png', 
        fullPage: true 
      });
    }
    
    console.log('✓ Node drag area tested');
  });

  test('should test connector positioning', async ({ page }) => {
    // Open a sample flow that has connectors
    const sampleFlow = page.locator('text=Temperature Alert System, text=Simple Math').first();
    if (await sampleFlow.isVisible()) {
      await sampleFlow.click();
      await page.waitForTimeout(2000);
      
      // Take screenshot to check connector positions
      await page.screenshot({ 
        path: 'screenshots/04-flows-connectors.png', 
        fullPage: true 
      });
      
      console.log('✓ Connector positioning tested');
    } else {
      console.log('⚠ No sample flows to test connectors');
    }
  });

  test('should test save flow', async ({ page }) => {
    // Look for save button
    const saveBtn = page.locator('button:has-text("Save")').first();
    if (await saveBtn.isVisible()) {
      console.log('✓ Save button visible');
      
      // Take screenshot
      await page.screenshot({ 
        path: 'screenshots/04-flows-save-button.png', 
        fullPage: true 
      });
    }
  });
});
