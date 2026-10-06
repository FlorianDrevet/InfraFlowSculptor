import { expect, type Page } from '@playwright/test';

const LOCAL_KEYCLOAK_ORIGIN = 'https://localhost:8080';

export async function loginAsAlice(page: Page): Promise<void> {
  const username = process.env['IFS_E2E_USERNAME'] ?? 'alice@contoso.example';
  const password = process.env['IFS_E2E_PASSWORD'];

  if (!password) {
    throw new Error(
      'Set IFS_E2E_PASSWORD to the local Alice demo password before running npm run e2e.',
    );
  }

  await page.addInitScript(() => {
    if (localStorage.getItem('ifs.language') === null) {
      localStorage.setItem('ifs.language', 'fr');
    }
  });
  await page.goto('/login');
  await page.getByTestId('login-with-microsoft').click();
  await expect(page).toHaveURL(
    new RegExp(`^${LOCAL_KEYCLOAK_ORIGIN.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}/`),
  );
  await page.locator('#username').fill(username);
  await page.locator('#password').fill(password);
  await page.locator('form button[type="submit"]').click();
  await expect(page).toHaveURL(/^http:\/\/localhost:4200\//);
}
