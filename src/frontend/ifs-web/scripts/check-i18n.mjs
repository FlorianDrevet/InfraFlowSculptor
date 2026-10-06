import { readFile, readdir } from 'node:fs/promises';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));
const localeDirectory = join(root, 'public', 'i18n');
const appDirectory = join(root, 'src', 'app');

function translationKeys(value, prefix = '') {
  return Object.entries(value).flatMap(([key, child]) => {
    const fullKey = prefix ? `${prefix}.${key}` : key;
    return typeof child === 'string' ? [fullKey] : translationKeys(child, fullKey);
  });
}

async function sourceFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const nested = await Promise.all(
    entries.map((entry) => {
      const path = join(directory, entry.name);
      if (entry.isDirectory()) {
        return sourceFiles(path);
      }
      return /\.(html|ts)$/.test(entry.name) ? [path] : [];
    }),
  );
  return nested.flat();
}

const locales = {};
for (const language of ['fr', 'en']) {
  locales[language] = JSON.parse(await readFile(join(localeDirectory, `${language}.json`), 'utf8'));
}

const frenchKeys = translationKeys(locales.fr).sort();
const englishKeys = translationKeys(locales.en).sort();
const errors = [];
if (JSON.stringify(frenchKeys) !== JSON.stringify(englishKeys)) {
  errors.push('fr.json and en.json do not contain the same translation keys.');
}

const usedKeys = new Set();
const source = (await Promise.all((await sourceFiles(appDirectory)).map((path) => readFile(path, 'utf8')))).join('\n');
const keyPattern = /['"]([\w.-]+)['"]\s*\|\s*transloco\b|\bt\(\s*['"]([\w.-]+)['"]/g;
for (const match of source.matchAll(keyPattern)) {
  usedKeys.add(match[1] ?? match[2]);
}

for (const key of frenchKeys) {
  if (!usedKeys.has(key)) {
    errors.push(`Translation key is unused: ${key}`);
  }
}
for (const key of usedKeys) {
  if (!frenchKeys.includes(key)) {
    errors.push(`Translation key is missing from fr.json and en.json: ${key}`);
  }
}

if (errors.length > 0) {
  for (const error of errors) {
    process.stderr.write(`i18n: ${error}\n`);
  }
  process.exitCode = 1;
} else {
  process.stdout.write(`i18n: ${frenchKeys.length} translated keys are present in fr/en and used.\n`);
}
