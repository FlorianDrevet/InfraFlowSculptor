import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { normalizeGeneratedApi } from './normalize-generated-api.mjs';

const temporaryRoots = [];

afterEach(async () => {
  await Promise.all(temporaryRoots.splice(0).map((root) => rm(root, { recursive: true, force: true })));
});

describe('generated API normalizer', () => {
  it('maps JsonElement to the recursive JSON value type', async () => {
    const root = await mkdtemp(join(tmpdir(), 'ifs-api-normalizer-'));
    temporaryRoots.push(root);
    const modelDirectory = join(root, 'models');
    await mkdir(modelDirectory);
    const modelPath = join(modelDirectory, 'json-element.ts');
    await writeFile(modelPath, 'export type JsonElement = any;\r\n', 'utf8');

    normalizeGeneratedApi(root);

    await expect(readFile(modelPath, 'utf8')).resolves.toBe(
      'export type JsonElement = boolean | null | number | string | JsonElement[] | { [key: string]: JsonElement };\n',
    );
  });
});
