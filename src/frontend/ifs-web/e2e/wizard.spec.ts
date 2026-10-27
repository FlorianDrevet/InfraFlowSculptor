import AxeBuilder from '@axe-core/playwright';
import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { type Page, type Response, expect, test } from '@playwright/test';
import { loginAsAlice } from './support/login';

const CONTOSO_ID = '33333333-3333-4333-8333-333333333333';
const TEST_SUBSCRIPTION_ID = '00000000-0000-4000-8000-000000000001';

test('Alice crée un projet et reprend son brouillon depuis un autre navigateur', async ({
  page,
  browser,
}) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo accounts.');

  await loginAsAlice(page);
  await selectFrench(page);
  await page.goto(`/o/${CONTOSO_ID}/projects/new`);
  await page.getByRole('button', { name: /Identité/ }).click();
  await expect(page.getByRole('heading', { name: 'Identité du projet' })).toBeVisible();

  const suffix = Date.now().toString(36).slice(-7);
  const projectName = `Boutique en ligne ${suffix}`;
  const projectCode = `p${suffix}`;
  await page.getByLabel('Nom', { exact: true }).fill(projectName);
  await page.getByLabel('Code', { exact: true }).fill(projectCode);

  await page.getByRole('button', { name: 'Continuer : outils' }).click();
  await expect(page.getByRole('heading', { name: 'Vos outils' })).toBeVisible();
  await expect(page.getByText('Bicep', { exact: true })).toBeVisible();
  await expect(page.getByText('Azure DevOps Pipelines', { exact: true })).toBeVisible();
  await expect(page.getByText('Terraform', { exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Continuer : environnements' }).click();

  const environments = page.locator('.wizard-environment');
  await expect(environments.first()).toBeVisible();
  const environmentCount = await environments.count();
  for (let index = environmentCount; index > 2; index -= 1) {
    await page.getByRole('button', { name: `Supprimer l’environnement ${index}` }).click();
  }
  for (let index = environmentCount; index < 2; index += 1) {
    await page.getByRole('button', { name: 'Ajouter un environnement' }).click();
  }
  await expect(environments).toHaveCount(2);

  await page.locator('input[name="environmentName0"]').fill('Développement');
  await page.locator('input[name="environmentCode0"]').fill('dev');
  await page.locator('select[name="environmentRegion0"]').selectOption('francecentral');
  await page.locator('input[name="environmentSubscription0"]').fill(TEST_SUBSCRIPTION_ID);

  await page.locator('input[name="environmentName1"]').fill('Production');
  await page.locator('input[name="environmentCode1"]').fill('prd');
  await page.locator('select[name="environmentRegion1"]').selectOption('francecentral');
  await page.locator('input[name="environmentSubscription1"]').fill(TEST_SUBSCRIPTION_ID);
  const protectedToggle = page
    .locator('.wizard-environment')
    .nth(1)
    .getByRole('switch', { name: 'Protégé : chaque déploiement exige une approbation' });
  if ((await protectedToggle.getAttribute('aria-checked')) !== 'true') {
    await protectedToggle.click();
  }
  await page.getByLabel('Approbateurs', { exact: true }).fill('Shop Release Approvers');

  await page.getByRole('button', { name: 'Continuer : nommage' }).click();
  await expect(page.getByRole('heading', { name: 'Convention de nommage' })).toBeVisible();
  await expect(page.getByText(`kv-${projectCode}-main-dev`, { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Continuer : publication' }).click();
  await expect(page.getByRole('heading', { name: 'Où publier les fichiers' })).toBeVisible();
  await page.close();

  const resumeContext = await browser.newContext({
    baseURL: process.env['IFS_E2E_BASE_URL'] ?? 'http://localhost:4200',
    viewport: page.viewportSize() ?? undefined,
    ignoreHTTPSErrors: true,
    locale: 'fr-FR',
  });
  const resumePage = await resumeContext.newPage();
  try {
    await loginAsAlice(resumePage);
    await selectFrench(resumePage);
    await resumePage.goto(`/o/${CONTOSO_ID}/projects/new`);
    await expect(
      resumePage.getByRole('heading', { name: 'Où publier les fichiers' }),
    ).toBeVisible();
    await resumePage.getByRole('button', { name: 'Identité' }).click();
    await expect(resumePage.getByLabel('Nom', { exact: true })).toHaveValue(projectName);
    await resumePage.getByRole('button', { name: 'Publication' }).click();
    await expect(
      resumePage.getByRole('heading', { name: 'Où publier les fichiers' }),
    ).toBeVisible();

    await resumePage.getByRole('button', { name: 'Passer' }).click();
    await expect(resumePage.getByRole('heading', { name: 'Récapitulatif' })).toBeVisible();
    const documentWidth = await resumePage.evaluate(() => document.documentElement.scrollWidth);
    expect(documentWidth).toBeLessThanOrEqual(resumePage.viewportSize()?.width ?? 1440);

    const accessibility = await new AxeBuilder({ page: resumePage })
      .include('.project-wizard')
      .analyze();
    expect(accessibility.violations).toEqual([]);

    const captureDirectory = resolve(process.cwd(), 'e2e/__captures__');
    await mkdir(captureDirectory, { recursive: true });
    await resumePage.screenshot({
      path: resolve(captureDirectory, `Assistant-${resumePage.viewportSize()?.width ?? 1440}.png`),
      fullPage: true,
    });

    const createResponse = resumePage.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname === '/v1/projects/from-wizard',
    );
    await resumePage.getByRole('button', { name: 'Créer le projet' }).click();
    expect((await createResponse).status()).toBe(201);
    await expect(resumePage.getByRole('heading', { name: 'Projet créé' })).toBeVisible();
    await expect(resumePage.getByText('vous en êtes propriétaire')).toBeVisible();
  } finally {
    await resumeContext.close();
  }
});

async function selectFrench(page: Page): Promise<void> {
  if ((page.viewportSize()?.width ?? 1440) <= 760) {
    await page.getByRole('button', { name: /Menu/ }).click();
  }

  const languageSwitch = page.locator('app-language-switch [role="group"]:visible');
  const frenchButton = languageSwitch.getByRole('button', { name: 'FR', exact: true });
  const englishButton = languageSwitch.getByRole('button', { name: 'EN', exact: true });
  if ((await frenchButton.getAttribute('aria-pressed')) === 'true') {
    const englishPreferenceSaved = waitForPreferenceSave(page);
    await englishButton.click();
    expect((await englishPreferenceSaved).ok()).toBeTruthy();
  }

  const frenchPreferenceSaved = waitForPreferenceSave(page);
  await frenchButton.click();
  await expect(page.locator('html')).toHaveAttribute('lang', 'fr');
  expect((await frenchPreferenceSaved).ok()).toBeTruthy();

  if ((page.viewportSize()?.width ?? 1440) <= 760) {
    await page.keyboard.press('Escape');
  }
}

function waitForPreferenceSave(page: Page): Promise<Response> {
  return page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname === '/v1/me/preferences',
  );
}
