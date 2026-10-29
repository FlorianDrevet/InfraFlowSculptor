import AxeBuilder from '@axe-core/playwright';
import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { expect, test } from '@playwright/test';
import type {
  ResourceApplicationResponse,
  SaveApplicationRequest,
} from '../src/app/core/api/generated';
import { loginAsAlice } from './support/login';

const CONTOSO_ID = '33333333-3333-4333-8333-333333333333';
const RECIPE_PROJECT_NAME = 'Boutique en ligne recette J0-15';

test('Alice voit le profil Application en lecture seule pour un ancien catalogue épinglé', async ({
  page,
}) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo accounts.');
  page.setDefaultTimeout(15_000);
  page.setDefaultNavigationTimeout(30_000);

  await loginAsAlice(page);
  await page.goto(`/o/${CONTOSO_ID}/projects`);
  const projectCard = page.locator('.projects-page__card').filter({ hasText: RECIPE_PROJECT_NAME });
  await expect(projectCard).toBeVisible();
  const projectNavigation = page.waitForURL(/\/projects\/[0-9a-f-]+$/i);
  await projectCard.locator('.projects-page__project-name').click();
  await projectNavigation;

  const componentNavigation = page.waitForURL(/\/components\/[0-9a-f-]+$/i);
  await page.getByRole('link', { name: 'Orders orders', exact: true }).click();
  await componentNavigation;
  const resourceNavigation = page.waitForURL(/\/resources\/[0-9a-f-]+$/i);
  await page.getByRole('link', { name: 'api ContainerApp', exact: true }).click();
  await resourceNavigation;

  await page.getByRole('link', { name: 'Application', exact: true }).click();
  const panel = page.locator('.resource-application');
  await expect(panel.locator('.resource-application__notice')).toContainText(
    'Profil de build indisponible',
  );
  await expect(panel.locator('input, select, button[type="submit"]')).toHaveCount(0);

  const { violations } = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(
    violations.filter(
      (violation) => violation.impact === 'serious' || violation.impact === 'critical',
    ),
  ).toEqual([]);
  const viewportWidth = page.viewportSize()?.width ?? 1440;
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
    viewportWidth,
  );

  const captureDirectory = resolve(process.cwd(), 'e2e/__captures__/J0-19');
  await mkdir(captureDirectory, { recursive: true });
  await page.screenshot({
    path: resolve(captureDirectory, `ApplicationUnavailable-${viewportWidth}.png`),
    fullPage: false,
  });
});

