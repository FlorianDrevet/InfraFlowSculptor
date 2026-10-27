import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { loginAsAlice, loginAsUser } from './support/login';

const ORGANIZATION_NAME = `Contoso Labs ${Date.now()} ${Math.random().toString(36).slice(2, 7)}`;

async function openMobileMenu(page: Page, projectName: string): Promise<void> {
  if (projectName === 'mobile') {
    await page.getByRole('button', { name: 'Menu' }).click();
  }
}

async function closeMobileMenu(page: Page, projectName: string): Promise<void> {
  if (projectName === 'mobile') {
    await page.getByRole('button', { name: 'Fermer' }).click();
  }
}

test('Alice crée, renomme et sélectionne une organisation', async ({ page }, testInfo) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo accounts.');

  await loginAsAlice(page);
  await openMobileMenu(page, testInfo.project.name);
  const sidebar = testInfo.project.name === 'mobile'
    ? page.locator('.sidebar--drawer')
    : page.locator('.shell__sidebar');
  const selector = sidebar.getByRole('combobox', { name: 'Organisation active' });
  await expect(selector).toHaveValue(/.+/);
  await expect(selector.locator('option:checked')).toHaveText('Contoso');
  await sidebar.getByRole('link', { name: 'Créer une organisation', exact: true }).click();
  await expect(page).toHaveURL(/\/organizations\/new$/);
  await closeMobileMenu(page, testInfo.project.name);
  await expect(page.getByRole('heading', { name: 'Créer une organisation' })).toBeVisible();
  const { violations } = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(
    violations.filter(
      (violation) => violation.impact === 'serious' || violation.impact === 'critical',
    ),
  ).toEqual([]);
  await page.getByLabel("Nom de l'organisation").fill(ORGANIZATION_NAME);
  await page.getByRole('button', { name: 'Créer', exact: true }).click();
  await expect(page).toHaveURL(/\/$/);
  await openMobileMenu(page, testInfo.project.name);
  await expect(selector.locator('option:checked')).toHaveText(ORGANIZATION_NAME);

  await sidebar.getByRole('link', { name: 'Organisation', exact: true }).click();
  await expect(page).toHaveURL(/\/organizations\/[^/]+\/settings$/);
  await closeMobileMenu(page, testInfo.project.name);
  await expect(page.getByRole('heading', { name: 'Organisation — plan et paramètres' })).toBeVisible();
  const renamedOrganization = `${ORGANIZATION_NAME} rénovée`;
  await page.getByLabel("Nom de l'organisation").fill(renamedOrganization);
  await page.getByRole('button', { name: 'Enregistrer', exact: true }).click();
  await openMobileMenu(page, testInfo.project.name);
  await expect(sidebar.getByRole('combobox', { name: 'Organisation active' }).locator('option:checked'))
    .toHaveText(renamedOrganization);

  await selector.selectOption({ label: 'Contoso' });
  await expect(selector.locator('option:checked')).toHaveText('Contoso');
});

test('David ne peut pas ouvrir les réglages d’une organisation étrangère', async ({ page, browser }, testInfo) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo accounts.');

  await loginAsAlice(page);
  await openMobileMenu(page, testInfo.project.name);
  const organizationSelector = testInfo.project.name === 'mobile'
    ? page.locator('.sidebar--drawer').getByRole('combobox', { name: 'Organisation active' })
    : page.locator('.shell__sidebar').getByRole('combobox', { name: 'Organisation active' });
  await expect(organizationSelector).toHaveValue(/.+/);
  const organizationId = await organizationSelector.inputValue();
  const foreignOrganizationUrl = new URL(`/organizations/${organizationId}/settings`, page.url()).toString();
  await closeMobileMenu(page, testInfo.project.name);
  await page.goto(foreignOrganizationUrl);
  await expect(page.getByRole('heading', { name: 'Organisation — plan et paramètres' })).toBeVisible();
  const davidPage = await browser.newPage({
    viewport: page.viewportSize() ?? undefined,
    baseURL: process.env['IFS_E2E_BASE_URL'] ?? 'http://localhost:4200',
    ignoreHTTPSErrors: true,
    locale: 'fr-FR',
  });
  await loginAsUser(davidPage, 'david@fabrikam.example');
  await openMobileMenu(davidPage, testInfo.project.name);
  const davidSidebar = testInfo.project.name === 'mobile'
    ? davidPage.locator('.sidebar--drawer')
    : davidPage.locator('.shell__sidebar');
  const davidOrganizationSelector = davidSidebar.getByRole('combobox', { name: 'Organisation active' });
  await expect(davidOrganizationSelector.locator('option')).toHaveText([
    'Choisir une organisation',
    'Fabrikam',
  ]);
  await expect(davidOrganizationSelector.locator('option:checked')).toHaveText('Fabrikam');
  await closeMobileMenu(davidPage, testInfo.project.name);
  await davidPage.goto(foreignOrganizationUrl);
  await expect(davidPage.getByRole('heading', { name: "Cette page n'existe pas." })).toBeVisible();
  await davidPage.close();
});

test('Emma sans organisation arrive sur la page de création', async ({ page }) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo accounts.');

  await loginAsUser(page, 'emma@outlook.example');
  await expect(page.getByRole('heading', { name: 'Créer une organisation' })).toBeVisible();
  await expect(page.getByLabel("Nom de l'organisation")).toBeVisible();
});
