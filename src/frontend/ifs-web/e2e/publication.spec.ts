import AxeBuilder from '@axe-core/playwright';
import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { expect, test, type Locator, type Page } from '@playwright/test';
import { loginAsAlice } from './support/login';

const CONTOSO_ID = '33333333-3333-4333-8333-333333333333';

test.use({ trace: 'off', screenshot: 'off' });

test('Alice enregistre le mono-dépôt Gitea et retrouve le dépôt dans l’assistant', async ({
  page,
}, testInfo) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set the local demo password for Alice.');
  test.setTimeout(180_000);
  page.setDefaultTimeout(15_000);
  page.setDefaultNavigationTimeout(30_000);

  await loginAsAlice(page);
  await selectFrench(page);
  const viewportWidth = page.viewportSize()?.width ?? 1440;
  const connectionsHref = await openConnectionsPage(page, testInfo.project.name);
  const giteaCard = page.locator('.git-connections__card').filter({ hasText: /Gitea/i });
  const shopRepository = giteaCard.getByRole('group', { name: 'shop', exact: true });
  await expect(shopRepository).toBeVisible();
  const allProjects = shopRepository.getByRole('checkbox', { name: 'Ouvrir à tous les projets' });
  if (await allProjects.isChecked()) await allProjects.uncheck();
  await restoreProjectCodes(shopRepository, ['shop']);
  await saveRepositoryAccess(page, shopRepository);
  await expect(allProjects).not.toBeChecked();
  expect(await checkedProjectCodes(shopRepository)).toEqual(['shop']);

  await page.goto(`/o/${CONTOSO_ID}/projects`);
  const shopCard = page.locator('.projects-page__card').filter({
    has: page.locator('.projects-page__code').getByText('shop', { exact: true }),
  });
  await expect(shopCard).toBeVisible();
  await shopCard.locator('.projects-page__project-name').click();
  await page.waitForURL(/\/projects\/[0-9a-f-]+$/i);
  const projectId = new URL(page.url()).pathname.match(/\/projects\/([0-9a-f-]+)$/i)?.[1];
  expect(projectId).toBeTruthy();

  await page.goto(`/o/${CONTOSO_ID}/projects/${projectId}/publishing`);
  await expect(page.getByRole('heading', { name: 'Plan de publication' })).toBeVisible();
  await expect(
    page.locator('app-project-publishing').getByText('Chargement du projet…', { exact: true }),
  ).toBeHidden({ timeout: 45_000 });
  const defaultRepository = page.locator('#publication-default-repository');
  const shopOption = defaultRepository.locator('option').filter({ hasText: /\/shop$/i });
  await expect(shopOption).toHaveCount(1, { timeout: 30_000 });
  const shopOptionValue = await shopOption.getAttribute('value');
  expect(shopOptionValue).toBeTruthy();
  const shopOptionLabel = (await shopOption.textContent())?.trim() ?? '';
  expect(shopOptionLabel).toMatch(/gitea.*\/shop$/i);
  await defaultRepository.selectOption(shopOptionValue);
  await page.locator('.project-publishing__preset app-ds-text-field input').fill('');

  const savePlan = page.waitForResponse(
    response =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname === `/v1/projects/${projectId}/publish-plan`,
  );
  const reloadedPlan = page.waitForResponse(
    response =>
      response.request().method() === 'GET' &&
      new URL(response.url()).pathname === `/v1/projects/${projectId}/publish-plan`,
  ).then(response => response, () => null);
  await page.getByRole('button', { name: 'Enregistrer', exact: true }).click();
  const saveResponse = await savePlan;
  expect(saveResponse.status()).toBe(204);
  const planResponse = await reloadedPlan;
  if (!planResponse) throw new Error('The saved publication plan was not reloaded.');
  expect(planResponse.status()).toBe(200);
  const plan = (await planResponse.json()) as {
    preset: string;
    destinations: Array<{ providerRepositoryId: string; basePath: string }>;
    issues: Array<{ code: string }>;
  };
  expect(plan.preset).toBe('MonoRepo');
  expect(plan.destinations).toHaveLength(1);
  expect(plan.destinations[0]?.providerRepositoryId).toMatch(/shop$/i);
  expect(plan.destinations[0]?.basePath).toBe('');
  expect(plan.issues.map(issue => issue.code)).toContain('VAL-PIP-COMPATIBILITE');
  await expect(page.getByRole('status').filter({ hasText: 'plan de publication est enregistré' })).toBeVisible();

  const captureDirectory = resolve(process.cwd(), '../../../docs/plan/revues/captures/R-07');
  await mkdir(captureDirectory, { recursive: true });
  await page.screenshot({
    path: resolve(captureDirectory, `ProjectPublishPlan-${viewportWidth}.png`),
    fullPage: true,
  });
  const documentWidth = await page.evaluate(() => document.documentElement.scrollWidth);
  expect(documentWidth).toBeLessThanOrEqual(viewportWidth);
  const accessibility = await new AxeBuilder({ page }).include('.project-publishing').analyze();
  expect(accessibility.violations).toEqual([]);

  await page.goto(connectionsHref);
  await expect(page.getByRole('heading', { name: 'Connexions', exact: true })).toBeVisible();

  const originallyOpenToAll = await allProjects.isChecked();
  const originalProjectCodes = await checkedProjectCodes(shopRepository);
  expect(originallyOpenToAll).toBe(false);
  expect(originalProjectCodes).toEqual(['shop']);

  try {
    if (!originallyOpenToAll) {
      await allProjects.check();
      await saveRepositoryAccess(page, shopRepository);
    }

    await page.goto(`/o/${CONTOSO_ID}/projects/new`);
    const publishingStep = page.locator('.wizard-steps').getByRole('button', { name: /Publication/ });
    await expect(publishingStep).toBeVisible();
    await publishingStep.click();
    await expect(page.getByRole('heading', { name: 'Où publier les fichiers' })).toBeVisible();

    await page.getByRole('button', { name: /Mono-dépôt/ }).click();
    const wizardRepository = page.locator('#wizard-publishing-repository');
    const wizardShopOption = wizardRepository.locator('option').filter({ hasText: /\/shop$/i });
    await expect(wizardShopOption).toHaveCount(1);
    await expect(wizardShopOption).toContainText(/gitea.*\/shop$/i);
    await expect(wizardRepository.locator('option:checked')).toContainText(/gitea.*\/shop$/i);
  } finally {
    if (!originallyOpenToAll) {
      await page.goto(connectionsHref);
      await expect(page.getByRole('heading', { name: 'Connexions', exact: true })).toBeVisible();
      const restoreCard = page.locator('.git-connections__card').filter({ hasText: /Gitea/i });
      const restoreRepository = restoreCard.getByRole('group', { name: 'shop', exact: true });
      const restoreAllProjects = restoreRepository.getByRole('checkbox', {
        name: 'Ouvrir à tous les projets',
      });
      if (await restoreAllProjects.isChecked()) await restoreAllProjects.uncheck();
      await restoreProjectCodes(restoreRepository, originalProjectCodes);
      await saveRepositoryAccess(page, restoreRepository);
      await expect(restoreAllProjects).not.toBeChecked();
      expect(await checkedProjectCodes(restoreRepository)).toEqual(originalProjectCodes);
    }
  }
});