test('Alice consulte et enregistre les réglages du profil Container', async ({ page }) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo accounts.');
  page.setDefaultTimeout(15_000);
  page.setDefaultNavigationTimeout(30_000);

  await loginAsAlice(page);
  await page.goto(`/o/${CONTOSO_ID}/projects`);
  const projectCard = page.locator('.projects-page__card').filter({ hasText: RECIPE_PROJECT_NAME });
  await expect(projectCard).toBeVisible();
  const projectNavigation = page.waitForURL(/\/projects\/[0-9a-f-]+$/i);
  await projectCard.locator('.projects-page__project-name').click();
  await projectNavigation;

  const componentNavigation = page.waitForURL(/\/components\/[0-9a-f-]+$/i);
  await page.getByRole('link', { name: 'Orders orders', exact: true }).click();
  await componentNavigation;
  const resourceNavigation = page.waitForURL(/\/resources\/[0-9a-f-]+$/i);
  await page.getByRole('link', { name: 'api ContainerApp', exact: true }).click();
  await resourceNavigation;

  let savedRequest: SaveApplicationRequest | undefined;
  await page.route('**/resources/*/application', async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({ json: containerApplicationResponse(null, 0) });
      return;
    }

    if (route.request().method() === 'PUT') {
      savedRequest = route.request().postDataJSON() as SaveApplicationRequest;
      await route.fulfill({
        json: containerApplicationResponse(savedRequest.healthCheckPath ?? null, 1),
      });
      return;
    }

    await route.continue();
  });

  await page.getByRole('link', { name: 'Application', exact: true }).click();
  const panel = page.locator('.resource-application');
  await expect(panel.getByRole('heading', { name: 'Application', exact: true })).toBeVisible();
  await expect(panel.locator('input[name="applicationSourcePath"]')).toHaveValue('src/api');
  await expect(panel.locator('input[name="dockerfilePath"]')).toHaveValue('src/api/Dockerfile');
  await expect(panel.locator('input[name="imageRepository"]')).toHaveValue('shop/orders/api');
  await expect(panel).toContainText('platform / main');
  await expect(panel).toContainText('crshopmainshared');
  await expect(panel.locator('select[id="step-param-ImageScan-minimumSeverity"]')).toHaveValue(
    'CRITICAL',
  );

  await panel.locator('input[name="healthCheckPath"]').fill('/healthz');
  const saveResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname.endsWith('/application'),
  );
  await panel.getByRole('button', { name: 'Enregistrer', exact: true }).click();
  expect((await saveResponse).status()).toBe(200);
  expect(savedRequest).toMatchObject({
    expectedVersion: 0,
    imageRepository: 'shop/orders/api',
    healthCheckPath: '/healthz',
    steps: expect.arrayContaining([
      expect.objectContaining({
        name: 'ImageScan',
        blocking: true,
        parameters: expect.arrayContaining([{ name: 'minimumSeverity', value: 'CRITICAL' }]),
      }),
    ]),
  });
  await expect(panel.locator('input[name="healthCheckPath"]')).toHaveValue('/healthz');

  const { violations } = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(
    violations.filter(
      (violation) => violation.impact === 'serious' || violation.impact === 'critical',
    ),
  ).toEqual([]);
  const viewportWidth = page.viewportSize()?.width ?? 1440;
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
    viewportWidth,
  );

  const captureDirectory = resolve(process.cwd(), 'e2e/__captures__/J0-19');
  await mkdir(captureDirectory, { recursive: true });
  await page.screenshot({
    path: resolve(captureDirectory, `ApplicationAvailable-${viewportWidth}.png`),
    fullPage: false,
  });
});

function containerApplicationResponse(
  healthCheckPath: string | null,
  version: number,
): ResourceApplicationResponse {
  return {
    available: true,
    catalogVersion: '2026.10-pilot.3',
    buildProfile: 'Container',
    unavailableReason: null,
    application: {
      version,
      name: 'api',
      codeSourcePath: 'src/api',
      dockerfilePath: 'src/api/Dockerfile',
      buildContextPath: 'src/api',
      imageRepository: 'shop/orders/api',
      healthCheckPath,
      deploymentStrategy: 'Direct',
      buildRegistry: {
        available: true,
        displayName: 'platform / main',
        registryName: 'crshopmainshared',
        promotionRequired: false,
        selectedEnvironmentId: null,
        hasValidSelection: true,
        options: [],
      },
      steps: [
        {
          name: 'DependencyCache',
          label: 'Cache des dépendances',
          description: 'Met en cache les dépendances du dépôt.',
          enabled: true,
          blocking: false,
          position: 'BeforeBuild',
          parameters: [],
        },
        {
          name: 'ImageScan',
          label: 'Scan de l’image',
          description: 'Analyse l’image de conteneur avec Trivy.',
          enabled: true,
          blocking: true,
          position: 'AfterBuild',
          parameters: [
            {
              name: 'tool',
              kind: 'enum',
              value: 'Trivy',
              defaultValue: 'Trivy',
              values: ['Trivy'],
            },
            {
              name: 'minimumSeverity',
              kind: 'enum',
              value: 'CRITICAL',
              defaultValue: 'CRITICAL',
              values: ['CRITICAL', 'HIGH', 'MEDIUM', 'LOW'],
            },
            { name: 'exceptions', kind: 'string', value: '', defaultValue: '', values: [] },
          ],
        },
      ],
      findings: [],
    },
  };
}
