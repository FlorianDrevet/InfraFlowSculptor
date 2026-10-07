import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HEADER = '/* Généré depuis docs/design/strata/tokens.json — ne pas modifier. */';
const TOKEN_GROUPS = ['color', 'spacing', 'radius', 'shadow', 'size'];
const REPOSITORY_ROOT = fileURLToPath(new URL('../../../..', import.meta.url));

function allTypeStyles(tokens) {
  return tokens.type.groups.flatMap((group) => group.styles);
}

function tokenVariable(name) {
  return `--ifs-${name}`;
}

function themeMapping(namespace, name, sourceName = name) {
  return `  --${namespace}-${name}: var(${tokenVariable(sourceName)});`;
}

function typeThemeMapping(styleName, property, sourceName) {
  return `  --text-${styleName}--${property}: var(${tokenVariable(sourceName)});`;
}

function fontAndTypeVariables(tokens) {
  const lines = [
    `  ${tokenVariable('font-sans')}: ${tokens.type.families.sans};`,
    `  ${tokenVariable('font-mono')}: ${tokens.type.families.mono};`,
  ];

  for (const style of allTypeStyles(tokens)) {
    lines.push(`  ${tokenVariable(`text-${style.name}`)}: ${style.fontSize};`);
    lines.push(`  ${tokenVariable(`line-height-${style.name}`)}: ${style.lineHeight};`);
    if (style.fontWeight !== undefined) {
      lines.push(`  ${tokenVariable(`font-weight-${style.name}`)}: ${style.fontWeight};`);
    }
    if (style.letterSpacing !== undefined) {
      lines.push(`  ${tokenVariable(`letter-spacing-${style.name}`)}: ${style.letterSpacing};`);
    }
  }

  return lines;
}

function renderTokensCss(tokens) {
  const lines = [HEADER, ':root {'];
  for (const group of TOKEN_GROUPS) {
    for (const token of tokens[group].tokens) {
      lines.push(`  ${tokenVariable(token.name)}: ${token.value};`);
    }
  }
  lines.push(...fontAndTypeVariables(tokens), '}', '');
  return lines.join('\n');
}

function renderThemeOverrides(tokens) {
  return tokens.color.themes
    .map((theme, index) => {
      const selector = index === 0 ? `:root, :root[data-theme="${theme.id}"]` : `:root[data-theme="${theme.id}"]`;
      const lines = [`${selector} {`];
      for (const token of tokens.color.tokens) {
        const value = token.values?.[theme.id] ?? token.value;
        lines.push(`  ${tokenVariable(token.name)}: ${value};`);
      }
      lines.push('}');
      return lines.join('\n');
    })
    .join('\n\n');
}

function renderTailwindTheme(tokens) {
  const lines = ['@theme {'];
  for (const token of tokens.color.tokens) {
    lines.push(themeMapping('color', token.name));
  }
  for (const token of tokens.spacing.tokens) {
    lines.push(themeMapping('spacing', token.name.replace(/^space-/, ''), token.name));
  }
  for (const token of tokens.radius.tokens) {
    lines.push(themeMapping('radius', token.name.replace(/^radius-/, ''), token.name));
  }
  for (const token of tokens.shadow.tokens) {
    lines.push(themeMapping('shadow', token.name.replace(/^shadow-/, ''), token.name));
  }
  for (const token of tokens.size.tokens) {
    lines.push(themeMapping('size', token.name, token.name));
  }
  lines.push(themeMapping('font', 'sans', 'font-sans'));
  lines.push(themeMapping('font', 'mono', 'font-mono'));
  for (const style of allTypeStyles(tokens)) {
    lines.push(themeMapping('text', style.name, `text-${style.name}`));
    if (style.lineHeight !== undefined) {
      lines.push(typeThemeMapping(style.name, 'line-height', `line-height-${style.name}`));
    }
    if (style.fontWeight !== undefined) {
      lines.push(typeThemeMapping(style.name, 'font-weight', `font-weight-${style.name}`));
    }
    if (style.letterSpacing !== undefined) {
      lines.push(typeThemeMapping(style.name, 'letter-spacing', `letter-spacing-${style.name}`));
    }
  }
  lines.push('}');
  return lines.join('\n');
}

function renderThemeCss(tokens) {
  return `${HEADER}\n${renderThemeOverrides(tokens)}\n\n${renderTailwindTheme(tokens)}\n`;
}

function renderThemesTypeScript(tokens) {
  const themeIds = tokens.color.themes.map((theme) => theme.id);
  const defaultTheme = themeIds[0];
  const themeList = themeIds.map((id) => `'${id.replaceAll('\\', '\\\\').replaceAll("'", "\\'")}'`).join(', ');
  const defaultLiteral = `'${defaultTheme.replaceAll('\\', '\\\\').replaceAll("'", "\\'")}'`;
  return `${HEADER}\nexport const AVAILABLE_THEMES = [${themeList}] as const;\nexport const DEFAULT_THEME = ${defaultLiteral};\n`;
}

function exportConstant(name, value) {
  return `export const ${name} = ${JSON.stringify(value, null, 2)} as const;`;
}

function renderFoundationsTypeScript(tokens) {
  const foundations = [
    ['COLOR_TOKENS', tokens.color.tokens],
    ['TYPE_GROUPS', tokens.type.groups],
    ['FONT_FAMILIES', tokens.type.families],
    ['SPACING_TOKENS', tokens.spacing.tokens],
    ['RADIUS_TOKENS', tokens.radius.tokens],
    ['SHADOW_TOKENS', tokens.shadow.tokens],
    ['SIZE_TOKENS', tokens.size.tokens],
  ];
  return `${HEADER}\n${foundations.map(([name, value]) => exportConstant(name, value)).join('\n\n')}\n`;
}

export function generateOutputs(tokens) {
  return {
    'src/styles/tokens.css': renderTokensCss(tokens),
    'src/styles/theme.css': renderThemeCss(tokens),
    'src/app/core/theme/themes.generated.ts': renderThemesTypeScript(tokens),
    'src/app/core/theme/foundations.generated.ts': renderFoundationsTypeScript(tokens),
  };
}

export async function runBuildTokens({ root = REPOSITORY_ROOT, projectRoot = join(root, 'src/frontend/ifs-web'), check = false } = {}) {
  const tokenPath = join(root, 'docs/design/strata/tokens.json');
  const tokens = JSON.parse(await readFile(tokenPath, 'utf8'));
  const outputs = generateOutputs(tokens);

  if (check) {
    const stale = [];
    for (const [relativePath, expected] of Object.entries(outputs)) {
      try {
        const actual = await readFile(join(projectRoot, relativePath), 'utf8');
        if (actual !== expected) {
          stale.push(relativePath);
        }
      } catch (error) {
        if (error.code !== 'ENOENT') {
          throw error;
        }
        stale.push(relativePath);
      }
    }
    if (stale.length > 0) {
      throw new Error(`Generated token files are stale:\n${stale.map((path) => `- ${path}`).join('\n')}`);
    }
    return;
  }

  for (const [relativePath, contents] of Object.entries(outputs)) {
    const outputPath = join(projectRoot, relativePath);
    await mkdir(dirname(outputPath), { recursive: true });
    await writeFile(outputPath, contents, 'utf8');
  }
}

const invokedPath = process.argv[1] ? resolve(process.argv[1]) : undefined;
const modulePath = fileURLToPath(import.meta.url);
if (invokedPath === modulePath) {
  try {
    const check = process.argv.includes('--check');
    await runBuildTokens({ check });
    process.stdout.write(check ? 'Generated token files are current.\n' : 'Generated Strata token files.\n');
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}
