import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';
import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { expect, test, type Locator, type Page, type Route } from '@playwright/test';
import { loginAsAlice } from './support/login';

const CONTOSO_ID = '33333333-3333-4333-8333-333333333333';
const WRONG_RESOURCE_ID =
  '/subscriptions/00000000-0000-4000-8000-000000000001/resourceGroups/rg-wrong/providers/Microsoft.Sql/servers/wrong';

test('Alice ajoute, règle et supprime des ressources du catalogue', async ({ page }) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo accounts.');
  test.setTimeout(180_000);
  page.setDefaultTimeout(15_000);
  page.setDefaultNavigationTimeout(30_000);

  await loginAsAlice(page);
  await page.goto(`/o/${CONTOSO_ID}/projects`);
  const shopCard = page.locator('.projects-page__card').filter({ hasText: 'shop' });
  await expect(shopCard).toBeVisible();
  const projectNavigation = page.waitForURL(/\/projects\/[0-9a-f-]+$/i);
  await shopCard.locator('.projects-page__project-name').click();
  await projectNavigation;
  const projectId = new URL(page.url()).pathname.match(/\/projects\/([^/]+)$/)?.[1];
  expect(projectId).toBeTruthy();

  const componentCode = (page.viewportSize()?.width ?? 1440) <= 760 ? 'r14mob' : 'r14desk';
  const componentName = `Ressources J0-14 ${componentCode}`;
  const componentsUrl = `/o/${CONTOSO_ID}/projects/${projectId}/components`;
  const environmentsUrl = `/o/${CONTOSO_ID}/projects/${projectId}/environments`;
  const createdEnvironmentIds: string[] = [];
  let componentUrl: string | undefined;

  try {
    componentUrl = await createRecipeComponent(page, componentsUrl, componentName, componentCode);
    await ensureRecipeEnvironments(page, environmentsUrl, componentCode, createdEnvironmentIds);
    const captureDirectory = resolve(process.cwd(), 'e2e/__captures__/R-05');
    await mkdir(captureDirectory, { recursive: true });
    const viewportWidth = page.viewportSize()?.width ?? 1440;

    await page.goto(componentUrl);
    await expect(page.locator('#resources-heading')).toBeVisible();
    await addResource(
      page,
      'Log Analytics',
      'LogAnalyticsWorkspace',
      'main',
      captureDirectory,
      viewportWidth,
    );
    await page.getByRole('link', { name: 'Propriétés', exact: true }).click();
    await waitForViewTransition(page);
    const retentionRow = page
      .locator('.property-grid__row')
      .filter({ hasText: 'Rétention (jours)' });
    await expect(retentionRow).toBeVisible();
    const environmentHeaders = page.locator('.property-grid__header [role="columnheader"]');
    const headerLabels = (await environmentHeaders.allTextContents()).map((label) => label.trim());
    expect(headerLabels).toContain('dev');
    expect(headerLabels).toContain('prd');
    const retentionCells = retentionRow.locator('.property-grid__cell');
    const developmentCell = propertyCellForEnvironment(retentionCells, headerLabels, 'dev');
    const productionCell = propertyCellForEnvironment(retentionCells, headerLabels, 'prd');
    await expect(retentionCells.first().locator('input')).toHaveValue('30');
    await expect(developmentCell.locator('input')).toHaveValue('30');
    await expect(productionCell).toHaveClass(/property-grid__cell--catalog-default/);
    await assertNoSeriousAccessibilityViolations(page);
    await assertNoPageHorizontalOverflow(page);
    await page.screenshot({
      path: resolve(captureDirectory, `ResourceProperties-${viewportWidth}.png`),
      fullPage: false,
    });

    const overrideResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname.endsWith('/overrides'),
    );
    await productionCell.locator('input').fill('90');
    await productionCell.locator('input').press('Tab');
    expect(
      (await overrideResponse).status(),
      'the override should use the resource version expected by the API',
    ).toBe(200);
    const updatedRetentionRow = page
      .locator('.property-grid__row')
      .filter({ hasText: 'Rétention (jours)' });
    const updatedProductionCell = propertyCellForEnvironment(
      updatedRetentionRow.locator('.property-grid__cell'),
      headerLabels,
      'prd',
    );
    await expect(updatedProductionCell.locator('input')).toHaveValue('90');
    await expect(updatedProductionCell).toHaveClass(/property-grid__cell--override/);
    await expect(updatedProductionCell.locator('app-provenance summary')).toHaveAttribute(
      'title',
      'Surcharge de l’environnement',
    );

    const clearResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'DELETE' &&
        new URL(response.url()).pathname.endsWith('/overrides'),
    );
    await updatedProductionCell.getByRole('button', { name: 'Effacer la surcharge' }).click();
    expect((await clearResponse).status()).toBe(200);
    const inheritedRetentionRow = page
      .locator('.property-grid__row')
      .filter({ hasText: 'Rétention (jours)' });
    const inheritedProductionCell = propertyCellForEnvironment(
      inheritedRetentionRow.locator('.property-grid__cell'),
      headerLabels,
      'prd',
    );
    await expect(inheritedProductionCell.locator('input')).toHaveValue('30');
    await expect(inheritedProductionCell).toHaveClass(/property-grid__cell--catalog-default/);
    await expect(inheritedProductionCell.locator('app-provenance summary')).toHaveAttribute(
      'title',
      'Défaut du catalogue',
    );

    const session = await page.evaluate(() => Object.entries(sessionStorage));
    const stalePage = await page.context().newPage();
    await stalePage.addInitScript((entries: Array<[string, string]>) => {
      for (const [key, value] of entries) sessionStorage.setItem(key, value);
    }, session);
    await stalePage.goto(page.url());
    const staleRow = stalePage
      .locator('.property-grid__row')
      .filter({ hasText: 'Rétention (jours)' });
    await expect(staleRow).toBeVisible();

    const resourceCell = retentionRow.locator('.property-grid__cell').first();
    const concurrentWriteResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname.endsWith('/properties'),
    );
    await resourceCell.locator('input').fill('45');
    await resourceCell.locator('input').press('Tab');
    expect((await concurrentWriteResponse).status()).toBe(200);

    const conflictWriteResponse = stalePage.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname.endsWith('/properties'),
    );
    const staleResourceCell = staleRow.locator('.property-grid__cell').first();
    await staleResourceCell.locator('input').fill('60');
    await staleResourceCell.locator('input').press('Tab');
    expect((await conflictWriteResponse).status()).toBe(409);
    const versionConflict = stalePage.getByRole('dialog', { name: 'Conflit de version' });
    await expect(versionConflict).toContainText('45');
    await expect(versionConflict).toContainText('60');
    await assertNoSeriousAccessibilityViolations(stalePage);
    await assertNoPageHorizontalOverflow(stalePage);
    await stalePage.screenshot({
      path: resolve(captureDirectory, `VersionConflict-${viewportWidth}.png`),
      fullPage: false,
    });
    const retryResponse = stalePage.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname.endsWith('/properties'),
    );
    await versionConflict.getByRole('button', { name: 'Reprendre' }).click();
    expect((await retryResponse).status()).toBe(200);
    const retriedRow = stalePage
      .locator('.property-grid__row')
      .filter({ hasText: 'Rétention (jours)' });
    await expect(
      retriedRow
        .locator('.property-grid__cell')
        .nth(headerLabels.indexOf('prd') - 1)
        .locator('input'),
    ).toHaveValue('60');
    await stalePage.close();

    await page.getByRole('link', { name: 'Retour au composant', exact: true }).click();
    await waitForViewTransition(page);
    await addResource(page, 'Application Insights', 'ApplicationInsights', 'main');
    await page.getByRole('link', { name: 'Retour au composant', exact: true }).click();
    await waitForViewTransition(page);
    await addResource(page, 'Coffre de clés', 'KeyVault', 'kv');
    await page.getByRole('link', { name: 'Retour au composant', exact: true }).click();
    await waitForViewTransition(page);
    await assertNoSeriousAccessibilityViolations(page);
    const appInsightsRow = page
      .locator('.component-detail__resources li')
      .filter({ hasText: 'ApplicationInsights' });
    await expect(appInsightsRow).toBeVisible();
    const appInsightsResourceHref = await appInsightsRow.locator('a').getAttribute('href');
    expect(appInsightsResourceHref).toBeTruthy();
    const appInsightsResourceId = appInsightsResourceHref!.split('/').at(-1)!;
    await appInsightsRow.getByRole('button', { name: 'Supprimer du modèle', exact: true }).click();
    const deletionDialog = page.getByRole('dialog', { name: 'Supprimer main du modèle ?' });
    await expect(deletionDialog.getByText('Analyse encore partielle')).toBeVisible();
    await expect(deletionDialog.locator(':scope > header')).toHaveCSS(
      'padding-left',
      viewportWidth <= 760 ? '16px' : '24px',
    );
    await expect(deletionDialog).toContainText('Liaisons entrantes');
    await expect(deletionDialog).toContainText('Paramètres d’application');
    await assertNoSeriousAccessibilityViolations(page);
    await deletionDialog.press('Escape');
    await expect(deletionDialog).toHaveCount(0);
    let deletionImpactRequests = 0;
    const countDeletionImpactRequests = async (route: Route): Promise<void> => {
      deletionImpactRequests += 1;
      await route.continue();
    };
    await page.route('**/delete-impact', countDeletionImpactRequests);
    await appInsightsRow.getByRole('button', { name: 'Supprimer du modèle', exact: true }).click();
    const reopenedDeletionDialog = page.getByRole('dialog', { name: 'Supprimer main du modèle ?' });
    await expect(reopenedDeletionDialog.getByText('Analyse encore partielle')).toBeVisible();
    await assertNoPageHorizontalOverflow(page);
    await page.screenshot({
      path: resolve(captureDirectory, `DeleteImpact-${viewportWidth}.png`),
      fullPage: false,
    });
    const initialImpactRequestCount = deletionImpactRequests;
    const deletionVersions: number[] = [];
    await page.route(
      (url) => url.pathname.endsWith(`/resources/${appInsightsResourceId}`),
      async (route) => {
        const request = route.request();
        if (request.method() !== 'DELETE') {
          await route.continue();
          return;
        }

        const body = request.postDataJSON() as { expectedVersion: number };
        deletionVersions.push(body.expectedVersion);
        if (deletionVersions.length === 1) {
          const authorization = await request.headerValue('authorization');
          const organizationId = await request.headerValue('x-ifs-organization');
          const recipeComponentId = new URL(componentUrl!, page.url()).pathname.split('/').at(-1)!;
          const updateResponse = await page.request.put(
            `${new URL(request.url()).origin}/v1/projects/${projectId}/components/${recipeComponentId}/resources/${appInsightsResourceId}/metadata`,
            {
              data: {
                description: 'Conflit concurrent de suppression J0-14',
                tags: {},
                expectedVersion: body.expectedVersion,
              },
              headers: {
                Authorization: authorization ?? '',
                'X-Ifs-Organization': organizationId ?? '',
                'Idempotency-Key': randomUUID().replaceAll('-', ''),
              },
            },
          );
          expect(updateResponse.status()).toBe(200);
        }

        await route.continue();
      },
    );
    await reopenedDeletionDialog.getByRole('textbox').fill('main');
    const deleteResponse = waitForResourceDelete(page);
    await reopenedDeletionDialog
      .getByRole('button', { name: 'Supprimer du modèle', exact: true })
      .click();
    expect((await deleteResponse).status()).toBe(409);
    await expect.poll(() => deletionImpactRequests).toBe(initialImpactRequestCount + 1);
    await expect(reopenedDeletionDialog.getByRole('textbox')).toHaveValue('');
    await reopenedDeletionDialog.getByRole('textbox').fill('main');
    const retryDeleteResponse = waitForResourceDelete(page);
    await reopenedDeletionDialog
      .getByRole('button', { name: 'Supprimer du modèle', exact: true })
      .click();
    expect((await retryDeleteResponse).status()).toBe(204);
    expect(deletionVersions).toHaveLength(2);
    expect(deletionVersions[1]).toBeGreaterThan(deletionVersions[0]);
    await expect(appInsightsRow).toHaveCount(0);

    await page.getByRole('link', { name: 'Ajouter la ressource', exact: true }).click();
    await page
      .getByRole('link', { name: 'Référencer une ressource existante', exact: true })
      .click();
    await waitForViewTransition(page);
    await page.locator('input[name="resourceTypeSearch"]').fill('vault');
    await page.getByRole('link', { name: /Coffre de clés/ }).click();
    await waitForViewTransition(page);
    await page.getByLabel('Nom logique').fill('wrong');
    await page.locator('select[name="resourceGroup"]').selectOption({ label: 'main' });
    const existingIdInputs = page.locator('input[name^="existing-"]');
    await expect(existingIdInputs).toHaveCount(2);
    for (const input of await existingIdInputs.all()) {
      await input.fill(WRONG_RESOURCE_ID);
    }
    const invalidIdResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname.endsWith('/existing-id'),
    );
    const addExistingButton = page
      .getByRole('button', { name: 'Référencer une ressource existante', exact: true })
      .last();
    await addExistingButton.click();
    expect((await invalidIdResponse).ok()).toBe(false);
    await expect(page.getByRole('alert')).toHaveText(
      'Le type Azure de l’identifiant ne correspond pas au type de ressource choisi.',
    );
    await expect(addExistingButton).toBeEnabled();
    await assertNoPageHorizontalOverflow(page);
    await page.screenshot({
      path: resolve(captureDirectory, `ResourceExistingInvalid-${viewportWidth}.png`),
      fullPage: false,
    });
    const validKeyVaultId =
      '/subscriptions/00000000-0000-4000-8000-000000000014/resourceGroups/rg-recipe/providers/Microsoft.KeyVault/vaults/recipe';
    for (const input of await existingIdInputs.all()) {
      await input.fill(validKeyVaultId);
    }
    const existingTargetIds = await existingIdInputs.evaluateAll((inputs) =>
      inputs.map((input) => input.getAttribute('name')!.replace(/^existing-/, '')),
    );
    const validExistingResponses = existingTargetIds.map((targetId) =>
      page.waitForResponse((response) => {
        if (
          response.request().method() !== 'PUT' ||
          !new URL(response.url()).pathname.endsWith('/existing-id')
        ) {
          return false;
        }
        return response.request().postData()?.includes(`"targetId":"${targetId}"`) ?? false;
      }),
    );
    await page
      .getByRole('button', { name: 'Référencer une ressource existante', exact: true })
      .last()
      .click();
    expect(
      (await Promise.all(validExistingResponses)).map((response) => response.status()),
    ).toEqual([200, 200]);
    await expect(page.locator('.resource-editor__existing')).toHaveCount(2);
    await expect(page.getByRole('link', { name: 'Propriétés', exact: true })).toHaveCount(0);
    await assertNoPageHorizontalOverflow(page);
    await page.locator('.resource-editor__existing').first().scrollIntoViewIfNeeded();
    await page.screenshot({
      path: resolve(captureDirectory, `ResourceExisting-${viewportWidth}.png`),
      fullPage: false,
    });
    const firstExistingResource = page.locator('.resource-editor__existing').first();
    const firstExistingId = firstExistingResource.locator('input');
    const updatedKeyVaultId = validKeyVaultId.replace(/\/recipe$/, '/recipe-dev-updated');
    await firstExistingId.fill(updatedKeyVaultId);
    const updateExistingIdResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname.endsWith('/existing-id'),
    );
    await firstExistingResource.getByRole('button', { name: 'Enregistrer', exact: true }).click();
    expect((await updateExistingIdResponse).status()).toBe(200);
    await expect(firstExistingId).toHaveValue(updatedKeyVaultId);
  } finally {
    try {
      if (componentUrl) {
        await removeRecipeComponent(page, componentUrl, componentsUrl);
      } else {
        await removeRecipeComponentByCode(page, componentsUrl, componentCode);
      }
    } finally {
      await removeRecipeEnvironments(page, environmentsUrl, createdEnvironmentIds);
    }
  }
});

