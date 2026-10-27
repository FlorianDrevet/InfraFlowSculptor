import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { expect, test, type Page } from '@playwright/test';
import { loginAsAlice, loginAsUser } from './support/login';

const CONTOSO_ID = '33333333-3333-4333-8333-333333333333';

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

async function openSidebarLink(page: Page, projectName: string, label: string): Promise<void> {
  await openMobileMenu(page, projectName);
  await sidebar(page, projectName).getByRole('link', { name: label, exact: true }).click();
  await closeMobileMenu(page, projectName);
}

test('Alice vérifie les changements d’audit et Chloé reçoit la permission requise', async ({
  page,
  browser,
}, testInfo) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo accounts.');

  await loginAsAlice(page);
  await openMobileMenu(page, testInfo.project.name);
  const organizationSelector = sidebar(page, testInfo.project.name).getByRole('combobox', {
    name: 'Organisation active',
  });
  await organizationSelector.selectOption(CONTOSO_ID);
  await expect(organizationSelector).toHaveValue(CONTOSO_ID);
  const organizationName = await organizationSelector.locator('option:checked').innerText();
  await closeMobileMenu(page, testInfo.project.name);

  const updatedName = 'Contoso audit ' + Date.now();
  let originalConnectionManagerRole: boolean | undefined;
  try {
    await openSidebarLink(page, testInfo.project.name, 'Organisation');
    await expect(page).toHaveURL(new RegExp('/organizations/' + CONTOSO_ID + '/settings$'));
    await page.getByLabel("Nom de l'organisation").fill(updatedName);
    const renameResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname.endsWith('/organizations/' + CONTOSO_ID),
    );
    await page.getByRole('button', { name: 'Enregistrer', exact: true }).click();
    expect((await renameResponse).status()).toBe(200);

    await openSidebarLink(page, testInfo.project.name, 'Membres');
    await expect(page).toHaveURL(new RegExp('/organizations/' + CONTOSO_ID + '/members$'));
    const bobRow = page.getByRole('row').filter({ hasText: 'Bob Bernard' });
    await expect(bobRow).toBeVisible();
    await bobRow.getByRole('button', { name: 'Modifier', exact: true }).click();
    const connectionManager = bobRow.getByRole('checkbox', { name: 'Gestionnaire des connexions' });
    originalConnectionManagerRole = await connectionManager.isChecked();
    await connectionManager.setChecked(!originalConnectionManagerRole);
    const roleResponse = page.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname.endsWith('/roles'),
    );
    await bobRow.getByRole('button', { name: 'Enregistrer', exact: true }).click();
    expect((await roleResponse).status()).toBe(200);

    await openSidebarLink(page, testInfo.project.name, 'Journal d’audit');
    await expect(page).toHaveURL(new RegExp('/organizations/' + CONTOSO_ID + '/audit$'));
    await expect(page.getByRole('heading', { name: 'Journal d’audit' })).toBeVisible();
    const organizationChange = page
      .getByRole('row')
      .filter({ hasText: 'Nom de l’organisation modifié' });
    await expect(organizationChange).toHaveCount(1);
    await expect(organizationChange).toContainText(organizationName);
    await expect(organizationChange).toContainText(updatedName);
    await expect(organizationChange).toContainText('Nom de l’organisation modifié');
    await expect(organizationChange).toContainText('Alice Martin');
    const roleChange = page
      .getByRole('row')
      .filter({ hasText: 'Rôles du membre modifiés' })
      .first();
    await expect(roleChange).toContainText('Alice Martin');
    await expect(roleChange).toContainText('Rôles');
    await expect(roleChange).toContainText('ConnectionManager');
    await expect(roleChange).toContainText('→');

    const viewportWidth = page.viewportSize()?.width ?? 1440;
    const screenshotDirectory = resolve(process.cwd(), 'e2e/__captures__/R-04');
    await mkdir(screenshotDirectory, { recursive: true });
    await page.screenshot({
      path: resolve(screenshotDirectory, 'OrgAudit-' + viewportWidth + '.png'),
      fullPage: true,
    });

    const chloePage = await browser.newPage({
      viewport: page.viewportSize() ?? undefined,
      baseURL: process.env['IFS_E2E_BASE_URL'] ?? 'http://localhost:4200',
      ignoreHTTPSErrors: true,
      locale: 'fr-FR',
    });
    try {
      await loginAsUser(chloePage, 'chloe@contoso.example');
      await openMobileMenu(chloePage, testInfo.project.name);
      const chloeOrganizationSelector = sidebar(chloePage, testInfo.project.name).getByRole(
        'combobox',
        {
          name: 'Organisation active',
        },
      );
      await chloeOrganizationSelector.selectOption(CONTOSO_ID);
      await expect(chloeOrganizationSelector).toHaveValue(CONTOSO_ID);
      await expect(
        sidebar(chloePage, testInfo.project.name).getByRole('link', {
          name: 'Journal d’audit',
          exact: true,
        }),
      ).toHaveCount(0);
      await closeMobileMenu(chloePage, testInfo.project.name);
      await chloePage.goto('/organizations/' + CONTOSO_ID + '/audit');
      await expect(chloePage.getByRole('alert')).toContainText(
        'Accès refusé. Permission requise : audit.lire.',
      );
      await chloePage.screenshot({
        path: resolve(screenshotDirectory, 'OrgAudit-Chloe-' + viewportWidth + '.png'),
        fullPage: true,
      });
    } finally {
      await chloePage.close();
    }
  } finally {
    try {
      if (originalConnectionManagerRole !== undefined) {
        await page.goto('/organizations/' + CONTOSO_ID + '/members');
        const bobRow = page.getByRole('row').filter({ hasText: 'Bob Bernard' });
        await bobRow.getByRole('button', { name: 'Modifier', exact: true }).click();
        const connectionManager = bobRow.getByRole('checkbox', {
          name: 'Gestionnaire des connexions',
        });
        if ((await connectionManager.isChecked()) !== originalConnectionManagerRole) {
          await connectionManager.setChecked(originalConnectionManagerRole);
          const restoreRoleResponse = page.waitForResponse(
            (response) =>
              response.request().method() === 'PUT' &&
              new URL(response.url()).pathname.endsWith('/roles'),
          );
          await bobRow.getByRole('button', { name: 'Enregistrer', exact: true }).click();
          expect((await restoreRoleResponse).status()).toBe(200);
        }
      }
    } finally {
      await page.goto('/organizations/' + CONTOSO_ID + '/settings');
      const organizationNameInput = page.getByLabel("Nom de l'organisation");
      await expect(organizationNameInput).toBeVisible();
      if ((await organizationNameInput.inputValue()) !== organizationName) {
        await organizationNameInput.fill(organizationName);
        const restoreNameResponse = page.waitForResponse(
          (response) =>
            response.request().method() === 'PUT' &&
            new URL(response.url()).pathname.endsWith('/organizations/' + CONTOSO_ID),
        );
        await page.getByRole('button', { name: 'Enregistrer', exact: true }).click();
        expect((await restoreNameResponse).status()).toBe(200);
      }
    }
  }
});
