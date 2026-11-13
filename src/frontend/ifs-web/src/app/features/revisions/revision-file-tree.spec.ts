import { describe, expect, it } from 'vitest';
import { buildRevisionFileTree } from './revision-file-tree';

describe('buildRevisionFileTree', () => {
  it('groups paths into a sorted directory tree while retaining file metadata', () => {
    const files = [
      { relativePath: 'src/main.bicep', sha256: 'a', size: 12 },
      { relativePath: 'README.md', sha256: 'b', size: 4 },
      { relativePath: 'src/params/dev.bicepparam', sha256: 'c', size: 8 },
    ];

    expect(buildRevisionFileTree(files)).toEqual([
      {
        name: 'src',
        path: 'src',
        file: null,
        children: [
          {
            name: 'params',
            path: 'src/params',
            file: null,
            children: [
              {
                name: 'dev.bicepparam',
                path: 'src/params/dev.bicepparam',
                file: files[2],
                children: [],
              },
            ],
          },
          { name: 'main.bicep', path: 'src/main.bicep', file: files[0], children: [] },
        ],
      },
      { name: 'README.md', path: 'README.md', file: files[1], children: [] },
    ]);
  });
});