function propertyCellForEnvironment(cells: Locator, headerLabels: string[], code: string) {
  const headerIndex = headerLabels.indexOf(code);
  expect(headerIndex).toBeGreaterThanOrEqual(2);
  return cells.nth(headerIndex - 1);
}

async function createRecipeComponent(
  page: Page,
  componentsUrl: string,
  name: string,
  code: string,
): Promise<string> {
  await page.goto(componentsUrl);
  await expect(page.locator('.project-components__list, .project-components__empty')).toHaveCount(
    1,
  );
  const existing = page.locator('.project-components__list a').filter({ hasText: code });
  if (await existing.count()) {
    const existingHref = await existing.first().getAttribute('href');
    if (!existingHref) throw new Error(`The component link for ${code} has no destination.`);
    await page.goto(new URL(existingHref, page.url()).toString());
    await removeRecipeComponent(page, page.url(), componentsUrl);
    await page.goto(componentsUrl);
  }
  await page.getByRole('link', { name: 'Créer un composant', exact: true }).click();
  await page.locator('input[name="componentName"]').fill(name);
  await page.locator('input[name="componentCode"]').fill(code);
  const createResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' &&
      new URL(response.url()).pathname.endsWith('/components'),
  );
  await page.getByRole('button', { name: 'Enregistrer', exact: true }).click();
  expect((await createResponse).status()).toBe(201);
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Ajouter un groupe de ressources', exact: true }).click();
  await page.locator('input[name="groupName"]').fill('main');
  const groupResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' &&
      new URL(response.url()).pathname.endsWith('/resource-groups'),
  );
  await page
    .locator('.component-detail__group-form')
    .getByRole('button', { name: 'Enregistrer', exact: true })
    .click();
  expect((await groupResponse).status()).toBe(201);
  return page.url();
}

