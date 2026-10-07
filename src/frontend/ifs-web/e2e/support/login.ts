import { type Page } from '@playwright/test';

const LOCAL_KEYCLOAK_ORIGIN = 'https://localhost:8080';
const AUTH_NAVIGATION_TIMEOUT_MS = 15_000;

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
  const keycloakNavigation = page.waitForURL(
    (url) => url.origin === LOCAL_KEYCLOAK_ORIGIN,
    { waitUntil: 'commit', timeout: AUTH_NAVIGATION_TIMEOUT_MS },
  );
  await page.getByTestId('login-with-microsoft').getByRole('button').click();
  await keycloakNavigation;
  await page.locator('#username').fill(username);
  await page.locator('#password').fill(password);
  const appNavigation = page.waitForURL(
    (url) => url.origin === 'http://localhost:4200',
    { waitUntil: 'commit', timeout: AUTH_NAVIGATION_TIMEOUT_MS },
  );
  await page.locator('form button[type="submit"]').click();
  await appNavigation;
}
