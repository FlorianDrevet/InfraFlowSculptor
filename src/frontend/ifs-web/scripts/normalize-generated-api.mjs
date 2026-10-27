import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export function normalizeGeneratedApi(directory) {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const entryPath = join(directory, entry.name);
    if (entry.isDirectory()) {
      normalizeGeneratedApi(entryPath);
    } else if (entry.isFile() && entry.name.endsWith('.ts')) {
      const original = readFileSync(entryPath, 'utf8');
      let normalized = original.replace(/\r\n?/g, '\n').replace(/\n*$/, '\n');
      if (entry.name === 'json-element.ts') {
        normalized = normalized.replace(
          'export type JsonElement = any;',
          'export type JsonElement = boolean | null | number | string | JsonElement[] | { [key: string]: JsonElement };',
        );
      }
      if (normalized !== original) {
        writeFileSync(entryPath, normalized, 'utf8');
      }
    }
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  normalizeGeneratedApi(resolve(process.cwd(), 'src/app/core/api/generated'));
}
