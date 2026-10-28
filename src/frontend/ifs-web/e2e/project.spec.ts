import AxeBuilder from '@axe-core/playwright';
import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { expect, test, type Locator, type Page } from '@playwright/test';
import { loginAsAlice, loginAsUser } from './support/login';

const CONTOSO_ID = '33333333-3333-4333-8333-333333333333';
const PROJECT_NAME_DESKTOP = 'Recette écrans J0-09 bureau';
const PROJECT_NAME_MOBILE = 'Recette écrans J0-09 mobile';
const PROJECT_CODE_DESKTOP = 'j009recipe';
const PROJECT_CODE_MOBILE = 'j009mobi';
const SUBSCRIPTION_ID = '00000000-0000-4000-8000-000000000001';

test.afterEach(async ({ page }, testInfo) => {
  if (testInfo.status === 'skipped') {
    return;
  }

  page.setDefaultTimeout(15_000);
  page.setDefaultNavigationTimeout(30_000);
  await normalizeRecipeProject(page, recipeProjectCode(page), recipeProjectName(page));
});

test('Alice parcourt et gère les écrans du projet', async ({ page, browser }) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo accounts.');
  test.setTimeout(180_000);
  page.setDefaultTimeout(15_000);
  page.setDefaultNavigationTimeout(30_000);

  await loginAsAlice(page);
  await selectFrench(page);
  const projectCode = recipeProjectCode(page);
  const projectName = recipeProjectName(page);
  await page.goto(`/o/${CONTOSO_ID}/projects`);
  await expect(page.getByRole('heading', { name: 'Projets', exact: true })).toBeVisible();
  const shopCard = page.locator('.projects-page__card').filter({ hasText: 'shop' });
  await expect(shopCard.getByRole('button', { name: 'shop', exact: true })).toBeVisible();
  await expect(shopCard.locator('.projects-page__metadata dd').first()).toHaveText(/\d+/);
  await assertNoSeriousAccessibilityViolations(page);

  const screenshotDirectory = resolve(process.cwd(), '../../../docs/plan/revues/captures/R-05');
  await mkdir(screenshotDirectory, { recursive: true });
  const viewportWidth = page.viewportSize()?.width ?? 1440;
  await page.screenshot({
    path: resolve(screenshotDirectory, `Projects-${viewportWidth}.png`),
    fullPage: true,
  });

  await openOrCreateRecipeProject(page, projectCode, projectName);
  const projectId = new URL(page.url()).pathname.match(/\/projects\/([^/]+)/)?.[1];
  expect(projectId).toBeTruthy();
  await normalizeRecipeProject(page, projectCode, projectName);
  await page.goto(`/o/${CONTOSO_ID}/projects/${projectId}`);
  await expect(page.getByRole('heading', { name: projectName, exact: true })).toBeVisible();
  await page.goto(`/o/${CONTOSO_ID}/projects`);
  const recipeCard = page.locator('.projects-page__card').filter({ hasText: projectCode });
  const aliceFavoriteButton = recipeCard.getByRole('button', {
    name: /Ajouter aux favoris|Retirer des favoris/,
  });
  const aliceOriginallyFavorited =
    (await aliceFavoriteButton.getAttribute('aria-pressed')) === 'true';
  if (!aliceOriginallyFavorited) {
    await aliceFavoriteButton.click();
  }
  await expect(aliceFavoriteButton).toHaveAttribute('aria-pressed', 'true');
  await page.getByRole('button', { name: /^Favoris · \d+$/ }).click();
  await expect(recipeCard).toBeVisible();
  await assertNoSeriousAccessibilityViolations(page);
  await page.screenshot({
    path: resolve(screenshotDirectory, `Projects-Favorites-${viewportWidth}.png`),
    fullPage: true,
  });
  await recipeCard.getByRole('button', { name: projectName, exact: true }).click();
  await assertNoSeriousAccessibilityViolations(page);
  await page.screenshot({
    path: resolve(screenshotDirectory, `ProjectOverview-${viewportWidth}.png`),
    fullPage: true,
  });

  await clickProjectNavigationLink(page, 'Environnements');
  await expect(page.getByRole('heading', { name: 'Environnements', exact: true })).toBeVisible();
  await assertNoSeriousAccessibilityViolations(page);
  await page.screenshot({
    path: resolve(screenshotDirectory, `ProjectEnvironments-${viewportWidth}.png`),
    fullPage: true,
  });
  const productionCard = page.locator('.project-environments__card').filter({ hasText: 'prd' });
  await expect(productionCard).toBeVisible();
  const environmentNavigation = page.waitForURL(/\/environments\/[^/]+$/);
  await productionCard.getByRole('link', { name: 'Modifier', exact: true }).click();
  await environmentNavigation;
  const environmentUrl = page.url();
  const environmentId = new URL(environmentUrl).pathname.match(/\/environments\/([^/]+)$/)?.[1];
  expect(environmentId).toBeTruthy();
  await expect(page.locator('#environment-description')).toBeVisible();
  await assertNoSeriousAccessibilityViolations(page);
  await page.screenshot({
    path: resolve(screenshotDirectory, `EnvironmentEdit-${viewportWidth}.png`),
    fullPage: true,
  });
  const originalDescription = await page.locator('#environment-description').inputValue();
  const firstDescription = `Première saisie ${Date.now()}`;
  const staleDescription = `Saisie obsolète ${Date.now()}`;

  const secondPagePromise = page.waitForEvent('popup');
  await page.evaluate((url) => window.open(url, '_blank'), environmentUrl);
  const secondPage = await secondPagePromise;
  secondPage.setDefaultTimeout(15_000);
  try {
    await secondPage.goto(environmentUrl);
    await expect(secondPage.locator('#environment-description')).toBeVisible();

    await page.locator('#environment-description').fill(firstDescription);
    const firstSaveRequest = waitForEnvironmentUpdateRequest(page, projectId!, environmentId!);
    const firstSave = waitForEnvironmentUpdate(page, projectId!, environmentId!);
    await page.getByRole('button', { name: 'Enregistrer', exact: true }).click();
    await firstSaveRequest;
    expect((await firstSave).status()).toBe(200);
    await expect(page).toHaveURL(/\/environments$/);

    await secondPage.locator('#environment-description').fill(staleDescription);
    await secondPage.getByRole('button', { name: 'Enregistrer', exact: true }).click();
    const conflict = secondPage.getByRole('alert');
    await expect(conflict.getByRole('heading', { name: 'Le projet a changé' })).toBeVisible();
    await expect(conflict.getByRole('heading', { name: 'Version enregistrée' })).toBeVisible();
    await expect(conflict.getByRole('heading', { name: 'Votre saisie' })).toBeVisible();
    await expect(conflict).toContainText(firstDescription);
    await expect(conflict).toContainText(staleDescription);
  } finally {
    await secondPage.close();
  }

  await page.goto(environmentUrl);
  await expect(page.locator('#environment-description')).toHaveValue(firstDescription);
  await page.locator('#environment-description').fill(originalDescription);
  const restoreDescription = waitForEnvironmentUpdate(page, projectId!, environmentId!);
  await page.getByRole('button', { name: 'Enregistrer', exact: true }).click();
  expect((await restoreDescription).status()).toBe(200);

  await clickProjectNavigationLink(page, 'Membres');
  await expect(page.getByRole('heading', { name: 'Membres du projet', exact: true })).toBeVisible();
  const bobRow = page.getByRole('row').filter({ hasText: 'Bob Bernard' });
  const chloeRow = page.getByRole('row').filter({ hasText: 'chloe@contoso.example' });
  await setProjectRoles(bobRow, ['Contributeur']);
  await setProjectRoles(chloeRow, ['Lecteur']);
  await expect(bobRow).toContainText('Contributeur');
  await expect(chloeRow).toContainText('Lecteur');
  await assertNoSeriousAccessibilityViolations(page);
  await page.screenshot({
    path: resolve(screenshotDirectory, `ProjectMembers-${viewportWidth}.png`),
    fullPage: true,
  });

  const chloeContext = await browser.newContext({
    baseURL: process.env['IFS_E2E_BASE_URL'] ?? 'http://localhost:4200',
    viewport: page.viewportSize() ?? undefined,
    ignoreHTTPSErrors: true,
    locale: 'fr-FR',
  });
  const chloePage = await chloeContext.newPage();
  chloePage.setDefaultTimeout(15_000);
  chloePage.setDefaultNavigationTimeout(30_000);
  try {
    await loginAsUser(chloePage, 'chloe@contoso.example');
    await selectFrench(chloePage);
    await chloePage.goto(`/o/${CONTOSO_ID}/projects`);
    const chloeRecipeCard = chloePage
      .locator('.projects-page__card')
      .filter({ hasText: projectCode });
    await expect(
      chloeRecipeCard.getByRole('button', { name: 'Ajouter aux favoris', exact: true }),
    ).toHaveAttribute('aria-pressed', 'false');
    await chloePage.goto(`/o/${CONTOSO_ID}/projects/${projectId}/members`);
    await expect(
      chloePage.getByRole('heading', { name: 'Membres du projet', exact: true }),
    ).toBeVisible();
    const chloeMemberRow = chloePage.getByRole('row').filter({ hasText: 'chloe@contoso.example' });
    await expect(chloeMemberRow).toBeVisible();
    await expect(
      chloePage.getByRole('button', { name: 'Modifier les rôles', exact: true }),
    ).toHaveCount(0);
    await expect(chloePage.getByRole('button', { name: 'Enregistrer', exact: true })).toHaveCount(
      0,
    );
  } finally {
    await chloeContext.close();
  }

  let deletionMayHaveCompleted = false;
  let restored = false;
  try {
    await clickProjectNavigationLink(page, 'Paramètres');
    await expect(
      page.getByRole('heading', { name: 'Paramètres du projet', exact: true }),
    ).toBeVisible();
    await assertNoSeriousAccessibilityViolations(page);
    await page.screenshot({
      path: resolve(screenshotDirectory, `ProjectSettings-${viewportWidth}.png`),
      fullPage: true,
    });
    const projectNameInput = page.locator('input[name="projectName"]');
    const originalProjectName = await projectNameInput.inputValue();
    const updatedProjectName = `Projet J0-09 ${Date.now()}`;
    const projectDescription = page.locator('#project-description');
    const originalProjectDescription = await projectDescription.inputValue();
    const updatedProjectDescription = `Description J0-09 ${Date.now()}`;
    await projectNameInput.fill(updatedProjectName);
    await projectDescription.fill(updatedProjectDescription);
    const projectUpdate = await saveProjectSettings(page, projectId!);
    expect(projectUpdate.status()).toBe(200);
    await expect(page.locator('.project-workspace__header h1')).toHaveText(updatedProjectName);
    await expect(projectNameInput).toHaveValue(updatedProjectName);
    await expect(projectDescription).toHaveValue(updatedProjectDescription);

    await projectNameInput.fill(originalProjectName);
    await projectDescription.fill(originalProjectDescription);
    const projectRestore = await saveProjectSettings(page, projectId!);
    expect(projectRestore.status()).toBe(200);
    await expect(page.locator('.project-workspace__header h1')).toHaveText(originalProjectName);
    await expect(projectDescription).toHaveValue(originalProjectDescription);

    await page.getByRole('button', { name: 'Supprimer le projet', exact: true }).click();
    const deleteDialog = page.getByRole('dialog', { name: 'Supprimer le projet' });
    await deleteDialog.getByLabel('Saisissez le nom exact du projet').fill(originalProjectName);
    deletionMayHaveCompleted = true;
    await deleteDialog.getByRole('button', { name: 'Supprimer le projet', exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`/o/${CONTOSO_ID}/projects$`));
    await expect(page.locator('.projects-page__card').filter({ hasText: projectCode })).toHaveCount(
      0,
    );

    await page.getByRole('link', { name: 'Voir les projets supprimés' }).click();
    const deletedCard = page.locator('.projects-page__card').filter({ hasText: projectCode });
    await expect(deletedCard).toBeVisible();
    await deletedCard.getByRole('button', { name: 'Restaurer', exact: true }).click();
    await expect(deletedCard).toHaveCount(0);
    restored = true;

    await page.goto(`/o/${CONTOSO_ID}/projects`);
    await expect(
      page.locator('.projects-page__card').filter({ hasText: projectCode }),
    ).toBeVisible();
  } finally {
    if (deletionMayHaveCompleted && !restored) {
      await page.goto(`/o/${CONTOSO_ID}/projects/deleted`);
      const deletedCard = page.locator('.projects-page__card').filter({ hasText: projectCode });
      if (await deletedCard.count()) {
        await deletedCard.getByRole('button', { name: 'Restaurer', exact: true }).click();
      }
    }
  }
});

