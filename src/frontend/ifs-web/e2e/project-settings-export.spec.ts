import { expect, test } from '@playwright/test';
import { loginAsAlice } from './support/login';

const CONTOSO_ID = '33333333-3333-4333-8333-333333333333';

test('project export panel follows the settings layout on desktop and mobile', async ({ page }) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo accounts.');
  page.setDefaultTimeout(15_000);
  page.setDefaultNavigationTimeout(30_000);

  await loginAsAlice(page);
  await page.goto(`/o/${CONTOSO_ID}/projects`);
  const shopCard = page.locator('.projects-page__card').filter({
    has: page.locator('.projects-page__code').getByText('shop', { exact: true }),
  });
  await expect(shopCard).toBeVisible();
  const projectNavigation = page.waitForURL((url) =>
    /\/projects\/[0-9a-f-]{36}(?:\/|$)/i.test(url.pathname),
  );
  await shopCard.locator('.projects-page__project-name').click();
  await projectNavigation;

  const projectId = new URL(page.url()).pathname.match(/\/projects\/([0-9a-f-]{36})(?:\/|$)/i)?.[1];
  expect(projectId).toBeTruthy();
  await page.goto(`/o/${CONTOSO_ID}/projects/${projectId}/settings`);

  const layout = page.locator('.project-settings__layout');
  const form = layout.locator('form');
  const exportPanel = layout.locator('.project-settings__export');
  await expect(exportPanel).toBeVisible();
  const boxes = await layout.evaluate((element) => {
    const formBox = element.querySelector('form')!.getBoundingClientRect();
    const panelBox = element.querySelector('.project-settings__export')!.getBoundingClientRect();
    return {
      viewportWidth: window.innerWidth,
      documentWidth: document.documentElement.scrollWidth,
      formBottom: formBox.bottom,
      formRight: formBox.right,
      panelLeft: panelBox.left,
      panelTop: panelBox.top,
    };
  });

  if (boxes.viewportWidth > 1024) {
    expect(boxes.panelLeft - boxes.formRight).toBeGreaterThanOrEqual(20);
  } else {
    expect(boxes.documentWidth).toBe(boxes.viewportWidth);
    expect(boxes.panelTop).toBeGreaterThanOrEqual(boxes.formBottom);
  }
  await expect(exportPanel.getByRole('button', { name: 'Exporter le projet' })).toBeVisible();
  await expect(form).toBeVisible();
});