function sidebar(page: Page, projectName: string): Locator {
  return projectName === 'mobile'
    ? page.locator('.sidebar--drawer')
    : page.locator('.shell__sidebar');
}

async function openConnectionsPage(page: Page, projectName: string): Promise<string> {
  await page.goto(`/o/${CONTOSO_ID}/projects`);
  if ((page.viewportSize()?.width ?? 1440) <= 760) {
    await page.getByRole('button', { name: /Menu/ }).click();
  }
  const link = sidebar(page, projectName).getByRole('link', {
    name: 'Connexions',
    exact: true,
  });
  const href = await link.getAttribute('href');
  if (!href) throw new Error('The organization connections link is missing.');
  await page.goto(href);
  if ((page.viewportSize()?.width ?? 1440) <= 760) {
    const closeMenu = page.getByRole('button', { name: 'Fermer' });
    if (await closeMenu.isVisible()) await closeMenu.click();
  }
  await expect(page.getByRole('heading', { name: 'Connexions', exact: true })).toBeVisible({ timeout: 15_000 });
  return href;
}

async function selectFrench(page: Page): Promise<void> {
  const viewportWidth = page.viewportSize()?.width ?? 1440;
  if (viewportWidth <= 760) await page.getByRole('button', { name: /Menu/ }).click();
  const languageSwitch = page.locator('app-language-switch').getByRole('group');
  const french = languageSwitch.getByRole('button', { name: 'FR', exact: true });
  if ((await french.getAttribute('aria-pressed')) !== 'true') {
    const response = page.waitForResponse(
      item => item.request().method() === 'PUT' && new URL(item.url()).pathname === '/v1/me/preferences',
    );
    await french.click();
    expect((await response).ok()).toBeTruthy();
  }
  if (viewportWidth <= 760) await page.keyboard.press('Escape');
}

async function checkedProjectCodes(repository: Locator): Promise<string[]> {
  const codes: string[] = [];
  for (const label of await repository.locator('.git-connections__project-list label').all()) {
    const code = await label.locator('[data-project-code]').getAttribute('data-project-code');
    if (code && (await label.getByRole('checkbox').isChecked())) codes.push(code);
  }
  return codes.sort();
}

async function restoreProjectCodes(repository: Locator, expectedCodes: string[]): Promise<void> {
  if (expectedCodes.length > 0) {
    await expect(repository.locator('.git-connections__project-list label').first()).toBeVisible();
  }
  for (const label of await repository.locator('.git-connections__project-list label').all()) {
    const code = await label.locator('[data-project-code]').getAttribute('data-project-code');
    const checkbox = label.getByRole('checkbox');
    const shouldBeChecked = code !== null && expectedCodes.includes(code);
    if (shouldBeChecked && !(await checkbox.isChecked())) await checkbox.check();
    if (!shouldBeChecked && (await checkbox.isChecked())) await checkbox.uncheck();
  }
}

async function saveRepositoryAccess(page: Page, repository: Locator): Promise<void> {
  const response = page.waitForResponse(
    item =>
      item.request().method() === 'PUT' &&
      new URL(item.url()).pathname.endsWith('/repositories/access'),
  );
  await repository.getByRole('button', { name: 'Enregistrer les accès' }).click();
  expect((await response).status()).toBe(200);
}