async function ensureRecipeEnvironments(
  page: Page,
  environmentsUrl: string,
  componentCode: string,
  createdEnvironmentIds: string[],
): Promise<void> {
  await page.goto(environmentsUrl);
  await expect(page.getByRole('heading', { name: 'Environnements', exact: true })).toBeVisible();
  await waitForEnvironmentList(page);
  const marker = `Recette J0-14 ${componentCode}`;
  const existingCards = page.locator('.project-environments__card');
  const staleEnvironmentIds: string[] = [];
  for (const card of await existingCards.all()) {
    if (!(await card.getByText(marker, { exact: true }).count())) continue;
    const href = await card
      .getByRole('link', { name: 'Modifier', exact: true })
      .getAttribute('href');
    const environmentId = href?.split('/').pop();
    if (environmentId) staleEnvironmentIds.push(environmentId);
  }
  await removeRecipeEnvironments(page, environmentsUrl, staleEnvironmentIds);

  let codes = await page.locator('.project-environments__code').allTextContents();
  for (const code of ['dev', 'prd']) {
    if (codes.map((value) => value.trim()).includes(code)) continue;
    await createRecipeEnvironment(page, environmentsUrl, code, marker, createdEnvironmentIds);
    codes = await page.locator('.project-environments__code').allTextContents();
  }
}

