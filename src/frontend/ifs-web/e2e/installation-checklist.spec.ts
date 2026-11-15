import AxeBuilder from '@axe-core/playwright';
import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { expect, test } from '@playwright/test';
import { loginAsAlice } from './support/login';

const CONTOSO_ID = '33333333-3333-4333-8333-333333333333';
const CAPTURE_DIRECTORY = resolve(process.cwd(), '../../../docs/plan/revues/captures/R-08');

test.use({ trace: 'off', screenshot: 'off' });

test('Alice parcourt la checklist d’installation et retrouve sa coche dans l’audit', async ({
  page,
}, testInfo) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set the local demo password for Alice.');
  test.setTimeout(120_000);
  page.setDefaultTimeout(20_000);
  page.setDefaultNavigationTimeout(30_000);

  let apiOrigin: string | undefined;
  let authorization: string | undefined;
  page.on('request', (request) => {
    if (!request.url().includes('/v1/')) {
      return;
    }

    const requestAuthorization = request.headers()['authorization'];
    if (requestAuthorization) {
      apiOrigin ??= new URL(request.url()).origin;
      authorization ??= requestAuthorization;
    }
  });

  await loginAsAlice(page);
  await page.goto(`/o/${CONTOSO_ID}/projects`);
  const shopCard = page.locator('.projects-page__card').filter({
    has: page.locator('.projects-page__code').getByText('shop', { exact: true }),
  });
  await expect(shopCard).toBeVisible();
  await shopCard.locator('.projects-page__project-name').click();
  await page.waitForURL(/\/projects\/[0-9a-f-]{36}$/i);

  const projectId = new URL(page.url()).pathname.match(/\/projects\/([0-9a-f-]{36})$/i)?.[1];
  expect(projectId).toBeTruthy();
  await ensureDevelopmentPaymentSecret(page, projectId!, apiOrigin, authorization);
  await page.reload();
  const installationOverview = page.locator('.project-overview__installation');
  await expect(installationOverview).toBeVisible();
  await expect(installationOverview.getByText(/\d+\s*\/\s*\d+ étapes terminées/)).toBeVisible();

  const projectUrl = `/o/${CONTOSO_ID}/projects/${projectId}`;
  await expect(
    installationOverview.getByRole('link', {
      name: 'Ouvrir la liste de contrôle',
    }),
  ).toBeVisible();

  const viewportWidth = page.viewportSize()?.width ?? 1440;
  await mkdir(CAPTURE_DIRECTORY, { recursive: true });
  await page.screenshot({
    path: resolve(CAPTURE_DIRECTORY, `ProjectOverview-${viewportWidth}.png`),
    fullPage: true,
  });
  const overviewWidth = await page.evaluate(() => document.documentElement.scrollWidth);
  expect(overviewWidth).toBeLessThanOrEqual(viewportWidth);

  if (testInfo.project.name === 'mobile') {
    await page.getByRole('button', { name: 'Menu' }).click();
    await page
      .locator('.sidebar--drawer')
      .getByRole('link', { name: 'Installation', exact: true })
      .click();
    await page.getByRole('button', { name: 'Fermer' }).click();
  } else {
    await page
      .locator('.shell__sidebar')
      .getByRole('link', { name: 'Installation', exact: true })
      .click();
  }

  await page.waitForURL(new RegExp(`/projects/${projectId}/installation$`));
  await expect(
    page.getByRole('heading', { name: "Liste de contrôle d'installation", level: 1 }),
  ).toBeVisible();
  const sectionHeadings = await page
    .locator('.installation-checklist__section h2')
    .allTextContents();
  expect(sectionHeadings).toEqual([
    '1. Prérequis Azure DevOps',
    '2. Script Azure',
    '3. Étapes laissées par le script',
    '4. Pipeline d’installation Azure DevOps',
    '5. Secrets de pipeline',
    '6. Secrets gérés hors IFS',
    '7. Premier déploiement',
    '8. Points d’extension',
    '9. Bascule après une migration',
  ]);
  await expect(
    page.locator('.installation-checklist__item pre code').filter({ hasText: '-WhatIf' }),
  ).toBeVisible();

  const developmentSecret = page.getByRole('checkbox', { name: /dev.*MAIN_PAYMENTS_API_KEY/i });
  await expect(developmentSecret).toBeVisible();
  if (await developmentSecret.isChecked()) {
    const clear = page.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname.includes(`/projects/${projectId}/installation-checklist/`),
    );
    await developmentSecret.uncheck();
    expect((await clear).status()).toBe(204);
  }
  const save = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname.includes(`/projects/${projectId}/installation-checklist/`),
  );
  await developmentSecret.check();
  expect((await save).status()).toBe(204);
  await expect(developmentSecret).toBeChecked();

  await page.goto(`/organizations/${CONTOSO_ID}/audit`);
  const checklistAudit = page
    .getByRole('row')
    .filter({ hasText: 'Étape de la checklist mise à jour' })
    .filter({ hasText: 'État de la coche' });
  await expect(checklistAudit.first()).toBeVisible();
  await expect(checklistAudit.first()).toContainText('Alice Martin');

  await page.goto(`${projectUrl}/installation`);
  await expect(developmentSecret).toBeChecked();
  await expect(page.getByRole('button', { name: 'Copier la commande Azure' })).toBeVisible();
  const checklistWidth = await page.evaluate(() => document.documentElement.scrollWidth);
  expect(checklistWidth).toBeLessThanOrEqual(viewportWidth);
  expect(
    (await new AxeBuilder({ page }).include('.installation-checklist').analyze()).violations,
  ).toEqual([]);
  await page.screenshot({
    path: resolve(CAPTURE_DIRECTORY, `InstallChecklist-${viewportWidth}.png`),
    fullPage: true,
  });

  if (testInfo.project.name === 'desktop') {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(
      pathToFileURL(
        resolve(process.cwd(), '../../../docs/design/maquette-v1/preview/ProjectOverview.html'),
      ).href,
    );
    await page.screenshot({
      path: resolve(CAPTURE_DIRECTORY, 'ProjectOverview-reference-1440.png'),
      fullPage: true,
    });
    await page.goto(
      pathToFileURL(
        resolve(process.cwd(), '../../../docs/design/maquette-v1/preview/InstallChecklist.html'),
      ).href,
    );
    await page.screenshot({
      path: resolve(CAPTURE_DIRECTORY, 'InstallChecklist-reference-1440.png'),
      fullPage: true,
    });
  }
});

