import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { expect, test, type Page } from '@playwright/test';
import { loginAsAlice } from './support/login';

const CONTOSO_ID = '33333333-3333-4333-8333-333333333333';

test('Alice consulte les constats du projet de démonstration', async ({ page }) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo accounts.');
  page.setDefaultTimeout(15_000);
  page.setDefaultNavigationTimeout(30_000);

  await loginAsAlice(page);
  await page.goto(`/o/${CONTOSO_ID}/projects`);
  const shopCard = page.locator('.projects-page__card').filter({
    has: page.locator('.projects-page__code').getByText('shop', { exact: true }),
  });
  await expect(shopCard.locator('.projects-page__project-name')).toHaveText('Boutique en ligne');
  const projectNavigation = page.waitForURL(/\/projects\/[0-9a-f-]+$/i);
  await shopCard.getByRole('button', { name: 'Boutique en ligne', exact: true }).click();
  await projectNavigation;

  const projectId = new URL(page.url()).pathname.match(/\/projects\/([^/]+)$/)?.[1];
  expect(projectId).toBeTruthy();
  const projectUrl = `/o/${CONTOSO_ID}/projects/${projectId}`;
  const findingsUrl = `${projectUrl}/findings`;
  await page.goto(findingsUrl);

  await expect(
    page.getByRole('heading', { name: 'Constats de validation', exact: true }),
  ).toBeVisible();
  await expect(page.locator('.project-findings__readiness')).toContainText('0 erreur');
  await expect(page.locator('.project-findings__counts')).toContainText('1 avertissement');
  await expect(page.locator('.project-findings__counts')).toContainText('1 info');
  const findings = page.locator('.project-findings__item');
  await expect(findings).toHaveCount(2);
  await expect(findings.filter({ hasText: 'VAL-LIA-SQL-ADMIN' })).toHaveCount(1);
  await expect(findings.filter({ hasText: 'VAL-LIA-PORTEE-EXTERNE' })).toHaveCount(1);

  const screenshotDirectory = resolve(process.cwd(), 'e2e/__captures__/R-05');
  await mkdir(screenshotDirectory, { recursive: true });
  const viewportWidth = page.viewportSize()?.width ?? 1440;
  await page.screenshot({
    path: resolve(screenshotDirectory, `ProjectFindings-${viewportWidth}.png`),
    fullPage: true,
  });

  let requiredLinkNeedsFix = false;
  let acknowledgmentNeedsInvalidation = false;
  let sqlDescriptionNeedsRestore = false;
  let apiResourceUrl: string | null = null;
  let sqlResourceUrl: string | null = null;
  let originalSqlDescription: string | null = null;

  try {
    await page.goto(projectUrl);
    const ordersLink = page.getByRole('link', { name: 'Orders orders', exact: true });
    await expect(ordersLink).toBeVisible();
    const ordersNavigation = page.waitForURL(/\/components\/[0-9a-f-]+$/i);
    await ordersLink.click();
    await ordersNavigation;
    await expect(page.locator('.component-detail__dependencies')).toContainText(
      'core, data, platform',
    );

    const apiResourceLink = page.getByRole('link', { name: 'api ContainerApp', exact: true });
    apiResourceUrl = await apiResourceLink.getAttribute('href');
    expect(apiResourceUrl).toBeTruthy();
    const apiResourceNavigation = page.waitForURL(/\/resources\/[0-9a-f-]+$/i);
    await apiResourceLink.click();
    await apiResourceNavigation;
    await page.getByRole('link', { name: 'Liaisons', exact: true }).click();
    await expect(page).toHaveURL(/tab=links$/);

    const hostingLink = page.locator('.resource-links__item').filter({
      has: page.locator('.resource-links__kind').getByText('Hébergement', { exact: true }),
    });
    await expect(hostingLink).toHaveCount(1);
    requiredLinkNeedsFix = true;
    await hostingLink.getByRole('button', { name: 'Retirer', exact: true }).click();
    const removalDialog = page.getByRole('dialog');
    await expect(removalDialog).toBeVisible();
    await removalDialog.getByRole('button', { name: 'Retirer la liaison', exact: true }).click();
    await expect(hostingLink).toHaveCount(0);

    await page.goto(findingsUrl);
    const missingHostingFinding = page
      .locator('.project-findings__item')
      .filter({ hasText: 'VAL-LIA-OBLIGATOIRE' });
    await expect(missingHostingFinding).toHaveCount(1);
    await expect(missingHostingFinding).toContainText('api');
    await missingHostingFinding.getByRole('button', { name: 'Corriger', exact: true }).click();
    await expect(missingHostingFinding).toHaveCount(0);
    requiredLinkNeedsFix = false;

    await page.goto(projectUrl);
    const dataLink = page.getByRole('link', { name: 'Data data', exact: true });
    await expect(dataLink).toBeVisible();
    const dataNavigation = page.waitForURL(/\/components\/[0-9a-f-]+$/i);
    await dataLink.click();
    await dataNavigation;
    const sqlServerLink = page.getByRole('link', { name: 'orders SqlServer', exact: true });
    sqlResourceUrl = await sqlServerLink.getAttribute('href');
    expect(sqlResourceUrl).toBeTruthy();
    await sqlServerLink.click();
    await expect(page.locator('input[name="resourceDescription"]')).toBeVisible();
    originalSqlDescription = await page.locator('input[name="resourceDescription"]').inputValue();
    await page.goto(findingsUrl);

    const sqlAdminFinding = page
      .locator('.project-findings__item')
      .filter({ hasText: 'VAL-LIA-SQL-ADMIN' });
    await expect(sqlAdminFinding).toHaveCount(1);
    acknowledgmentNeedsInvalidation = true;
    await sqlAdminFinding.getByRole('button', { name: /Acquitter/ }).click();
    const acknowledgmentDialog = page.getByRole('dialog', {
      name: 'Acquitter cet avertissement',
    });
    const acknowledgmentComment = 'Recette J0-20 : contrôle après modification de sql orders';
    await acknowledgmentDialog.getByLabel('Commentaire obligatoire').fill(acknowledgmentComment);
    await acknowledgmentDialog
      .getByRole('button', { name: 'Enregistrer l’acquittement', exact: true })
      .click();
    await expect(sqlAdminFinding.locator('.project-findings__acknowledgment')).toContainText(
      acknowledgmentComment,
      { timeout: 15_000 },
    );
    await expect(sqlAdminFinding).toHaveClass(/project-findings__item--acknowledged/, {
      timeout: 15_000,
    });

    const updatedSqlDescription = `${originalSqlDescription} Recette J0-20`.trim();
    sqlDescriptionNeedsRestore = true;
    await saveResourceDescription(page, sqlResourceUrl!, updatedSqlDescription);
    await page.goto(findingsUrl);
    await expect(sqlAdminFinding).toHaveCount(1);
    await expect(sqlAdminFinding).not.toHaveClass(/project-findings__item--acknowledged/);
    await expect(sqlAdminFinding.locator('.project-findings__acknowledgment')).toHaveCount(0);
    acknowledgmentNeedsInvalidation = false;

    await saveResourceDescription(page, sqlResourceUrl!, originalSqlDescription);
    sqlDescriptionNeedsRestore = false;
  } finally {
    if (requiredLinkNeedsFix && apiResourceUrl) {
      try {
        await page.goto(apiResourceUrl);
        await page.getByRole('link', { name: 'Liaisons', exact: true }).click();
        await expect(
          page.locator('.resource-links__empty, .resource-links__list').first(),
        ).toBeVisible();
        const hostingLink = page.locator('.resource-links__item').filter({
          has: page.locator('.resource-links__kind').getByText('Hébergement', { exact: true }),
        });
        if (!(await hostingLink.count())) {
          await page.goto(findingsUrl);
          const missingHostingFinding = page
            .locator('.project-findings__item')
            .filter({ hasText: 'VAL-LIA-OBLIGATOIRE' });
          await expect(missingHostingFinding).toHaveCount(1);
          await missingHostingFinding
            .getByRole('button', { name: 'Corriger', exact: true })
            .click();
          await expect(missingHostingFinding).toHaveCount(0);
          await page.goto(apiResourceUrl);
          await page.getByRole('link', { name: 'Liaisons', exact: true }).click();
          await expect(
            page.locator('.resource-links__empty, .resource-links__list').first(),
          ).toBeVisible();
        }
        await expect(hostingLink).toHaveCount(1);
      } catch {
        // Keep the original recipe failure; the CI fixture is discarded with the AppHost.
      }
    }

    if (
      sqlResourceUrl &&
      originalSqlDescription !== null &&
      (acknowledgmentNeedsInvalidation || sqlDescriptionNeedsRestore)
    ) {
      try {
        if (acknowledgmentNeedsInvalidation) {
          await saveResourceDescription(
            page,
            sqlResourceUrl,
            `${originalSqlDescription} Recette J0-20 cleanup`.trim(),
          );
        }
        await saveResourceDescription(page, sqlResourceUrl, originalSqlDescription);
      } catch {
        // Keep the original recipe failure; the CI fixture is discarded with the AppHost.
      }
    }
  }
});

async function saveResourceDescription(
  page: Page,
  resourceUrl: string,
  description: string,
): Promise<void> {
  await page.goto(resourceUrl);
  const form = page.locator('form.resource-editor__metadata');
  const descriptionInput = form.locator('input[name="resourceDescription"]');
  await expect(descriptionInput).toBeVisible();
  const responsePromise = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname.endsWith('/metadata'),
  );
  await descriptionInput.fill(description);
  await form.getByRole('button', { name: 'Enregistrer', exact: true }).click();
  const response = await responsePromise;
  expect(response.ok(), await response.text()).toBeTruthy();
  await expect(descriptionInput).toHaveValue(description.trim());
}