async function createRecipeEnvironment(
  page: Page,
  environmentsUrl: string,
  code: string,
  marker: string,
  createdEnvironmentIds: string[],
): Promise<void> {
  await page.goto(environmentsUrl);
  await waitForEnvironmentList(page);
  await page.getByRole('button', { name: 'Ajouter un environnement', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Nouvel environnement', exact: true }),
  ).toBeVisible();
  await page
    .locator('input[name="environmentName"]')
    .fill(code === 'dev' ? 'Développement' : 'Production');
  await page.locator('input[name="environmentCode"]').fill(code);
  await page.locator('input[name="subscriptionId"]').fill('00000000-0000-4000-8000-000000000014');
  await page.locator('input[name="region"]').fill('francecentral');
  await page.locator('#environment-description').fill(marker);
  const response = page.waitForResponse(
    (candidate) =>
      candidate.request().method() === 'POST' &&
      new URL(candidate.url()).pathname.endsWith('/environments'),
  );
  await page.getByRole('button', { name: 'Enregistrer', exact: true }).click();
  const createResult = await response;
  expect(createResult.status()).toBe(201);
  const createdEnvironment = (await createResult.json()) as { id?: unknown };
  const environmentId =
    typeof createdEnvironment.id === 'string' ? createdEnvironment.id : undefined;
  if (!environmentId) throw new Error(`Created ${code} environment response has no id.`);
  createdEnvironmentIds.push(environmentId);
  await expect(page).toHaveURL(environmentsUrl);
  const card = page.locator('.project-environments__card').filter({
    has: page.locator('.project-environments__code').getByText(code, { exact: true }),
  });
  await expect(card).toBeVisible();
}

async function removeRecipeEnvironments(
  page: Page,
  environmentsUrl: string,
  environmentIds: string[],
): Promise<void> {
  for (const environmentId of [...environmentIds].reverse()) {
    await page.goto(environmentsUrl);
    await expect(page.getByRole('heading', { name: 'Environnements', exact: true })).toBeVisible();
    await waitForEnvironmentList(page);
    const environmentLink = page.locator(`a[href$="/${environmentId}"]`);
    if (!(await environmentLink.count())) continue;
    const card = environmentLink.locator(
      'xpath=ancestor::li[contains(@class,"project-environments__card")]',
    );
    await card.getByRole('button', { name: 'Supprimer', exact: true }).click();
    const dialog = page.locator('.project-environments__dialog');
    await expect(dialog).toBeVisible();
    await dialog.getByRole('button', { name: 'Supprimer', exact: true }).click();
    await expect(
      dialog.getByText(
        'Aucune ressource d’infrastructure n’est encore enregistrée pour cet environnement.',
        { exact: true },
      ),
    ).toBeVisible();
    const response = page.waitForResponse(
      (candidate) =>
        candidate.request().method() === 'DELETE' &&
        new URL(candidate.url()).pathname.endsWith(`/environments/${environmentId}`),
    );
    await dialog.getByRole('button', { name: 'Confirmer la suppression', exact: true }).click();
    expect((await response).status()).toBe(204);
    await expect(environmentLink).toHaveCount(0);
  }
}

async function waitForEnvironmentList(page: Page): Promise<void> {
  await expect(page.locator('.project-environments > [role="status"]')).toHaveCount(0);
  await expect(page.locator('.project-environments > [role="alert"]')).toHaveCount(0);
}

async function removeRecipeComponentByCode(
  page: Page,
  componentsUrl: string,
  code: string,
): Promise<void> {
  await page.goto(componentsUrl);
  await expect(page.locator('.project-components__list, .project-components__empty')).toHaveCount(
    1,
  );
  const link = page.locator('.project-components__list a').filter({ hasText: code });
  if (!(await link.count())) return;
  const href = await link.first().getAttribute('href');
  if (!href) throw new Error(`The component link for ${code} has no destination.`);
  await removeRecipeComponent(page, new URL(href, page.url()).toString(), componentsUrl);
}

async function addResource(
  page: Page,
  label: string,
  type: string,
  logicalName: string,
  captureDirectory?: string,
  viewportWidth?: number,
): Promise<void> {
  const actualViewportWidth = page.viewportSize()?.width ?? viewportWidth ?? 1440;
  await page.getByRole('link', { name: 'Ajouter la ressource', exact: true }).click();
  await expect(page.getByRole('heading', { name: /^Ajouter une ressource à / })).toBeVisible();
  await expect(page.getByRole('dialog')).toBeVisible();
  await waitForViewTransition(page);
  await expect(page.locator('.resource-type-picker')).toBeInViewport();
  await expect(page.locator('.resource-type-picker > header')).toHaveCSS(
    'padding-left',
    actualViewportWidth <= 760 ? '16px' : '24px',
  );
  if (captureDirectory && viewportWidth) {
    await page.screenshot({
      path: resolve(captureDirectory, `AddResourceType-${viewportWidth}.png`),
      fullPage: false,
    });
  }
  await page.locator('input[name="resourceTypeSearch"]').fill(label);
  await assertNoSeriousAccessibilityViolations(page);
  await page.getByRole('link', { name: new RegExp(label) }).click();
  await expect(page.getByRole('heading', { name: /^Nouvelle / })).toBeVisible();
  await expect(page.getByRole('dialog')).toBeVisible();
  await waitForViewTransition(page);
  await expect(page.locator('.resource-create')).toBeInViewport();
  await expect(page.locator('.resource-create > header')).toHaveCSS(
    'padding-left',
    actualViewportWidth <= 760 ? '16px' : '24px',
  );
  await page.getByLabel('Nom logique').fill(logicalName);
  await page.locator('select[name="resourceGroup"]').selectOption({ label: 'main' });
  await expect(page.locator('app-generated-names')).toContainText('dev');
  await expect(page.locator('app-generated-names')).toContainText('francecentral');
  if (captureDirectory && viewportWidth) {
    await assertNoPageHorizontalOverflow(page);
    await page.screenshot({
      path: resolve(captureDirectory, `AddResourceConfigure-${viewportWidth}.png`),
      fullPage: false,
    });
  }
  const addResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' &&
      new URL(response.url()).pathname.endsWith('/resources'),
  );
  await page.getByRole('button', { name: /^Ajouter / }).click();
  expect((await addResponse).status()).toBe(201);
  await expect(page.getByRole('heading', { name: logicalName, exact: true })).toBeVisible();
  await waitForViewTransition(page);
  await expect(page.locator('.resource-editor__type')).toContainText(label);
  await expect(page.locator('.resource-editor')).toBeInViewport();
  await expect(page.locator('app-generated-names')).toContainText('dev');
  if (captureDirectory && viewportWidth) {
    await expect(page.getByRole('switch', { name: 'Présence dev' })).toHaveAttribute(
      'aria-checked',
      'true',
    );
    await expect(page.locator('app-generated-names li')).toHaveCount(2);
    await page.screenshot({
      path: resolve(captureDirectory, `ResourceTabs-${viewportWidth}.png`),
      fullPage: false,
    });
    const description = page.getByLabel('Description');
    await description.fill('Workspace de recette J0-14');
    await page.locator('textarea[name="resourceTags"]').fill('team=platform');
    const metadataResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname.endsWith('/metadata'),
    );
    await page.getByRole('button', { name: 'Enregistrer', exact: true }).click();
    expect((await metadataResponse).status()).toBe(200);
    await expect(page.getByLabel('Description')).toHaveValue('Workspace de recette J0-14');
    const developmentPresence = page.getByRole('switch', { name: 'Présence dev' });
    const presenceResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname.endsWith('/presence'),
    );
    await developmentPresence.click();
    expect((await presenceResponse).status()).toBe(200);
    await expect(developmentPresence).toHaveAttribute('aria-checked', 'false');
    const restorePresenceResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname.endsWith('/presence'),
    );
    await developmentPresence.click();
    expect((await restorePresenceResponse).status()).toBe(200);
    await expect(developmentPresence).toHaveAttribute('aria-checked', 'true');
    await expect(page.locator('app-generated-names li')).toHaveCount(2);
    await assertNoPageHorizontalOverflow(page);
    await page.screenshot({
      path: resolve(captureDirectory, `ResourceGeneral-${viewportWidth}.png`),
      fullPage: false,
    });
    await page.getByRole('button', { name: 'Forcer un nom pour un environnement' }).click();
    const forcedNameForm = page
      .locator('.resource-editor__forced-name')
      .filter({ hasText: 'prd · francecentral' });
    await expect(forcedNameForm).toBeVisible();
    const forcedNameInput = forcedNameForm.locator('input');
    await forcedNameInput.fill('log-shop-main-prd-legacy');
    const forcedNameResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname.endsWith('/forced-name'),
    );
    await forcedNameForm.getByRole('button', { name: 'Enregistrer', exact: true }).click();
    expect((await forcedNameResponse).status()).toBe(200);
    await expect(forcedNameInput).toHaveValue('log-shop-main-prd-legacy');
    await expect(page.locator('app-generated-names')).toContainText('log-shop-main-prd-legacy');
    await assertNoPageHorizontalOverflow(page);
    await forcedNameForm.scrollIntoViewIfNeeded();
    await page.screenshot({
      path: resolve(captureDirectory, `ResourceGeneralForcedName-${viewportWidth}.png`),
      fullPage: false,
    });
  }
}