function recipeProjectCode(page: Page): string {
  return (page.viewportSize()?.width ?? 1440) <= 760 ? PROJECT_CODE_MOBILE : PROJECT_CODE_DESKTOP;
}

function recipeProjectName(page: Page): string {
  return (page.viewportSize()?.width ?? 1440) <= 760 ? PROJECT_NAME_MOBILE : PROJECT_NAME_DESKTOP;
}

async function openOrCreateRecipeProject(
  page: Page,
  projectCode: string,
  projectName: string,
): Promise<void> {
  let projectCard = page.locator('.projects-page__card').filter({ hasText: projectCode });
  if (!(await projectCard.count())) {
    await page.getByRole('link', { name: 'Voir les projets supprimés' }).click();
    await waitForProjectList(page);
    const deletedCard = page.locator('.projects-page__card').filter({ hasText: projectCode });
    if (await deletedCard.count()) {
      await deletedCard.getByRole('button', { name: 'Restaurer', exact: true }).click();
    } else {
      await createRecipeProject(page, projectCode, projectName);
      await page.waitForURL(/\/projects\/[^/]+$/);
      return;
    }
    await page.goto(`/o/${CONTOSO_ID}/projects`);
    await waitForProjectList(page);
    projectCard = page.locator('.projects-page__card').filter({ hasText: projectCode });
  }

  const projectNavigation = page.waitForURL(/\/projects\/[^/]+$/);
  await projectCard.locator('.projects-page__project-name').click();
  await projectNavigation;
}

