import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, relative, resolve, sep } from 'node:path';
import { normalizeGeneratedApi } from './normalize-generated-api.mjs';

const projectRoot = process.cwd();
const committedDirectory = resolve(projectRoot, 'src/app/core/api/generated');
const temporaryRoot = resolve(tmpdir());
const temporaryDirectory = mkdtempSync(join(temporaryRoot, 'ifs-api-check-'));
const generatorEntryPoint = resolve(projectRoot, 'node_modules/ng-openapi-gen/lib/index.js');

function listFiles(directory, root = directory) {
  return readdirSync(directory, { withFileTypes: true })
    .flatMap((entry) => {
      const entryPath = join(directory, entry.name);
      if (entry.isDirectory()) {
        return listFiles(entryPath, root);
      }

      return [relative(root, entryPath).split(sep).join('/')];
    })
    .sort();
}

try {
  const result = spawnSync(
    process.execPath,
    [generatorEntryPoint, '--config', 'ng-openapi-gen.json', '--output', temporaryDirectory],
    { cwd: projectRoot, encoding: 'utf8' },
  );

  if (result.stdout) {
    process.stdout.write(result.stdout);
  }
  if (result.stderr) {
    process.stderr.write(result.stderr);
  }
  if (result.error) {
    throw result.error;
  }
  if (result.status !== 0) {
    process.exitCode = result.status ?? 1;
  } else {
    normalizeGeneratedApi(temporaryDirectory);
    const committedFiles = listFiles(committedDirectory);
    const generatedFiles = listFiles(temporaryDirectory);
    const fileNames = [...new Set([...committedFiles, ...generatedFiles])].sort();
    const differences = fileNames.filter((fileName) => {
      if (!committedFiles.includes(fileName) || !generatedFiles.includes(fileName)) {
        return true;
      }

      return !readFileSync(join(committedDirectory, fileName)).equals(
        readFileSync(join(temporaryDirectory, fileName)),
      );
    });

    if (differences.length > 0) {
      console.error(`Client OpenAPI obsolète : ${differences.join(', ')}`);
      console.error('Exécutez npm run api:generate puis commitez le résultat.');
      process.exitCode = 1;
    } else {
      console.log('Client OpenAPI à jour.');
    }
  }
} finally {
  if (dirname(temporaryDirectory) === temporaryRoot) {
    rmSync(temporaryDirectory, { recursive: true, force: true });
  }
}