async function removeRecipeComponent(
  page: Page,
  componentUrl: string,
  componentsUrl: string,
): Promise<void> {
  await page.goto(componentUrl);
  await waitForComponentDetail(page);
  const resourceRows = page.locator('.component-detail__resources li');
  while (await resourceRows.count()) {
    const previousCount = await resourceRows.count();
    const row = resourceRows.first();
    const name = (await row.locator('strong').textContent())?.trim() ?? '';
    const impactResponse = page.waitForResponse(
      (candidate) =>
        candidate.request().method() === 'GET' &&
        new URL(candidate.url()).pathname.endsWith('/delete-impact'),
    );
    await row.getByRole('button', { name: 'Supprimer du modèle', exact: true }).click();
    const impactResult = await impactResponse;
    if (!impactResult.ok()) {
      const problem = (await impactResult.json().catch(() => null)) as {
        code?: unknown;
        title?: unknown;
        traceId?: unknown;
      } | null;
      const code =
        typeof problem?.code === 'string' && /^[A-Z0-9_-]{1,80}$/.test(problem.code)
          ? problem.code
          : 'UNKNOWN';
      const traceId =
        typeof problem?.traceId === 'string' && /^[0-9a-fA-F-]{1,64}$/.test(problem.traceId)
          ? `, trace ${problem.traceId}`
          : '';
      throw new Error(
        `Resource impact returned HTTP ${impactResult.status()} (${code}${traceId}).`,
      );
    }
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await dialog.getByRole('textbox').fill(name);
    const response = waitForResourceDelete(page);
    await dialog.getByRole('button', { name: 'Supprimer du modèle', exact: true }).click();
    expect((await response).status()).toBe(204);
    await waitForComponentDetail(page);
    await expect(resourceRows).toHaveCount(previousCount - 1);
  }
  await expect(resourceRows).toHaveCount(0);

  const groups = page.locator('.component-detail__groups li');
  while (await groups.count()) {
    const previousCount = await groups.count();
    const group = groups.first();
    await group.getByRole('button', { name: 'Supprimer', exact: true }).click();
    const response = page.waitForResponse(
      (candidate) =>
        candidate.request().method() === 'DELETE' &&
        new URL(candidate.url()).pathname.includes('/resource-groups/'),
    );
    await group.getByRole('button', { name: 'Supprimer', exact: true }).click();
    expect((await response).status()).toBe(204);
    await waitForComponentDetail(page);
    await expect(groups).toHaveCount(previousCount - 1);
  }
  await expect(groups).toHaveCount(0);
  await expect(resourceRows).toHaveCount(0);

  await page.getByRole('link', { name: 'Réglages', exact: true }).click();
  const deletion = page.locator('.component-editor__delete');
  await deletion.getByRole('button', { name: 'Supprimer le composant', exact: true }).click();
  const response = page.waitForResponse(
    (candidate) =>
      candidate.request().method() === 'DELETE' &&
      new URL(candidate.url()).pathname.endsWith(
        '/components/' + new URL(componentUrl).pathname.split('/').pop(),
      ),
  );
  await deletion.getByRole('button', { name: 'Supprimer le composant', exact: true }).click();
  const deleteResult = await response;
  if (deleteResult.status() !== 204) {
    const problem = (await deleteResult.json().catch(() => null)) as {
      code?: unknown;
      title?: unknown;
      traceId?: unknown;
    } | null;
    const code =
      typeof problem?.code === 'string' && /^[A-Z0-9_-]{1,80}$/.test(problem.code)
        ? problem.code
        : 'UNKNOWN';
    const title = typeof problem?.title === 'string' ? problem.title.slice(0, 100) : '';
    const traceId =
      typeof problem?.traceId === 'string' && /^[0-9a-fA-F-]{1,64}$/.test(problem.traceId)
        ? `, trace ${problem.traceId}`
        : '';
    throw new Error(
      `Component cleanup returned HTTP ${deleteResult.status()} (${code}${traceId}): ${title}`,
    );
  }
  await expect(page).toHaveURL(componentsUrl);
}