async function waitForProjectList(page: Page): Promise<void> {
  await page.locator('.projects-page__list, .projects-page__empty').waitFor({ state: 'visible' });
}

async function clickProjectNavigationLink(page: Page, label: string): Promise<void> {
  if ((page.viewportSize()?.width ?? 1440) <= 760) {
    await page.getByRole('button', { name: /Menu/ }).click();
  }

  await page
    .getByRole('navigation', { name: 'Navigation principale' })
    .locator('a[href*="/projects/"]')
    .filter({ hasText: label })
    .click();

  if ((page.viewportSize()?.width ?? 1440) <= 760) {
    await page.keyboard.press('Escape');
  }
}

async function createRecipeProject(
  page: Page,
  projectCode: string,
  projectName: string,
): Promise<void> {
  await page.goto(`/o/${CONTOSO_ID}/projects/new`);
  await page.getByRole('button', { name: /Identité/ }).click();
  await expect(page.getByRole('heading', { name: 'Identité du projet' })).toBeVisible();
  await page.getByLabel('Nom', { exact: true }).fill(projectName);
  await page.getByLabel('Code', { exact: true }).fill(projectCode);
  await page.getByRole('button', { name: 'Continuer : outils' }).click();
  await page.getByRole('button', { name: 'Continuer : environnements' }).click();

  const environments = page.locator('.wizard-environment');
  await expect(environments.first()).toBeVisible();
  while ((await environments.count()) > 2) {
    const previousCount = await environments.count();
    await environments
      .last()
      .getByRole('button', { name: /Supprimer l’environnement/ })
      .click();
    await expect(environments).toHaveCount(previousCount - 1);
  }
  while ((await environments.count()) < 2) {
    const previousCount = await environments.count();
    await page.getByRole('button', { name: 'Ajouter un environnement' }).click();
    await expect(environments).toHaveCount(previousCount + 1);
  }
  await expect(environments).toHaveCount(2);
  await page.locator('input[name="environmentName0"]').fill('Développement');
  await page.locator('input[name="environmentCode0"]').fill('dev');
  await page.locator('select[name="environmentRegion0"]').selectOption('francecentral');
  await page.locator('input[name="environmentSubscription0"]').fill(SUBSCRIPTION_ID);
  await page.locator('input[name="environmentName1"]').fill('Production');
  await page.locator('input[name="environmentCode1"]').fill('prd');
  await page.locator('select[name="environmentRegion1"]').selectOption('francecentral');
  await page.locator('input[name="environmentSubscription1"]').fill(SUBSCRIPTION_ID);
  const protectedToggle = environments
    .nth(1)
    .getByRole('switch', { name: 'Protégé : chaque déploiement exige une approbation' });
  if ((await protectedToggle.getAttribute('aria-checked')) !== 'true') {
    await protectedToggle.click();
  }
  await page.getByLabel('Approbateurs', { exact: true }).fill('Shop Release Approvers');

  await page.getByRole('button', { name: 'Continuer : nommage' }).click();
  await expect(page.getByRole('heading', { name: 'Convention de nommage' })).toBeVisible();
  await page.getByRole('button', { name: 'Continuer : publication' }).click();
  await page.getByRole('button', { name: 'Passer', exact: true }).click();
  await page.getByRole('button', { name: 'Créer le projet', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Projet créé' })).toBeVisible();
  const projectNavigation = page.waitForURL(/\/projects\/[^/]+$/);
  await page.getByRole('button', { name: 'Ouvrir le projet', exact: true }).click();
  await projectNavigation;
}

async function setProjectRoles(row: Locator, targetLabels: string[]): Promise<void> {
  await row.getByRole('button', { name: 'Modifier les rôles', exact: true }).click();
  const checkboxes = row.getByRole('checkbox');
  await expect(checkboxes.first()).toBeVisible();
  for (const checkbox of await checkboxes.all()) {
    const label =
      (await checkbox.evaluate((element) => element.closest('label')?.textContent?.trim())) ?? '';
    const shouldBeChecked = targetLabels.includes(label);
    if (shouldBeChecked && !(await checkbox.isChecked())) {
      await checkbox.check();
    } else if (!shouldBeChecked && (await checkbox.isChecked())) {
      await checkbox.uncheck();
    }
  }
  await row.getByRole('button', { name: 'Enregistrer', exact: true }).click();
  await expect(row.getByRole('button', { name: 'Enregistrer', exact: true })).toHaveCount(0);
  for (const targetLabel of targetLabels) {
    await expect(row).toContainText(targetLabel);
  }
  if (targetLabels.length === 0) {
    await expect(row.locator('.project-members__roles li')).toHaveCount(0);
  }
}

async function normalizeRecipeProject(
  page: Page,
  projectCode: string,
  desiredProjectName: string,
): Promise<void> {
  if (
    !(await page
      .locator('.shell')
      .isVisible()
      .catch(() => false))
  ) {
    await loginAsAlice(page);
  }
  await selectFrench(page);
  await page.goto(`/o/${CONTOSO_ID}/projects`);

  let recipeCard = page.locator('.projects-page__card').filter({ hasText: projectCode });
  if ((await recipeCard.count()) === 0) {
    await page.goto(`/o/${CONTOSO_ID}/projects/deleted`);
    const deletedCard = page.locator('.projects-page__card').filter({ hasText: projectCode });
    if ((await deletedCard.count()) === 0) {
      return;
    }
    await deletedCard.getByRole('button', { name: 'Restaurer', exact: true }).click();
    await page.goto(`/o/${CONTOSO_ID}/projects`);
    recipeCard = page.locator('.projects-page__card').filter({ hasText: projectCode });
    await expect(recipeCard).toBeVisible();
  }

  const favoriteButton = recipeCard.getByRole('button', {
    name: /Ajouter aux favoris|Retirer des favoris/,
  });
  if ((await favoriteButton.getAttribute('aria-pressed')) === 'true') {
    await favoriteButton.click();
  }
  await expect(
    recipeCard.getByRole('button', { name: 'Ajouter aux favoris', exact: true }),
  ).toHaveAttribute('aria-pressed', 'false');
  await recipeCard.locator('.projects-page__project-name').click();
  const projectId = new URL(page.url()).pathname.match(/\/projects\/([^/]+)/)?.[1];
  expect(projectId).toBeTruthy();

  await page.goto(`/o/${CONTOSO_ID}/projects/${projectId}/settings`);
  const projectName = page.locator('input[name="projectName"]');
  const projectDescription = page.locator('#project-description');
  await expect(projectName).toBeVisible();
  if (
    (await projectName.inputValue()) !== desiredProjectName ||
    (await projectDescription.inputValue()) !== ''
  ) {
    await projectName.fill(desiredProjectName);
    await projectDescription.fill('');
    const projectUpdate = await saveProjectSettings(page, projectId!);
    expect(projectUpdate.status()).toBe(200);
    await expect(page.locator('.project-workspace__header h1')).toHaveText(desiredProjectName);
  }

  await page.goto(`/o/${CONTOSO_ID}/projects/${projectId}/environments`);
  const productionCard = page.locator('.project-environments__card').filter({ hasText: 'prd' });
  await expect(productionCard).toBeVisible();
  const environmentNavigation = page.waitForURL(/\/environments\/[^/]+$/);
  await productionCard.getByRole('link', { name: 'Modifier', exact: true }).click();
  await environmentNavigation;
  const environmentId = new URL(page.url()).pathname.match(/\/environments\/([^/]+)$/)?.[1];
  expect(environmentId).toBeTruthy();
  const environmentDescription = page.locator('#environment-description');
  await expect(environmentDescription).toBeVisible();
  if ((await environmentDescription.inputValue()) !== '') {
    await environmentDescription.fill('');
    const environmentUpdate = waitForEnvironmentUpdate(page, projectId!, environmentId!);
    await page.getByRole('button', { name: 'Enregistrer', exact: true }).click();
    expect((await environmentUpdate).status()).toBe(200);
  }

  await page.goto(`/o/${CONTOSO_ID}/projects/${projectId}/members`);

  const bobRow = page.getByRole('row').filter({ hasText: 'Bob Bernard' });
  const chloeRow = page.getByRole('row').filter({ hasText: 'chloe@contoso.example' });
  await expect(bobRow).toBeVisible();
  await expect(chloeRow).toBeVisible();
  await setProjectRoles(bobRow, []);
  await setProjectRoles(chloeRow, []);
}

async function saveProjectSettings(page: Page, projectId: string) {
  const initialSave = waitForProjectUpdate(page, projectId);
  await page.getByRole('button', { name: 'Enregistrer les paramètres', exact: true }).click();
  const initialResponse = await initialSave;
  if (initialResponse.status() !== 409) {
    return initialResponse;
  }

  const impactDialog = page.getByRole('dialog', { name: 'Confirmer les changements', exact: true });
  await expect(impactDialog).toBeVisible();
  const confirmedSave = waitForProjectUpdate(page, projectId);
  await impactDialog.getByRole('button', { name: 'Confirmer et enregistrer', exact: true }).click();
  return confirmedSave;
}

async function assertNoSeriousAccessibilityViolations(page: Page): Promise<void> {
  const { violations } = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(
    violations.filter(
      (violation) => violation.impact === 'serious' || violation.impact === 'critical',
    ),
  ).toEqual([]);
}

function waitForEnvironmentUpdate(page: Page, projectId: string, environmentId: string) {
  return page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname ===
        `/v1/projects/${projectId}/environments/${environmentId}`,
    { timeout: 15_000 },
  );
}

function waitForEnvironmentUpdateRequest(page: Page, projectId: string, environmentId: string) {
  return page.waitForRequest(
    (request) =>
      request.method() === 'PUT' &&
      new URL(request.url()).pathname === `/v1/projects/${projectId}/environments/${environmentId}`,
    { timeout: 15_000 },
  );
}

function waitForProjectUpdate(page: Page, projectId: string) {
  return page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname === `/v1/projects/${projectId}`,
    { timeout: 15_000 },
  );
}

async function selectFrench(page: Page): Promise<void> {
  if ((page.viewportSize()?.width ?? 1440) <= 760) {
    await page.getByRole('button', { name: /Menu/ }).click();
  }

  const languageSwitch = page.locator('app-language-switch').getByRole('group');
  const frenchButton = languageSwitch.getByRole('button', { name: 'FR', exact: true });
  if ((await frenchButton.getAttribute('aria-pressed')) !== 'true') {
    const frenchPreferenceSaved = page.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname === '/v1/me/preferences',
    );
    await frenchButton.click();
    await expect(page.locator('html')).toHaveAttribute('lang', 'fr');
    expect((await frenchPreferenceSaved).ok()).toBeTruthy();
  }

  if ((page.viewportSize()?.width ?? 1440) <= 760) {
    await page.keyboard.press('Escape');
  }
}
