import { test, expect } from '@playwright/test';

test.describe('Smoke Tests', () => {

  test('User Login Page Loads', async ({ page }) => {
    await page.goto('/login');
    // Verify that the login form or a known element is present
    await expect(page).toHaveTitle(/Smooth Operator/i);
    // Smoke check: ensure the page didn't crash
    const loginButton = page.locator('button', { hasText: /log\s?in|sign\s?in/i });
    if (await loginButton.count() > 0) {
      await expect(loginButton.first()).toBeVisible();
    }
  });

  test('User Management Route is accessible', async ({ page }) => {
    // If we are not logged in, this might redirect to /login.
    // That's acceptable for a basic smoke test if we just want to ensure the route exists and doesn't 500/404.
    const response = await page.goto('/settings/users');
    expect(response?.status()).toBeLessThan(400);
  });

  test('Vaults Route is accessible', async ({ page }) => {
    const response = await page.goto('/settings/vaults');
    expect(response?.status()).toBeLessThan(400);
  });

  test('Connections Route is accessible', async ({ page }) => {
    const response = await page.goto('/connections');
    expect(response?.status()).toBeLessThan(400);
  });

  test('Credentials/Keys Route is accessible', async ({ page }) => {
    const response = await page.goto('/credentials');
    expect(response?.status()).toBeLessThan(400);
  });

});