function waitForResourceDelete(page: Page) {
  return page.waitForResponse(
    (response) =>
      response.request().method() === 'DELETE' &&
      /\/components\/[^/]+\/resources\/[^/]+$/.test(new URL(response.url()).pathname),
  );
}

async function waitForComponentDetail(page: Page): Promise<void> {
  await expect(page.locator('#resource-groups-heading')).toBeVisible();
  await expect(page.locator('#resources-heading')).toBeVisible();
}

async function waitForViewTransition(page: Page): Promise<void> {
  await page.evaluate(async () => {
    const animations = document.getAnimations().filter((animation) => {
      const effect = animation.effect;
      return (
        effect instanceof KeyframeEffect && effect.pseudoElement?.startsWith('::view-transition')
      );
    });
    await Promise.all(animations.map((animation) => animation.finished.catch(() => undefined)));
  });
}

async function assertNoSeriousAccessibilityViolations(page: Page): Promise<void> {
  const { violations } = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
    .analyze();
  const serious = violations.filter(
    (violation) => violation.impact === 'serious' || violation.impact === 'critical',
  );
  expect(serious).toEqual([]);
}

async function assertNoPageHorizontalOverflow(page: Page): Promise<void> {
  const viewportWidth = page.viewportSize()?.width;
  expect(viewportWidth).toBeTruthy();
  const documentWidth = await page.evaluate(() => document.documentElement.scrollWidth);
  expect(documentWidth).toBeLessThanOrEqual(viewportWidth!);
}
