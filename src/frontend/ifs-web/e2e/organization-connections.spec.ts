import AxeBuilder from '@axe-core/playwright';
import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { expect, test, type Page } from '@playwright/test';
import { loginAsAlice } from './support/login';

const CONTOSO_ID = '33333333-3333-4333-8333-333333333333';
const GITEA_CONNECTION_NAME = 'Gitea recette J0-26';
const AZURE_CONNECTION_NAME = 'Azure DevOps recette J0-26';
const AZURE_ORGANIZATION = 'floriandrevet0332';

test.use({ trace: 'off', screenshot: 'off' });

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

function sidebar(page: Page, projectName: string) {
  return projectName === 'mobile'
    ? page.locator('.sidebar--drawer')
    : page.locator('.shell__sidebar');
}

test('Alice connecte Gitea et Azure DevOps et reçoit l’alerte d’expiration', async ({
  page,
  request,
}, testInfo) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set the local demo password for Alice.');
  test.skip(!process.env['IFS_E2E_AZDO_PAT'], 'Set a short-lived Azure DevOps test PAT.');
  test.skip(!process.env['IFS_E2E_MAILPIT_URL'], 'Set the local MailPit HTTP endpoint.');

  await loginAsAlice(page);
  await openMobileMenu(page, testInfo.project.name);
  const organizationSelector = sidebar(page, testInfo.project.name).getByRole('combobox', {
    name: 'Organisation active',
  });
  await organizationSelector.selectOption(CONTOSO_ID);
  await expect(organizationSelector).toHaveValue(CONTOSO_ID);
  await sidebar(page, testInfo.project.name)
    .getByRole('link', { name: 'Connexions', exact: true })
    .click();
  await closeMobileMenu(page, testInfo.project.name);
  await expect(page.getByRole('heading', { name: 'Connexions', exact: true })).toBeVisible();
  const addConnectionButton = page.getByRole('button', {
    name: 'Ajouter une connexion',
    exact: true,
  });
  await expect(addConnectionButton).toBeVisible();

  const giteaCards = page
    .locator('.git-connections__card')
    .filter({ hasText: GITEA_CONNECTION_NAME });
  while ((await giteaCards.count()) > 1) {
    const duplicateCount = await giteaCards.count();
    const deleteResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'DELETE' &&
        /\/git-connections\/[0-9a-f-]+$/i.test(new URL(response.url()).pathname),
    );
    await giteaCards.last().getByRole('button', { name: 'Retirer', exact: true }).click();
    const removalDialog = page.getByRole('dialog');
    await expect(removalDialog).toBeVisible();
    await removalDialog.getByRole('button', { name: 'Retirer', exact: true }).click();
    expect((await deleteResponse).status()).toBe(204);
    await expect(giteaCards).toHaveCount(duplicateCount - 1);
  }

  let giteaCard = giteaCards.first();
  if ((await giteaCard.count()) === 0) {
    await addConnectionButton.click();
    await page.getByRole('combobox', { name: 'Fournisseur', exact: true }).selectOption('Gitea');
    await page.getByLabel('Nom de la connexion').fill(GITEA_CONNECTION_NAME);
    await page.getByLabel('Organisation Gitea').fill('contoso');
    const createResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname.endsWith('/git-connections'),
    );
    await page.getByRole('button', { name: 'Enregistrer et tester', exact: true }).click();
    expect((await createResponse).status()).toBe(201);
    await expect(page.getByText('La connexion a été testée et enregistrée.')).toBeVisible();
    giteaCard = giteaCards.first();
  }

  const shopRepository = giteaCard.getByRole('group', { name: 'shop', exact: true });
  await expect(shopRepository.locator('legend')).toHaveText('shop');
  await expect(
    giteaCard.getByRole('group', { name: 'shop-infra', exact: true }).locator('legend'),
  ).toHaveText('shop-infra');
  await expect(
    giteaCard.getByRole('group', { name: 'shop-app', exact: true }).locator('legend'),
  ).toHaveText('shop-app');
  const allProjects = shopRepository.getByRole('checkbox', { name: 'Ouvrir à tous les projets' });
  if (await allProjects.isChecked()) {
    await allProjects.uncheck();
  }
  const projectLabels = shopRepository.locator('.git-connections__project-list label');
  for (const label of await projectLabels.all()) {
    const projectCode = await label
      .locator('[data-project-code]')
      .getAttribute('data-project-code');
    const checkbox = label.getByRole('checkbox');
    if (projectCode === 'shop') {
      await checkbox.check();
    } else if (await checkbox.isChecked()) {
      await checkbox.uncheck();
    }
  }
  const accessResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname.endsWith('/repositories/access'),
  );
  await shopRepository.getByRole('button', { name: 'Enregistrer les accès' }).click();
  expect((await accessResponse).status()).toBe(200);
  await expect(allProjects).not.toBeChecked();
  await expect(shopRepository.getByRole('checkbox', { checked: true })).toHaveCount(1);
  await expect(
    shopRepository.locator('[data-project-code="shop"]').locator('..').getByRole('checkbox'),
  ).toBeChecked();

  let azureCard = page.locator('.git-connections__card').filter({ hasText: AZURE_CONNECTION_NAME });
  const createdAzureConnection = (await azureCard.count()) === 0;
  if (createdAzureConnection) {
    await page.getByRole('button', { name: 'Ajouter une connexion', exact: true }).click();
    await page.getByLabel('Nom de la connexion', { exact: true }).fill(AZURE_CONNECTION_NAME);
    await page.getByLabel('Organisation Azure DevOps', { exact: true }).fill(AZURE_ORGANIZATION);
    await page
      .getByRole('combobox', { name: 'Authentification', exact: true })
      .selectOption('PersonalAccessToken');
    await page
      .getByRole('textbox', { name: /Jeton d'accès personnel/ })
      .fill(process.env['IFS_E2E_AZDO_PAT']!);
    await page
      .getByLabel("Date d'expiration du jeton", { exact: true })
      .fill(new Date(Date.now() + 10 * 86_400_000).toISOString().slice(0, 10));
    const createResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname.endsWith('/git-connections'),
    );
    await page.getByRole('button', { name: 'Enregistrer et tester', exact: true }).click();
    expect((await createResponse).status()).toBe(201);
    console.log('[recipe] Azure DevOps connection created.');
    await expect(page.getByText('La connexion a été testée et enregistrée.')).toBeVisible();
    azureCard = page.locator('.git-connections__card').filter({ hasText: AZURE_CONNECTION_NAME });
  }

  await expect(azureCard.getByText(/Expire dans (?:[0-9]|1[0-5]) j/)).toBeVisible();
  await azureCard.getByRole('button', { name: 'Tester', exact: true }).click();
  await expect(
    page.getByText('La connexion a répondu. La liste des dépôts est à jour.'),
  ).toBeVisible();

  await page.goto('/dev/design-system');
  const jobComplete = page.getByRole('status').filter({
    hasText: 'Le contrôle des expirations est terminé. Vérifiez la boîte MailPit.',
  });
  const mailpitUrl = process.env['IFS_E2E_MAILPIT_URL']!;
  const messagesBeforeResponse = await request.get(`${mailpitUrl}/api/v1/messages?limit=50`);
  expect(messagesBeforeResponse.ok()).toBeTruthy();
  const messagesBefore = (await messagesBeforeResponse.json()) as {
    messages?: Array<{ Subject?: string; subject?: string }>;
  };
  const previousAlertCount = (messagesBefore.messages ?? []).filter((message) =>
    (message.Subject ?? message.subject ?? '').includes('Expiration du jeton Git'),
  ).length;

  await page.getByRole('button', { name: 'Déclencher le contrôle' }).click();
  await expect(jobComplete).toBeVisible({ timeout: 30_000 });
  const messagesAfterResponse = await request.get(`${mailpitUrl}/api/v1/messages?limit=50`);
  expect(messagesAfterResponse.ok()).toBeTruthy();
  const messagesAfter = (await messagesAfterResponse.json()) as {
    messages?: Array<{ Subject?: string; subject?: string }>;
  };
  const newAlertCount = (messagesAfter.messages ?? []).filter((message) =>
    (message.Subject ?? message.subject ?? '').includes('Expiration du jeton Git'),
  ).length;
  if (createdAzureConnection) {
    expect(newAlertCount).toBeGreaterThan(previousAlertCount);
  } else {
    expect(newAlertCount).toBeGreaterThan(0);
  }

  await page.goto(`/organizations/${CONTOSO_ID}/connections`);
  await expect(page.getByRole('heading', { name: 'Connexions' })).toBeVisible();
  await expect(
    page.getByRole('button', { name: 'Ajouter une connexion', exact: true }),
  ).toBeVisible();
  await expect(
    page.locator('.git-connections__card').filter({ hasText: GITEA_CONNECTION_NAME }),
  ).toBeVisible();
  await expect(
    page.locator('.git-connections__card').filter({ hasText: AZURE_CONNECTION_NAME }),
  ).toBeVisible();
  const providerGrid = page.locator('.git-connections__list');
  const providerColumns = await providerGrid.evaluate(
    (element) => getComputedStyle(element).gridTemplateColumns.split(' ').length,
  );
  expect(providerColumns).toBe(testInfo.project.name === 'mobile' ? 1 : 3);
  if (testInfo.project.name === 'desktop') {
    const azureCard = page
      .locator('.git-connections__card')
      .filter({ hasText: AZURE_CONNECTION_NAME });
    const giteaCard = page
      .locator('.git-connections__card')
      .filter({ hasText: GITEA_CONNECTION_NAME });
    const azureCardHeight = await azureCard.evaluate(
      (element) => element.getBoundingClientRect().height,
    );
    const giteaCardHeight = await giteaCard.evaluate(
      (element) => element.getBoundingClientRect().height,
    );
    expect(giteaCardHeight).toBeLessThan(azureCardHeight);
  }
  const pageWidths = await page.evaluate(() => ({
    client: document.documentElement.clientWidth,
    scroll: document.documentElement.scrollWidth,
  }));
  expect(pageWidths.scroll).toBeLessThanOrEqual(pageWidths.client);
  const { violations } = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(
    violations.filter(
      (violation) => violation.impact === 'serious' || violation.impact === 'critical',
    ),
  ).toEqual([]);
  const captureDirectory = resolve(process.cwd(), '../../..', 'docs/plan/revues/captures/R-07');
  await mkdir(captureDirectory, { recursive: true });
  const captureSuffix = testInfo.project.name === 'mobile' ? '390' : '1440';
  await page.screenshot({ path: resolve(captureDirectory, `OrgConnections-${captureSuffix}.png`) });

  const referencePage = await page.context().newPage();
  await referencePage.goto(
    pathToFileURL(
      resolve(process.cwd(), '../../..', 'docs/design/maquette-v1/preview/OrgConnections.html'),
    ).href,
  );
  await referencePage.screenshot({
    path: resolve(captureDirectory, `OrgConnections-reference-${captureSuffix}.png`),
  });
  await referencePage.close();
});
