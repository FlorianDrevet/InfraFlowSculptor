import { mkdir, writeFile } from 'node:fs/promises';
import { dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const env = (name, fallback) => process.env[name]?.trim() || fallback;
const provider = env('IFS_OIDC_PROVIDER', 'keycloak');
if (provider !== 'keycloak' && provider !== 'entra') {
  throw new Error('IFS_OIDC_PROVIDER must be keycloak or entra.');
}

const config = {
  apiUrl: env('IFS_API_URL', 'https://localhost:7246').replace(/\/+$/, ''),
  oidc: {
    provider,
    authority: env('IFS_OIDC_AUTHORITY', 'https://localhost:8080/realms/ifs'),
    clientId: env('IFS_OIDC_CLIENT_ID', 'ifs-web'),
    scope: env('IFS_OIDC_SCOPE', 'openid profile email'),
  },
};

const target = fileURLToPath(new URL('../public/config.json', import.meta.url));
await mkdir(dirname(target), { recursive: true });
await writeFile(target, `${JSON.stringify(config, null, 2)}\n`, 'utf8');
process.stdout.write('Wrote public/config.json from runtime configuration.\n');
