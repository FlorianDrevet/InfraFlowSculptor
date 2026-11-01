import AxeBuilder from '@axe-core/playwright';
import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { expect, test, type Page } from '@playwright/test';
import { loginAsAlice } from './support/login';

const CONTOSO_ID = '33333333-3333-4333-8333-333333333333';
const RECIPE_PROJECT_CODE = 'shop15';
const VAULT_NAME = 'kvj018';
const EXISTING_VAULT_ID =
  '/subscriptions/00000000-0000-4000-8000-000000000014/resourceGroups/rg-recipe/providers/Microsoft.KeyVault/vaults/kvj018';
const SETTING_NAME = 'Payments__ApiKey';
const PIPELINE_VARIABLE = 'MAIN_PAYMENTS_API_KEY';

test('Alice crée une liaison et un secret de pipeline, puis vérifie les implicites', async ({
  page,
}, testInfo) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo accounts.');
  test.setTimeout(180_000);
  page.setDefaultTimeout(15_000);
  page.setDefaultNavigationTimeout(30_000);

  await loginAsAlice(page);
  await page.goto(`/o/${CONTOSO_ID}/projects`);
  const projectCard = page.locator('.projects-page__card').filter({
    has: page.locator('.projects-page__code').getByText(RECIPE_PROJECT_CODE, { exact: true }),
  });
  await expect(projectCard).toBeVisible();
  const projectNavigation = page.waitForURL(/\/projects\/[0-9a-f-]+$/i);
  await projectCard.locator('.projects-page__project-name').click();
  await projectNavigation;

  const projectId = new URL(page.url()).pathname.match(/\/projects\/([^/]+)$/)?.[1];
  expect(projectId).toBeTruthy();
  const componentNavigation = page.waitForURL(/\/components\/[0-9a-f-]+$/i);
  await page.getByRole('link', { name: 'Orders orders', exact: true }).click();
  await componentNavigation;
  const componentUrl = page.url();
  await expect(page.getByRole('heading', { name: 'Orders', exact: true })).toBeVisible();
  await expect(
    page.getByRole('link', { name: `${VAULT_NAME} KeyVault`, exact: true }),
  ).toHaveCount(0);
  const componentId = new URL(componentUrl).pathname.match(/\/components\/([^/]+)$/)?.[1];
  expect(componentId).toBeTruthy();
  const resourceNavigation = page.waitForURL(/\/resources\/[0-9a-f-]+$/i);
  await page.getByRole('link', { name: 'api ContainerApp', exact: true }).click();
  await resourceNavigation;
  const resourceUrl = page.url();

  let vaultCreated = false;
  let settingCreated = false;
  let accessLinkCreated = false;
  let recipeFailed = false;
  let recipeFailure: unknown;
  const cleanupErrors: unknown[] = [];
  const captureDirectory = resolve(process.cwd(), 'e2e/__captures__/R-05');
  const viewportWidth = page.viewportSize()?.width ?? 1440;
  await mkdir(captureDirectory, { recursive: true });

  try {
    await page.getByRole('link', { name: 'Retour au composant', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Orders', exact: true })).toBeVisible();
    await addRecipeKeyVault(page, () => {
      vaultCreated = true;
    });

    await page.getByRole('link', { name: 'api ContainerApp', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'api', exact: true })).toBeVisible();
    await page.getByRole('link', { name: 'Paramètres applicatifs', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Paramètres applicatifs' })).toBeVisible();
    const settingsPanel = page.locator('.resource-app-settings');
    await expect(
      settingsPanel.locator('.resource-app-settings__list li').filter({ hasText: SETTING_NAME }),
    ).toHaveCount(0);
    await settingsPanel.getByRole('button', { name: 'Ajouter un paramètre', exact: true }).click();
    const settingsForm = settingsPanel.locator('.resource-app-settings__form');
    await settingsForm.locator('input[name="settingName"]').fill(SETTING_NAME);
    await settingsForm.locator('select[name="sourceKind"]').selectOption('KeyVaultSecret');
    const vaultOption = settingsForm
      .locator('select[name="keyVaultResourceId"] option')
      .filter({ hasText: VAULT_NAME });
    await expect(vaultOption).toHaveCount(1);
    const vaultId = await vaultOption.getAttribute('value');
    expect(vaultId).toBeTruthy();
    await settingsForm.locator('select[name="keyVaultResourceId"]').selectOption(vaultId);
    await settingsForm.locator('input[name="secretName"]').fill('payments-api-key');
    await settingsForm.locator('select[name="secretFeedKind"]').selectOption('PipelineSecret');
    await settingsForm.locator('input[name="pipelineVariableName"]').fill(PIPELINE_VARIABLE);
    const identityOption = settingsForm
      .locator('select[name="identityResourceId"] option')
      .filter({ hasText: 'api' });
    await expect(identityOption).toHaveCount(1);
    const identityId = await identityOption.getAttribute('value');
    expect(identityId).toBeTruthy();
    await settingsForm.locator('select[name="identityResourceId"]').selectOption(identityId);
    await expect(settingsForm.locator('input[name="literalValue"]')).toHaveCount(0);
    await expect(settingsForm).toContainText(
      'La valeur du secret ne sera ni demandée ni affichée ici.',
    );

    const settingResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname.endsWith('/app-settings'),
    );
    settingCreated = true;
    await settingsForm.getByRole('button', { name: 'Enregistrer', exact: true }).click();
    const settingResult = await settingResponse;
    expect(settingResult.status(), await settingResult.text()).toBe(201);

    const settingRow = settingsPanel
      .locator('.resource-app-settings__list li')
      .filter({ hasText: SETTING_NAME });
    await expect(settingRow).toContainText(PIPELINE_VARIABLE);
    await expect(settingRow.locator('input')).toHaveCount(0);
    await assertNoSeriousAccessibilityViolations(page);
    await assertNoHorizontalOverflow(page);
    await page.screenshot({
      path: resolve(captureDirectory, `ResourceAppSettings-${viewportWidth}.png`),
      fullPage: false,
    });

    await page.getByRole('link', { name: 'Liaisons', exact: true }).click();
    await expect(page).toHaveURL(/tab=links$/);
    await expect(page.getByRole('heading', { name: 'Liaisons sortantes' })).toBeVisible();
    const explicitAccess = page
      .locator('.resource-links__item')
      .filter({ hasText: 'Accès' })
      .filter({ hasText: 'core / main' })
      .filter({ hasText: 'Identité système' });
    await expect(explicitAccess).toHaveCount(0);
    const implicitSecretRead = page
      .locator('.resource-links__item--implicit')
      .filter({ hasText: 'Lecture de secret' });
    await expect(implicitSecretRead).toContainText(`orders / ${VAULT_NAME}`);
    await expect(implicitSecretRead).toContainText(`Paramètre applicatif : ${SETTING_NAME}`);
    await expect(
      page.getByRole('button', { name: 'Ajouter une liaison', exact: true }).first(),
    ).toBeEnabled();

    await page.getByRole('button', { name: 'Ajouter une liaison', exact: true }).first().click();
    const dialog = page.getByRole('dialog', { name: /Ajouter une liaison/ });
    await expect(dialog).toBeVisible();
    const selects = dialog.locator('.resource-links__form select');
    await expect(dialog.getByRole('button', { name: 'Accès', exact: true })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    await dialog.getByRole('button', { name: 'Accès', exact: true }).click();
    await expect(dialog.getByRole('button', { name: 'Accès', exact: true })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    const accessTargets = await selects.nth(0).locator('option').allTextContents();
    const workspaceOption = selects.nth(0).locator('option', { hasText: 'core / main' });
    await expect(workspaceOption).toHaveCount(1);
    expect(accessTargets, 'Cibles proposées pour la liaison Accès').toContain(
      (await workspaceOption.textContent())?.trim() ?? '',
    );
    const workspaceId = await workspaceOption.getAttribute('value');
    expect(workspaceId).toBeTruthy();
    await selects.nth(0).selectOption(workspaceId);
    await expect(selects).toHaveCount(2);

    const identitySelect = selects.nth(1);
    const identityOptions = await identitySelect.locator('option').allTextContents();
    expect(identityOptions.map((option) => option.trim())).toContain('Identité système');
    await identitySelect.selectOption({ label: 'Identité système' });
    for (const roleChoice of await dialog
      .locator('.resource-links__choices input[type="checkbox"]')
      .all()) {
      if (await roleChoice.isChecked()) await roleChoice.uncheck();
    }
    await dialog.getByLabel('Log Analytics Reader', { exact: true }).check();
    await dialog.getByRole('button', { name: 'Prévisualiser', exact: true }).click();

    const preview = dialog.locator('.resource-links__preview');
    await expect(preview).toContainText('dev : Log Analytics Reader → api → main');
    await expect(preview).toContainText('prd : Log Analytics Reader → api → main');
    await assertNoSeriousAccessibilityViolations(page);
    await assertNoHorizontalOverflow(page);
    await page.screenshot({
      path: resolve(captureDirectory, `ResourceLinks-AddLink-${viewportWidth}.png`),
      fullPage: false,
    });

    const createResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname.endsWith('/links'),
    );
    accessLinkCreated = true;
    await dialog.getByRole('button', { name: 'Ajouter une liaison', exact: true }).click();
    const createResult = await createResponse;
    expect(createResult.status()).toBe(201);

    await expect(explicitAccess).toContainText('Log Analytics Reader');
    await expect(explicitAccess).toContainText('Origine : liaison explicite');
    await expect(implicitSecretRead).toContainText(`Paramètre applicatif : ${SETTING_NAME}`);
    await assertNoSeriousAccessibilityViolations(page);
    await assertNoHorizontalOverflow(page);
    await page.screenshot({
      path: resolve(captureDirectory, `ResourceLinks-${viewportWidth}.png`),
      fullPage: false,
    });

    await page.getByRole('link', { name: 'Identité et accès', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Identité système' })).toBeVisible();
    const heldRoles = page
      .locator('.resource-identity__section')
      .filter({ hasText: 'Rôles détenus' });
    const usedIdentities = page.locator('.resource-identity__used-identities');
    await expect(usedIdentities).toContainText('api');
    await expect(usedIdentities).toContainText('core / main');
    await expect(heldRoles).toContainText('Log Analytics Reader');
    await expect(heldRoles).toContainText('Origine : liaison explicite');
    await expect(heldRoles).toContainText('Key Vault Secrets User');
    await expect(heldRoles).toContainText(`Paramètre applicatif : ${SETTING_NAME}`);
    await expect(page.locator('.resource-identity')).toContainText('api');
    await assertNoSeriousAccessibilityViolations(page);
    await assertNoHorizontalOverflow(page);
    await page.screenshot({
      path: resolve(captureDirectory, `ResourceIdentity-${viewportWidth}.png`),
      fullPage: false,
    });
  } catch (error) {
    recipeFailed = true;
    recipeFailure = error;
  } finally {
    for (const cleanup of [
      async () => {
        if (accessLinkCreated) await removeAccessLink(page, resourceUrl, 'Identité système');
      },
      async () => {
        if (settingCreated) await removeRecipeAppSetting(page, resourceUrl);
      },
      async () => {
        if (vaultCreated) await removeRecipeVault(page, componentUrl);
      },
    ]) {
      try {
        await cleanup();
      } catch (error) {
        cleanupErrors.push(error);
      }
    }
  }

  if (cleanupErrors.length > 0) {
    testInfo.annotations.push({
      type: 'cleanup-failure',
      description: cleanupErrors
        .map((error) => (error instanceof Error ? error.message : String(error)))
        .join('\n'),
    });
  }
  if (recipeFailed && cleanupErrors.length > 0) {
    throw new AggregateError(
      [recipeFailure, ...cleanupErrors],
      'The recipe failed and its test-created resources could not all be cleaned up.',
    );
  }
  if (recipeFailed) throw recipeFailure;
  if (cleanupErrors.length > 0) {
    throw new AggregateError(cleanupErrors, 'Every test-created resource should be cleaned up.');
  }
});

async function addRecipeKeyVault(page: Page, onCreated: () => void): Promise<void> {
  await page.getByRole('link', { name: 'Ajouter la ressource', exact: true }).click();
  await expect(page.getByRole('heading', { name: /^Ajouter une ressource à / })).toBeVisible();
  await page.getByRole('link', { name: 'Référencer une ressource existante', exact: true }).click();
  await page.locator('input[name="resourceTypeSearch"]').fill('vault');
  await page.getByRole('link', { name: /Coffre de clés/ }).click();
  await expect(page.getByRole('heading', { name: /^Nouvelle / })).toBeVisible();
  await page.getByLabel('Nom logique').fill(VAULT_NAME);
  await page.locator('select[name="resourceGroup"]').selectOption({ label: 'main' });
  const existingIdInputs = page.locator('input[name^="existing-"]');
  await expect(existingIdInputs).toHaveCount(2);
  for (const input of await existingIdInputs.all()) await input.fill(EXISTING_VAULT_ID);
  const targetIds = await existingIdInputs.evaluateAll((inputs) =>
    inputs.map((input) => input.getAttribute('name')!.replace(/^existing-/, '')),
  );
  const idResponses = targetIds.map((targetId) =>
    page.waitForResponse((response) => {
      if (
        response.request().method() !== 'PUT' ||
        !new URL(response.url()).pathname.endsWith('/existing-id')
      ) {
        return false;
      }
      try {
        return hasTargetId(response.request().postDataJSON(), targetId);
      } catch {
        return false;
      }
    }),
  );
  const createResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' &&
      new URL(response.url()).pathname.endsWith('/resources'),
  );
  onCreated();
  await page
    .getByRole('button', { name: 'Référencer une ressource existante', exact: true })
    .last()
    .click();
  const createResult = await createResponse;
  expect(createResult.status()).toBe(201);
  expect((await Promise.all(idResponses)).map((response) => response.status())).toEqual([200, 200]);
  await expect(page.getByRole('heading', { name: VAULT_NAME, exact: true })).toBeVisible();
  await expect(page.locator('.resource-editor__type')).toContainText('Coffre de clés');
  await expect(page.locator('.resource-editor__existing')).toHaveCount(2);
  await page.getByRole('link', { name: 'Retour au composant', exact: true }).click();
  await expect(page.locator('.component-detail__resources')).toContainText(VAULT_NAME);
  await expect(
    page.getByRole('link', { name: `${VAULT_NAME} KeyVault`, exact: true }),
  ).toBeVisible();
}

async function removeAccessLink(
  page: Page,
  resourceUrl: string,
  identityLabel: string,
): Promise<void> {
  await page.goto(`${resourceUrl}?tab=links`);
  await expect(page.getByRole('heading', { name: 'Liaisons sortantes' })).toBeVisible();
  await expect(page.locator('.resource-links__empty, .resource-links__list').first()).toBeVisible();
  const explicitAccess = page
    .locator('.resource-links__item')
    .filter({ hasText: 'Accès' })
    .filter({ hasText: 'core / main' })
    .filter({ hasText: identityLabel });
  if ((await explicitAccess.count()) === 0) return;
  await expect(explicitAccess).toBeVisible();
  await explicitAccess.getByRole('button', { name: 'Retirer', exact: true }).click();
  const removalDialog = page.getByRole('dialog', { name: 'Retirer', exact: true });
  await expect(removalDialog).toBeVisible();
  const removalResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'DELETE' &&
      /\/links\/[0-9a-f-]+$/i.test(new URL(response.url()).pathname),
  );
  await removalDialog.getByRole('button', { name: 'Retirer la liaison', exact: true }).click();
  expect((await removalResponse).status()).toBe(200);
  await expect(explicitAccess).toHaveCount(0);
}

async function removeRecipeAppSetting(page: Page, resourceUrl: string): Promise<void> {
  await page.goto(`${resourceUrl}?tab=settings`);
  await expect(page.getByRole('heading', { name: 'Paramètres applicatifs' })).toBeVisible();
  await expect(
    page.locator('.resource-app-settings__list, .resource-app-settings__empty, .resource-app-settings__error'),
  ).toBeVisible();
  const settingRow = page
    .locator('.resource-app-settings__list li')
    .filter({ hasText: SETTING_NAME });
  if ((await settingRow.count()) === 0) return;
  await expect(settingRow).toBeVisible();
  const deleteResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'DELETE' &&
      new URL(response.url()).pathname.includes('/app-settings/'),
  );
  await settingRow.getByRole('button', { name: 'Supprimer', exact: true }).click();
  expect((await deleteResponse).status()).toBe(204);
  await expect(settingRow).toHaveCount(0);
}

async function removeRecipeVault(page: Page, componentUrl: string): Promise<void> {
  await page.goto(componentUrl);
  await expect(page.getByRole('heading', { name: 'Orders', exact: true })).toBeVisible();
  const resourceRow = page
    .locator('.component-detail__resources li')
    .filter({ hasText: VAULT_NAME });
  if ((await resourceRow.count()) === 0) return;
  await expect(resourceRow).toBeVisible();
  const impactResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'GET' &&
      new URL(response.url()).pathname.endsWith('/delete-impact'),
  );
  await resourceRow.getByRole('button', { name: 'Supprimer du modèle', exact: true }).click();
  expect((await impactResponse).status()).toBe(200);
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await dialog.getByRole('textbox').fill(VAULT_NAME);
  const deleteResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'DELETE' &&
      new URL(response.url()).pathname.includes('/resources/'),
  );
  await dialog.getByRole('button', { name: 'Supprimer du modèle', exact: true }).click();
  const deleteResult = await deleteResponse;
  expect(deleteResult.status(), await deleteResult.text()).toBe(204);
  await expect(resourceRow).toHaveCount(0);
}

async function assertNoSeriousAccessibilityViolations(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  expect(
    results.violations.filter(
      (violation) => violation.impact === 'serious' || violation.impact === 'critical',
    ),
  ).toEqual([]);
}

async function assertNoHorizontalOverflow(page: Page): Promise<void> {
  const dimensions = await page.evaluate(() => ({
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
  }));
  expect(dimensions.scrollWidth).toBeLessThanOrEqual(dimensions.clientWidth + 1);
}

function hasTargetId(body: unknown, targetId: string): boolean {
  return (
    typeof body === 'object' &&
    body !== null &&
    'targetId' in body &&
    typeof body.targetId === 'string' &&
    body.targetId === targetId
  );
}
