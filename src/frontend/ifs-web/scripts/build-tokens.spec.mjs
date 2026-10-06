import { mkdtemp, mkdir, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { generateOutputs, runBuildTokens } from './build-tokens.mjs';

const temporaryRoots = [];

afterEach(async () => {
  await Promise.all(temporaryRoots.splice(0).map((root) => rm(root, { recursive: true, force: true })));
});

function minimalTokens() {
  return {
    color: {
      themes: [{ id: 'dark', name: 'Dark' }],
      tokens: [{ name: 'bg', value: '#11151c', usage: 'Canvas' }],
    },
    type: {
      families: { sans: '"Instrument Sans", sans-serif', mono: '"JetBrains Mono", monospace' },
      groups: [
        {
          name: 'Interface',
          family: 'sans',
          styles: [{ name: 'body', fontSize: '14px', lineHeight: '20px', fontWeight: 400, usage: 'Body text' }],
        },
      ],
    },
    spacing: { tokens: [{ name: 'space-1', value: '4px', usage: 'Gap' }] },
    radius: { tokens: [{ name: 'radius-md', value: '6px', usage: 'Control' }] },
    shadow: { tokens: [{ name: 'shadow-overlay', value: '0 2px black', usage: 'Floating surface' }] },
    size: { tokens: [{ name: 'control-height', value: '36px', usage: 'Control height' }] },
  };
}

describe('Strata token generator', () => {
  it('renders the CSS token file exactly from a reduced source', () => {
    const outputs = generateOutputs(minimalTokens());

    expect(outputs['src/styles/tokens.css']).toBe(`/* Généré depuis docs/design/strata/tokens.json — ne pas modifier. */
:root {
  --ifs-bg: #11151c;
  --ifs-space-1: 4px;
  --ifs-radius-md: 6px;
  --ifs-shadow-overlay: 0 2px black;
  --ifs-control-height: 36px;
  --ifs-font-sans: "Instrument Sans", sans-serif;
  --ifs-font-mono: "JetBrains Mono", monospace;
  --ifs-text-body: 14px;
  --ifs-line-height-body: 20px;
  --ifs-font-weight-body: 400;
}
`);
  });

  it('creates a block and generated theme list for every color theme', () => {
    const tokens = minimalTokens();
    tokens.color.themes.push({ id: 'light', name: 'Light' });
    tokens.color.tokens.push({ name: 'text', value: '#eeeeee', usage: 'Text' });
    tokens.color.tokens[0].values = { light: '#ffffff' };
    const outputs = generateOutputs(tokens);

    expect(outputs['src/styles/theme.css']).toBe(`/* Généré depuis docs/design/strata/tokens.json — ne pas modifier. */
:root, :root[data-theme="dark"] {
  --ifs-bg: #11151c;
  --ifs-text: #eeeeee;
}

:root[data-theme="light"] {
  --ifs-bg: #ffffff;
  --ifs-text: #eeeeee;
}

@theme {
  --color-bg: var(--ifs-bg);
  --color-text: var(--ifs-text);
  --spacing-1: var(--ifs-space-1);
  --radius-md: var(--ifs-radius-md);
  --shadow-overlay: var(--ifs-shadow-overlay);
  --size-control-height: var(--ifs-control-height);
  --font-sans: var(--ifs-font-sans);
  --font-mono: var(--ifs-font-mono);
  --text-body: var(--ifs-text-body);
  --text-body--line-height: var(--ifs-line-height-body);
  --text-body--font-weight: var(--ifs-font-weight-body);
}
`);
    expect(outputs['src/app/core/theme/themes.generated.ts']).toContain(
      "export const AVAILABLE_THEMES = ['dark', 'light'] as const;",
    );
    expect(outputs['src/app/core/theme/themes.generated.ts']).toContain("export const DEFAULT_THEME = 'dark';");
  });

  it('reports stale generated files in check mode without rewriting them', async () => {
    const root = await mkdtemp(join(tmpdir(), 'ifs-tokens-'));
    temporaryRoots.push(root);
    const tokens = minimalTokens();
    const outputs = generateOutputs(tokens);
    const tokenFile = join(root, 'docs/design/strata/tokens.json');
    await mkdir(join(root, 'docs/design/strata'), { recursive: true });
    await writeFile(tokenFile, JSON.stringify(tokens), 'utf8');

    for (const [relativePath, contents] of Object.entries(outputs)) {
      const outputPath = join(root, relativePath);
      await mkdir(join(outputPath, '..'), { recursive: true });
      await writeFile(outputPath, contents, 'utf8');
    }
    await writeFile(join(root, 'src/styles/tokens.css'), 'stale\n', 'utf8');

    await expect(runBuildTokens({ root, projectRoot: root, check: true })).rejects.toThrow('src/styles/tokens.css');
    await expect(runBuildTokens({ root, projectRoot: root, check: false })).resolves.toBeUndefined();
    await expect(runBuildTokens({ root, projectRoot: root, check: true })).resolves.toBeUndefined();
  });
});
