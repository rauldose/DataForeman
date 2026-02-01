import { test, expect } from '@playwright/test';

test.describe('Login Page', () => {
  test('should display login form', async ({ page }) => {
    await page.goto('/login');
    
    // Check page title
    await expect(page).toHaveTitle(/DataForeman/);
    
    // Check login form elements
    await expect(page.locator('input[type="email"]')).toBeVisible();
    await expect(page.locator('input[type="password"]')).toBeVisible();
    await expect(page.locator('button:has-text("Login")')).toBeVisible();
    
    // Take screenshot
    await page.screenshot({ 
      path: 'screenshots/01-login-page.png', 
      fullPage: true 
    });
    
    console.log('✓ Login page rendered correctly');
  });

  test('should handle login attempt', async ({ page }) => {
    await page.goto('/login');
    
    // Fill in credentials
    await page.fill('input[type="email"]', 'admin@example.com');
    await page.fill('input[type="password"]', 'password');
    
    // Click login button
    await page.click('button:has-text("Login")');
    
    // Wait for navigation or error message
    await page.waitForTimeout(2000);
    
    // Take screenshot of result
    await page.screenshot({ 
      path: 'screenshots/01-login-result.png', 
      fullPage: true 
    });
    
    console.log('✓ Login flow executed');
  });
});
