import AxeBuilder from '@axe-core/playwright';
import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { expect, test } from '@playwright/test';
import { loginAsAlice } from './support/login';

const CONTOSO_ID = '33333333-3333-4333-8333-333333333333';
const TEST_SUBSCRIPTION_ID = '00000000-0000-0000-0000-000000000001';

test('Alice crée un projet et reprend son brouillon depuis un autre navigateur', async ({
  page,
  browser,
}) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo accounts.');

  await loginAsAlice(page);
  await page.goto(`/o/${CONTOSO_ID}/projects/new`);
  await expect(page.getByRole('heading', { name: 'Identité du projet' })).toBeVisible();
  await page.getByRole('button', { name: /Identité/ }).click();

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

  const environmentCount = await page.locator('.wizard-environment').count();
  for (let index = environmentCount; index > 2; index -= 1) {
    await page.getByRole('button', { name: `Supprimer l’environnement ${index}` }).click();
  }
  for (let index = environmentCount; index < 2; index += 1) {
    await page.getByRole('button', { name: 'Ajouter un environnement' }).click();
  }

  await page.locator('input[name="environmentName0"]').fill('Développement');
  await page.locator('input[name="environmentCode0"]').fill('dev');
  await page.locator('select[name="environmentRegion0"]').selectOption('francecentral');
  await page.locator('input[name="environmentSubscription0"]').fill(TEST_SUBSCRIPTION_ID);

  await page.locator('input[name="environmentName1"]').fill('Production');
  await page.locator('input[name="environmentCode1"]').fill('prd');
  await page.locator('select[name="environmentRegion1"]').selectOption('francecentral');
  await page.locator('input[name="environmentSubscription1"]').fill(TEST_SUBSCRIPTION_ID);
  const protectedToggle = page.getByRole('checkbox', {
    name: 'Protégé : chaque déploiement exige une approbation',
  });
  if (!(await protectedToggle.isChecked())) {
    await protectedToggle.check();
  }
  await page.getByLabel('Approbateurs', { exact: true }).fill('Shop Release Approvers');

  await page.getByRole('button', { name: 'Continuer : nommage' }).click();
  await expect(page.getByRole('heading', { name: 'Convention de nommage' })).toBeVisible();
  await expect(page.getByText(`kv-${projectCode}-main-dev`, { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Continuer : publication' }).click();
  await expect(page.getByRole('heading', { name: 'Où publier les fichiers' })).toBeVisible();
  await page.close();

  const resumePage = await browser.newPage({
    baseURL: process.env['IFS_E2E_BASE_URL'] ?? 'http://localhost:4200',
    viewport: page.viewportSize() ?? undefined,
    ignoreHTTPSErrors: true,
    locale: 'fr-FR',
  });
  try {
    await loginAsAlice(resumePage);
    await resumePage.goto(`/o/${CONTOSO_ID}/projects/new`);
    await expect(
      resumePage.getByRole('heading', { name: 'Où publier les fichiers' }),
    ).toBeVisible();
    await expect(resumePage.getByText(projectName, { exact: true })).toBeVisible();

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
    await resumePage.close();
  }
});
