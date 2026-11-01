import { randomUUID } from 'node:crypto';
import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { expect, test } from '@playwright/test';
import { loginAsUser } from './support/login';

const CONTOSO_ID = '33333333-3333-4333-8333-333333333333';
const SHOP_PROJECT_ID = '77777777-7777-4777-8777-777777777777';

interface EffectivePermissions {
  version: number;
  permissions: string[];
}

interface OrganizationMember {
  id: string;
  email: string | null;
}

interface ProblemDetails {
  code?: string;
  permission?: string;
}

test('Chloé lectrice ne peut pas gérer les rôles du projet', async ({
  browser,
  page: viewportPage,
}) => {
  test.skip(!process.env['IFS_E2E_PASSWORD'], 'Set IFS_E2E_PASSWORD for the local demo accounts.');

  const context = await browser.newContext({
    baseURL: process.env['IFS_E2E_BASE_URL'] ?? 'http://localhost:4200',
    viewport: viewportPage.viewportSize() ?? undefined,
    ignoreHTTPSErrors: true,
    locale: 'fr-FR',
  });
  const page = await context.newPage();

  try {
    let resolveAuthenticatedRequest!: (request: { origin: string; authorization: string }) => void;
    const authenticatedRequest = new Promise<{ origin: string; authorization: string }>((resolve) => {
      resolveAuthenticatedRequest = resolve;
    });
    let capturedAuthorization = false;

    page.on('request', (request) => {
      void (async () => {
        const url = new URL(request.url());
        if (capturedAuthorization || !url.pathname.startsWith('/v1/')) return;

        const authorization = (await request.allHeaders())['authorization'];
        if (!authorization) return;

        capturedAuthorization = true;
        resolveAuthenticatedRequest({ origin: url.origin, authorization });
      })();
    });

    await loginAsUser(page, 'chloe@contoso.example');
    await page.goto(`/o/${CONTOSO_ID}/projects/${SHOP_PROJECT_ID}/members`);
    await expect(page.getByRole('heading', { name: 'Membres du projet', exact: true })).toBeVisible();
    const chloeRow = page.getByRole('row').filter({ hasText: 'chloe@contoso.example' });
    await expect(chloeRow).toBeVisible();
    await expect(chloeRow).toContainText('Lecteur');
    await expect(page.getByRole('button', { name: 'Modifier les rôles', exact: true })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Enregistrer', exact: true })).toHaveCount(0);
    const screenshotDirectory = resolve(process.cwd(), 'e2e/__captures__/R-05');
    await mkdir(screenshotDirectory, { recursive: true });
    const viewportWidth = viewportPage.viewportSize()?.width ?? 1440;
    await page.screenshot({
      path: resolve(screenshotDirectory, `ProjectMembers-Chloe-${viewportWidth}.png`),
      fullPage: true,
    });

    const credentials = await authenticatedRequest;
    const headers = {
      Authorization: credentials.authorization,
      'X-Ifs-Organization': CONTOSO_ID,
    };
    const api = context.request;
    const [permissionsResponse, membersResponse] = await Promise.all([
      api.get(`${credentials.origin}/v1/projects/${SHOP_PROJECT_ID}/permissions/me`, { headers }),
      api.get(`${credentials.origin}/v1/organizations/${CONTOSO_ID}/members`, { headers }),
    ]);
    expect(permissionsResponse.status()).toBe(200);
    expect(membersResponse.status()).toBe(200);

    const permissions = (await permissionsResponse.json()) as EffectivePermissions;
    expect(permissions.permissions).not.toContain('members.manage');
    const members = (await membersResponse.json()) as OrganizationMember[];
    const bob = members.find((member) => member.email === 'bob@contoso.example');
    expect(bob).toBeDefined();
    if (!bob) throw new Error('The local Contoso fixture must contain Bob.');

    const deniedChange = await api.post(
      `${credentials.origin}/v1/projects/${SHOP_PROJECT_ID}/members/${bob.id}/roles`,
      {
        headers: {
          ...headers,
          'Idempotency-Key': randomUUID().replaceAll('-', ''),
        },
        data: {
          role: 'Auditor',
          scope: 'Project',
          expectedVersion: permissions.version,
        },
      },
    );
    expect(deniedChange.status()).toBe(403);
    const problem = (await deniedChange.json()) as ProblemDetails;
    expect(problem.code).toBe('PROJECT_PERMISSION_REQUIRED');
    expect(problem.permission).toBe('members.manage');
  } finally {
    await context.close();
  }
});
