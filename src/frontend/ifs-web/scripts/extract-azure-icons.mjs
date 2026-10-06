import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const defaultRoot = resolve(scriptDirectory, '../../../..');
const dataUriPrefix = 'data:image/svg+xml;base64,';

function objectBody(source, variableName) {
  const declaration = new RegExp(`\\bvar\\s+${variableName}\\s*=\\s*\\{`);
  const match = declaration.exec(source);

  if (!match) {
    throw new Error(`The bundle does not contain var ${variableName} = { … }`);
  }

  const opening = source.indexOf('{', match.index);
  let depth = 0;
  let quote = null;
  let escaped = false;

  for (let index = opening; index < source.length; index += 1) {
    const character = source[index];

    if (quote) {
      if (escaped) {
        escaped = false;
      } else if (character === '\\') {
        escaped = true;
      } else if (character === quote) {
        quote = null;
      }
      continue;
    }

    if (character === '"' || character === "'" || character === '`') {
      quote = character;
    } else if (character === '{') {
      depth += 1;
    } else if (character === '}') {
      depth -= 1;
      if (depth === 0) return source.slice(opening + 1, index);
    }
  }

  throw new Error(`The ${variableName} object is not closed`);
}

function parseAzureIcons(source) {
  const literal = `{${objectBody(source, 'AZURE_ICONS')}}`;
  let icons;

  try {
    icons = JSON.parse(literal);
  } catch (error) {
    throw new Error(`AZURE_ICONS must be a JSON-compatible object: ${error.message}`);
  }

  if (
    !icons ||
    Array.isArray(icons) ||
    typeof icons !== 'object' ||
    Object.keys(icons).length === 0
  ) {
    throw new Error('AZURE_ICONS must contain at least one icon');
  }

  return icons;
}

function parseResourceTypes(source) {
  const entries = [];
  const entryPattern =
    /^\s*([A-Za-z_$][\w$]*)\s*:\s*\{\s*label:\s*'([^']*)',\s*abbr:\s*'([^']*)',\s*category:\s*'([^']*)',\s*icon:\s*'([^']*)'(?:,\s*roadmap:\s*(true|false))?\s*\},?\s*$/u;

  for (const [lineNumber, line] of objectBody(source, 'RESOURCE_TYPES').split(/\r?\n/u).entries()) {
    if (!line.trim()) continue;

    const match = entryPattern.exec(line);
    if (!match) {
      throw new Error(`Unsupported RESOURCE_TYPES entry on line ${lineNumber + 1}`);
    }

    const [, type, label, abbr, category, icon, roadmap] = match;
    if (entries.some((entry) => entry.type === type)) {
      throw new Error(`Duplicate resource type: ${type}`);
    }

    entries.push({ type, label, abbr, category, icon, roadmap: roadmap === 'true' });
  }

  if (entries.length === 0)
    throw new Error('RESOURCE_TYPES must contain at least one resource type');
  return entries;
}

function decodeIcon(iconName, dataUri) {
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/u.test(iconName)) {
    throw new Error(`Unsafe Azure icon filename: ${iconName}`);
  }
  if (typeof dataUri !== 'string' || !dataUri.startsWith(dataUriPrefix)) {
    throw new Error(`Azure icon ${iconName} is not an SVG base64 data URI`);
  }

  const encoded = dataUri.slice(dataUriPrefix.length);
  if (!/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/u.test(encoded)) {
    throw new Error(`Azure icon ${iconName} contains invalid base64`);
  }

  const bytes = Buffer.from(encoded, 'base64');
  const svg = bytes.toString('utf8');
  if (!/<svg(?:\s|>)/u.test(svg)) {
    throw new Error(`Azure icon ${iconName} does not decode to an SVG document`);
  }

  return bytes;
}

function renderResourceTypes(entries, icons) {
  const lines = [
    '// Généré depuis docs/design/strata/components/bundle.js — ne pas modifier.',
    'export interface AzureResourceTypeInfo {',
    '  readonly label: string;',
    '  readonly abbr: string;',
    '  readonly category: string;',
    '  readonly file: string | null;',
    '  readonly roadmap?: true;',
    '}',
    '',
    'export const RESOURCE_TYPES = {',
  ];

  for (const entry of entries) {
    const file = Object.hasOwn(icons, entry.icon) ? `${entry.icon}.svg` : null;
    const fields = [
      `label: ${JSON.stringify(entry.label)}`,
      `abbr: ${JSON.stringify(entry.abbr)}`,
      `category: ${JSON.stringify(entry.category)}`,
      `file: ${JSON.stringify(file)}`,
    ];
    if (entry.roadmap) fields.push('roadmap: true');
    lines.push(`  ${entry.type}: { ${fields.join(', ')} },`);
  }

  lines.push(
    '} as const satisfies Record<string, AzureResourceTypeInfo>;',
    '',
    'export type AzureResourceType = keyof typeof RESOURCE_TYPES;',
    "export type AzureResourceCategory = (typeof RESOURCE_TYPES)[AzureResourceType]['category'];",
    '',
  );

  return lines.join('\n');
}

export function buildAzureIconOutputs(source) {
  const icons = parseAzureIcons(source);
  const resourceTypes = parseResourceTypes(source);
  const files = new Map();

  for (const [name, dataUri] of Object.entries(icons)) {
    files.set(`${name}.svg`, decodeIcon(name, dataUri));
  }

  return {
    files,
    resourceTypes: renderResourceTypes(resourceTypes, icons),
  };
}

export async function extractAzureIcons({ root = defaultRoot } = {}) {
  const bundlePath = join(root, 'docs/design/strata/components/bundle.js');
  const publicDirectory = join(root, 'src/frontend/ifs-web/public/azure-icons');
  const typesPath = join(root, 'src/frontend/ifs-web/src/app/ds/resource-icon/resource-types.ts');
  const source = await readFile(bundlePath, 'utf8');
  const outputs = buildAzureIconOutputs(source);

  await mkdir(publicDirectory, { recursive: true });
  await Promise.all(
    [...outputs.files].map(([filename, contents]) =>
      writeFile(join(publicDirectory, filename), contents),
    ),
  );
  await mkdir(dirname(typesPath), { recursive: true });
  await writeFile(typesPath, outputs.resourceTypes, 'utf8');

  return { iconCount: outputs.files.size, resourceTypePath: typesPath };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  extractAzureIcons()
    .then(({ iconCount, resourceTypePath }) => {
      process.stdout.write(
        `Extracted ${iconCount} Azure SVG icons and resource metadata to ${resourceTypePath}\n`,
      );
    })
    .catch((error) => {
      process.stderr.write(`${error.message}\n`);
      process.exitCode = 1;
    });
}
