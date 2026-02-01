import { test, expect } from '@playwright/test';

test.describe('Remaining Pages', () => {
  test('Connectivity Page', async ({ page }) => {
    await page.goto('/connectivity');
    await page.waitForTimeout(2000);
    
    // Check for connections list
    const connections = page.locator('text=Connections');
    if (await connections.isVisible()) {
      console.log('✓ Connections section visible');
    }
    
    // Take screenshot
    await page.screenshot({ 
      path: 'screenshots/05-connectivity.png', 
      fullPage: true 
    });
    
    console.log('✓ Connectivity page tested');
  });

  test('Diagnostics Page', async ({ page }) => {
    await page.goto('/diagnostics');
    await page.waitForTimeout(2000);
    
    // Check for system health
    const systemHealth = page.locator('text=System Health, text=Diagnostics');
    if (await systemHealth.isVisible()) {
      console.log('✓ Diagnostics content visible');
    }
    
    // Take screenshot
    await page.screenshot({ 
      path: 'screenshots/06-diagnostics.png', 
      fullPage: true 
    });
    
    console.log('✓ Diagnostics page tested');
  });

  test('Users Page', async ({ page }) => {
    await page.goto('/users');
    await page.waitForTimeout(2000);
    
    // Check for user management
    const users = page.locator('text=Users, text=User Management');
    if (await users.isVisible()) {
      console.log('✓ Users page content visible');
    }
    
    // Take screenshot
    await page.screenshot({ 
      path: 'screenshots/07-users.png', 
      fullPage: true 
    });
    
    console.log('✓ Users page tested');
  });

  test('Profile Page', async ({ page }) => {
    await page.goto('/profile');
    await page.waitForTimeout(2000);
    
    // Check for profile settings
    const profile = page.locator('text=Profile, text=Settings');
    if (await profile.isVisible()) {
      console.log('✓ Profile page content visible');
    }
    
    // Take screenshot
    await page.screenshot({ 
      path: 'screenshots/08-profile.png', 
      fullPage: true 
    });
    
    console.log('✓ Profile page tested');
  });

  test('Navigation Menu', async ({ page }) => {
    await page.goto('/');
    await page.waitForTimeout(1000);
    
    // Check main menu items
    const menuItems = ['File', 'View', 'Tools', 'Help'];
    
    for (const item of menuItems) {
      const menuItem = page.locator(`text="${item}"`).first();
      if (await menuItem.isVisible()) {
        console.log(`✓ Menu item "${item}" visible`);
        
        // Click to open dropdown
        await menuItem.click();
        await page.waitForTimeout(500);
        
        // Take screenshot of dropdown
        await page.screenshot({ 
          path: `screenshots/09-menu-${item.toLowerCase()}.png`, 
          fullPage: true 
        });
        
        // Click elsewhere to close
        await page.click('body');
        await page.waitForTimeout(300);
      }
    }
    
    console.log('✓ Navigation menu tested');
  });

  test('User Dropdown', async ({ page }) => {
    await page.goto('/');
    await page.waitForTimeout(1000);
    
    // Look for user dropdown (Admin button)
    const userDropdown = page.locator('text=Admin').first();
    if (await userDropdown.isVisible()) {
      console.log('✓ User dropdown visible');
      
      // Click to open
      await userDropdown.click();
      await page.waitForTimeout(500);
      
      // Take screenshot
      await page.screenshot({ 
        path: 'screenshots/09-user-dropdown.png', 
        fullPage: true 
      });
      
      console.log('✓ User dropdown tested');
    }
  });
});