async function ensureDevelopmentPaymentSecret(
  page: import('@playwright/test').Page,
  projectId: string,
  apiOrigin: string | undefined,
  authorization: string | undefined,
): Promise<void> {
  expect(apiOrigin, 'The authenticated API origin should be observed after login.').toBeTruthy();
  expect(authorization, 'The authenticated API request should be observed after login.').toBeTruthy();

  const headers = {
    authorization: authorization!,
    'X-Ifs-Organization': CONTOSO_ID,
  };
  const componentsResponse = await page.request.get(
    `${apiOrigin}/v1/projects/${projectId}/components`,
    { headers },
  );
  expect(componentsResponse.status(), await componentsResponse.text()).toBe(200);
  const components = (await componentsResponse.json()) as Array<{
    id: string;
    code: string;
    resources: Array<{
      id: string;
      type: string;
      logicalName: string;
      defaultIdentityResourceId: string | null;
    }>;
  }>;
  const applicationResources = components.flatMap((component) =>
    component.resources
      .filter((resource) => resource.type === 'ContainerApp')
      .map((resource) => ({ component, resource })),
  );

  for (const { component, resource } of applicationResources) {
    const settingsResponse = await page.request.get(
      `${apiOrigin}/v1/projects/${projectId}/components/${component.id}/resources/${resource.id}/app-settings`,
      { headers },
    );
    expect(settingsResponse.status(), await settingsResponse.text()).toBe(200);
    const settings = (await settingsResponse.json()) as Array<{
      pipelineVariableName: string | null;
    }>;
    if (settings.some((setting) => setting.pipelineVariableName === 'MAIN_PAYMENTS_API_KEY')) {
      return;
    }
  }

  const keyVault = components
    .flatMap((component) => component.resources)
    .find((resource) => resource.type === 'KeyVault');
  const application = applicationResources.find(({ component }) => component.code === 'orders')
    ?? applicationResources[0];
  if (!keyVault || !application) {
    throw new Error('The local shop project needs a Key Vault and a container app for the recipe.');
  }

  const createResponse = await page.request.post(
    `${apiOrigin}/v1/projects/${projectId}/components/${application.component.id}/resources/${application.resource.id}/app-settings`,
    {
      headers: {
        ...headers,
        'idempotency-key': crypto.randomUUID(),
      },
      data: {
        name: 'Payments__ApiKey',
        sourceKind: 'KeyVaultSecret',
        literalValue: null,
        overrides: null,
        sourceResourceId: null,
        outputName: null,
        keyVaultResourceId: keyVault.id,
        secretName: 'payments-api-key',
        secretFeedKind: 'PipelineSecret',
        pipelineVariableName: null,
        identityResourceId: application.resource.defaultIdentityResourceId,
        forceNonSecret: false,
      },
    },
  );
  expect(createResponse.status(), await createResponse.text()).toBe(201);
}
