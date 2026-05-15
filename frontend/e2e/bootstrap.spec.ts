import { test, expect } from '@playwright/test';

/**
 * Smooth Operator: "First Run" Bootstrap E2E Flow
 *
 * This test simulates a fresh deployment:
 * 1. ROOT Account Creation (First Access)
 * 2. Login with the new account
 * 3. Create a new Vault
 * 4. Create a new Connection (SSH)
 */

test.describe('First Run Bootstrap Flow', () => {
  const adminEmail = `admin-${Date.now()}@example.com`;
  const adminPass = 'Password123!!';
  const adminName = 'Root Admin';

  test.afterEach(async ({ page }, testInfo) => {
    if (testInfo.status !== testInfo.expectedStatus) {
      const screenshotPath = `test-results/screenshots/${testInfo.title.replace(/\s+/g, '-')}-failed.png`;
      await page.screenshot({ path: screenshotPath, fullPage: true });
      console.log(`Test failed. Screenshot saved to ${screenshotPath}`);
      console.log('Final URL:', page.url());

      // Try to find error messages on the page
      const errorMsg = await page
        .locator('.bg-error-container, .text-error')
        .first()
        .innerText()
        .catch(() => null);
      if (errorMsg) console.log('Error message found on page:', errorMsg);
    }
  });

  test('Complete Bootstrap Flow', async ({ page }) => {
    console.log('Starting Bootstrap Flow test...');

    // 1. Landing: Go to root
    await page.goto('/', { waitUntil: 'networkidle' });
    console.log('Landed on:', page.url());

    // Check if we are redirected to setup or login
    if (page.url().includes('/first-access')) {
      console.log('Setup mode detected');
      await page.fill('input[id="fullName"]', adminName);
      await page.fill('input[id="email"]', adminEmail);
      await page.fill('input[id="password"]', adminPass);
      await page.fill('input[id="confirmPassword"]', adminPass);
      await page.click('button[type="submit"]');
      console.log('Submitted root account creation');
    } else {
      console.log('Login mode detected. Trying default dev admin...');
      await page.fill('#email', 'admin@example.com');
      await page.fill('#password', 'Password123!!');
      await page.click('button[type="submit"]');

      // If login fails (invalid credentials error), try the root account we use for tests
      const errorVisible = await page
        .locator('.bg-error-container, .text-error')
        .isVisible()
        .catch(() => false);
      if (errorVisible) {
        console.log('Default login failed. Trying test root account...');
        await page.fill('#email', adminEmail);
        await page.fill('#password', adminPass);
        await page.click('button[type="submit"]');
      }
    }

    // 2. Wait for landing page after login/setup
    console.log('Waiting for post-auth redirect...');
    await expect(page).toHaveURL(/\/vault|administration|settings/, { timeout: 20000 });
    console.log('Successfully reached dashboard:', page.url());

    // 3. Create a Vault
    await page.goto('/settings/vaults');
    // The input has name="vaultName" and placeholder="Vault name"
    await page.waitForSelector('input[name="vaultName"]', { state: 'visible' });

    const vaultName = `Vault ${Date.now()}`;
    await page.fill('input[name="vaultName"]', vaultName);
    await page.click('button:has-text("Create vault")');
    console.log('Submitted vault creation:', vaultName);

    // Wait for the vault to appear in the table
    await expect(page.locator('table')).toContainText(vaultName, { timeout: 10000 });
    console.log('Vault confirmed in list');

    // 4. Create a Connection
    await page.goto('/connections');
    await page.click('button:has-text("New Connection")');
    await page.waitForSelector('#connectionName', { state: 'visible' });

    const connectionName = `Srv ${Date.now()}`;
    await page.fill('#connectionName', connectionName);
    await page.selectOption('#connectionProtocol', 'ssh');
    await page.fill('#connectionNewHostAddress', '10.0.0.50');

    // Select the vault we just created
    await page.selectOption('#connectionVaultGroup', { label: vaultName });

    await page.click('button:has-text("Save Connection")');
    console.log('Submitted connection creation:', connectionName);

    // Verify it appeared in the list
    await expect(page.locator('table')).toContainText(connectionName);
    await expect(page.locator('table')).toContainText('10.0.0.50');
    console.log('Connection confirmed in list');

    // 5. Final check: Go back to Vault view and see the connection
    await page.goto('/vault');
    // Connections on the dashboard are displayed in cards
    await expect(page.locator('.scan-card')).toContainText(connectionName);
    console.log('Bootstrap flow complete');
  });
});
