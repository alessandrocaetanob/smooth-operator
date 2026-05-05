# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: bootstrap.spec.ts >> First Run Bootstrap Flow >> Complete Bootstrap Flow
- Location: e2e\bootstrap.spec.ts:31:7

# Error details

```
Error: page.goto: net::ERR_CONNECTION_REFUSED at http://localhost:4200/
Call log:
  - navigating to "http://localhost:4200/", waiting until "networkidle"

```

```
Test timeout of 30000ms exceeded while running "afterEach" hook.
```

# Test source

```ts
  1   | import { test, expect } from '@playwright/test';
  2   | 
  3   | /**
  4   |  * Smooth Operator: "First Run" Bootstrap E2E Flow
  5   |  * 
  6   |  * This test simulates a fresh deployment:
  7   |  * 1. ROOT Account Creation (First Access)
  8   |  * 2. Login with the new account
  9   |  * 3. Create a new Vault
  10  |  * 4. Create a new Connection (SSH)
  11  |  */
  12  | 
  13  | test.describe('First Run Bootstrap Flow', () => {
  14  |   const adminEmail = `admin-${Date.now()}@example.com`;
  15  |   const adminPass = 'Password123!!';
  16  |   const adminName = 'Root Admin';
  17  | 
> 18  |   test.afterEach(async ({ page }, testInfo) => {
      |        ^ Test timeout of 30000ms exceeded while running "afterEach" hook.
  19  |     if (testInfo.status !== testInfo.expectedStatus) {
  20  |       const screenshotPath = `test-results/screenshots/${testInfo.title.replace(/\s+/g, '-')}-failed.png`;
  21  |       await page.screenshot({ path: screenshotPath, fullPage: true });
  22  |       console.log(`Test failed. Screenshot saved to ${screenshotPath}`);
  23  |       console.log('Final URL:', page.url());
  24  |       
  25  |       // Try to find error messages on the page
  26  |       const errorMsg = await page.locator('.bg-error-container, .text-error').first().innerText().catch(() => null);
  27  |       if (errorMsg) console.log('Error message found on page:', errorMsg);
  28  |     }
  29  |   });
  30  | 
  31  |   test('Complete Bootstrap Flow', async ({ page }) => {
  32  |     console.log('Starting Bootstrap Flow test...');
  33  |     
  34  |     // 1. Landing: Go to root
  35  |     await page.goto('/', { waitUntil: 'networkidle' });
  36  |     console.log('Landed on:', page.url());
  37  |     
  38  |     // Check if we are redirected to setup or login
  39  |     if (page.url().includes('/first-access')) {
  40  |       console.log('Setup mode detected');
  41  |       await page.fill('input[id="fullName"]', adminName);
  42  |       await page.fill('input[id="email"]', adminEmail);
  43  |       await page.fill('input[id="password"]', adminPass);
  44  |       await page.fill('input[id="confirmPassword"]', adminPass);
  45  |       await page.click('button[type="submit"]');
  46  |       console.log('Submitted root account creation');
  47  |     } else {
  48  |       console.log('Login mode detected. Trying default dev admin...');
  49  |       await page.fill('#email', 'admin@example.com');
  50  |       await page.fill('#password', 'Password123!!');
  51  |       await page.click('button[type="submit"]');
  52  |       
  53  |       // If login fails (invalid credentials error), try the root account we use for tests
  54  |       const errorVisible = await page.locator('.bg-error-container, .text-error').isVisible().catch(() => false);
  55  |       if (errorVisible) {
  56  |         console.log('Default login failed. Trying test root account...');
  57  |         await page.fill('#email', adminEmail);
  58  |         await page.fill('#password', adminPass);
  59  |         await page.click('button[type="submit"]');
  60  |       }
  61  |     }
  62  | 
  63  |     // 2. Wait for landing page after login/setup
  64  |     console.log('Waiting for post-auth redirect...');
  65  |     await expect(page).toHaveURL(/\/vault|administration|settings/, { timeout: 20000 });
  66  |     console.log('Successfully reached dashboard:', page.url());
  67  |     
  68  |     // 3. Create a Vault
  69  |     await page.goto('/settings/vaults');
  70  |     // The input has name="vaultName" and placeholder="Vault name"
  71  |     await page.waitForSelector('input[name="vaultName"]', { state: 'visible' });
  72  |     
  73  |     const vaultName = `Vault ${Date.now()}`;
  74  |     await page.fill('input[name="vaultName"]', vaultName);
  75  |     await page.click('button:has-text("Create vault")');
  76  |     console.log('Submitted vault creation:', vaultName);
  77  | 
  78  |     // Wait for the vault to appear in the table
  79  |     await expect(page.locator('table')).toContainText(vaultName, { timeout: 10000 });
  80  |     console.log('Vault confirmed in list');
  81  | 
  82  |     // 4. Create a Connection
  83  |     await page.goto('/connections');
  84  |     await page.click('button:has-text("New Connection")');
  85  |     await page.waitForSelector('#connectionName', { state: 'visible' });
  86  | 
  87  |     const connectionName = `Srv ${Date.now()}`;
  88  |     await page.fill('#connectionName', connectionName);
  89  |     await page.selectOption('#connectionProtocol', 'ssh');
  90  |     await page.fill('#connectionNewHostAddress', '10.0.0.50');
  91  |     
  92  |     // Select the vault we just created
  93  |     await page.selectOption('#connectionVaultGroup', { label: vaultName });
  94  | 
  95  |     await page.click('button:has-text("Save Connection")');
  96  |     console.log('Submitted connection creation:', connectionName);
  97  | 
  98  |     // Verify it appeared in the list
  99  |     await expect(page.locator('table')).toContainText(connectionName);
  100 |     await expect(page.locator('table')).toContainText('10.0.0.50');
  101 |     console.log('Connection confirmed in list');
  102 | 
  103 |     // 5. Final check: Go back to Vault view and see the connection
  104 |     await page.goto('/vault');
  105 |     // Connections on the dashboard are displayed in cards
  106 |     await expect(page.locator('.scan-card')).toContainText(connectionName);
  107 |     console.log('Bootstrap flow complete');
  108 |   });
  109 | });
  110 | 
```