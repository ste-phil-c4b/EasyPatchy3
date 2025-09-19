const { test, expect } = require('@playwright/test');

test('homepage loads successfully', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle(/EasyPatchy/);
});

test('can navigate to patches page', async ({ page }) => {
  await page.goto('/');
  await page.click('text=Patches');
  await expect(page).toHaveURL(/.*patches/);
});

test('can navigate to versions page', async ({ page }) => {
  await page.goto('/');
  await page.click('text=Versions');
  await expect(page).toHaveURL(/.*browse/);
});