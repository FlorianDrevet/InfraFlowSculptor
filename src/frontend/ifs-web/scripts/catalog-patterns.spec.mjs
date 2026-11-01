import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { describe, expect, it } from 'vitest';

const catalogRoot = join(process.cwd(), '../../backend/InfraFlowSculptor.Catalog/catalog');
const reservedSqlLogins = [
  'admin',
  'administrator',
  'sa',
  'root',
  'dbmanager',
  'loginmanager',
  'guest',
  'public',
];

function collectPatterns(value, descriptorPath, patterns = []) {
  if (Array.isArray(value)) {
    value.forEach((item) => collectPatterns(item, descriptorPath, patterns));
  } else if (value !== null && typeof value === 'object') {
    if (typeof value.pattern === 'string') patterns.push({ descriptorPath, pattern: value.pattern });
    Object.values(value).forEach((item) => collectPatterns(item, descriptorPath, patterns));
  }

  return patterns;
}

const descriptorPaths = readdirSync(catalogRoot, { withFileTypes: true })
  .filter((entry) => entry.isDirectory() && existsSync(join(catalogRoot, entry.name, 'catalog.json')))
  .flatMap((version) => {
    const typesDirectory = join(catalogRoot, version.name, 'types');
    return readdirSync(typesDirectory)
      .filter((file) => file.endsWith('.json'))
      .map((file) => join(typesDirectory, file));
  });

const patterns = descriptorPaths.flatMap((descriptorPath) =>
  collectPatterns(JSON.parse(readFileSync(descriptorPath, 'utf8')), descriptorPath),
);

describe('catalog property patterns', () => {
  it.each(patterns)('is valid in the browser regex engine: $descriptorPath ($pattern)', ({ pattern }) => {
    expect(() => new RegExp(pattern)).not.toThrow();
  });

  it('rejects the explicitly documented SQL administrator login names regardless of case', () => {
    const sqlDescriptors = descriptorPaths
      .filter((descriptorPath) => basename(descriptorPath) === 'SqlServer.json')
      .map((descriptorPath) => JSON.parse(readFileSync(descriptorPath, 'utf8')));
    const loginPatterns = sqlDescriptors
      .flatMap((descriptor) => descriptor.properties)
      .filter((property) => property.name === 'administratorLogin' && typeof property.pattern === 'string')
      .map((property) => new RegExp(property.pattern));

    expect(loginPatterns).not.toHaveLength(0);
    for (const pattern of loginPatterns) {
      expect(pattern.source).not.toContain('(?i:');
      for (const login of reservedSqlLogins) {
        expect(pattern.test(login)).toBe(false);
        expect(pattern.test(login.toUpperCase())).toBe(false);
      }
      expect(pattern.test('sqladmin')).toBe(true);
      expect(pattern.test('sqladmin\n')).toBe(false);
    }
  });
});
