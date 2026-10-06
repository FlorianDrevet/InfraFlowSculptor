import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { loginAsAlice } from './support/login';

const HOME_GREETING_TIMEOUT = 15_000;

test('public login screen is responsive and has a named sign-in action', async ({
  page,
}, testInfo) => {
  await page.addInitScript(() => localStorage.setItem('ifs.language', 'fr'));
  await page.goto('/login');
  await expect(page.getByRole('heading', { level: 2, name: 'Connexion' })).toBeVisible();
  await expect(page.getByTestId('login-with-microsoft')).toBeVisible();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

  if (testInfo.project.name === 'desktop') {
    const introWidth = await page
      .locator('.login__intro')
      .evaluate((element) => element.getBoundingClientRect().width);
    expect(introWidth).toBeCloseTo(770, 0);
  } else {
    const pageWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    const viewportWidth = await page.evaluate(() => window.innerWidth);
    expect(pageWidth).toBeLessThanOrEqual(viewportWidth);
  }

  const { violations } = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(
    violations.filter(
      (violation) => violation.impact === 'serious' || violation.impact === 'critical',
    ),
  ).toEqual([]);

  const captureDirectory = path.resolve('e2e/__captures__');
  await mkdir(captureDirectory, { recursive: true });
  await page.screenshot({
    path: path.join(captureDirectory, `login-${testInfo.project.name}.png`),
    fullPage: true,
  });
});

test('signed-in shell is usable and accessible', async ({ page }, testInfo) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local Alice account.');
  await loginAsAlice(page);
  const expectedName = process.env['IFS_E2E_EXPECTED_NAME'] ?? 'Alice Martin';
  await expect(
    page.getByRole('heading', { name: `Bonjour ${expectedName}`, exact: true }),
  ).toBeVisible({ timeout: HOME_GREETING_TIMEOUT });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  const primaryNavigation = page.getByRole('navigation', { name: 'Navigation principale' });

  if (testInfo.project.name === 'mobile') {
    await page.getByRole('button', { name: 'Menu' }).click();
    await expect(primaryNavigation).toBeVisible();
  } else {
    await expect(primaryNavigation).toBeVisible();
  }

  await expect(primaryNavigation.getByRole('link', { name: /^Accueil/ })).toBeVisible();

  if (testInfo.project.name === 'mobile') {
    await page.keyboard.press('Escape');
    await expect(page.getByRole('button', { name: 'Menu' })).toBeFocused();
    const pageWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    const viewportWidth = await page.evaluate(() => window.innerWidth);
    expect(pageWidth).toBeLessThanOrEqual(viewportWidth);
  }

  await expect(page.getByRole('navigation', { name: 'Fil d’Ariane' })).toContainText('Accueil');

  if (testInfo.project.name === 'desktop') {
    await expect(page.locator('app-theme-switch button')).toHaveCount(0);
    await page.getByRole('group', { name: 'Langue' }).getByRole('button', { name: 'EN' }).click();
    await expect(
      page.getByRole('heading', { name: `Hello ${expectedName}`, exact: true }),
    ).toBeVisible({ timeout: HOME_GREETING_TIMEOUT });
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');

    await page.reload();
    await expect(
      page.getByRole('heading', { name: `Hello ${expectedName}`, exact: true }),
    ).toBeVisible({ timeout: HOME_GREETING_TIMEOUT });
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await page.getByRole('group', { name: 'Language' }).getByRole('button', { name: 'FR' }).click();
    await expect(
      page.getByRole('heading', { name: `Bonjour ${expectedName}`, exact: true }),
    ).toBeVisible({ timeout: HOME_GREETING_TIMEOUT });
    await expect(page.locator('html')).toHaveAttribute('lang', 'fr');
  }

  const { violations } = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(
    violations.filter(
      (violation) => violation.impact === 'serious' || violation.impact === 'critical',
    ),
  ).toEqual([]);

  const captureDirectory = path.resolve('e2e/__captures__');
  await mkdir(captureDirectory, { recursive: true });
  await page.screenshot({
    path: path.join(captureDirectory, `shell-${testInfo.project.name}.png`),
    fullPage: true,
  });
});

test('a failed current-user request shows a local reference', async ({ page }) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo account.');
  const useLiveApiOutage = process.env['IFS_E2E_USE_LIVE_API_OUTAGE'] === 'true';

  if (useLiveApiOutage) {
    test.setTimeout(30_000);
  } else {
    await page.route('**/v1/me', (route) => route.abort());
  }

  await loginAsAlice(page);
  await expect(page.getByRole('heading', { name: "Une erreur s'est produite." })).toBeVisible({
    timeout: useLiveApiOutage ? 15_000 : 5_000,
  });
  await expect(page.locator('.error-page__reference code')).toHaveText(/^local-/);
});
