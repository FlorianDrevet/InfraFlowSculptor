import type { RevisionFileResponse } from '../../core/api/generated';

export interface RevisionFileTreeNode {
  name: string;
  path: string;
  file: RevisionFileResponse | null;
  children: RevisionFileTreeNode[];
}

interface MutableRevisionFileTreeNode {
  name: string;
  path: string;
  file: RevisionFileResponse | null;
  children: Map<string, MutableRevisionFileTreeNode>;
}

export function buildRevisionFileTree(
  files: readonly RevisionFileResponse[],
): RevisionFileTreeNode[] {
  const roots = new Map<string, MutableRevisionFileTreeNode>();
  for (const file of files) {
    const parts = file.relativePath.split('/').filter(Boolean);
    let siblings = roots;
    let parentPath = '';
    for (const [index, part] of parts.entries()) {
      const path = parentPath ? `${parentPath}/${part}` : part;
      let node = siblings.get(part);
      if (!node) {
        node = { name: part, path, file: null, children: new Map() };
        siblings.set(part, node);
      }
      if (index === parts.length - 1) node.file = file;
      siblings = node.children;
      parentPath = path;
    }
  }

  const convert = (nodes: Map<string, MutableRevisionFileTreeNode>): RevisionFileTreeNode[] =>
    [...nodes.values()]
      .sort((left, right) => {
        if (Boolean(left.file) !== Boolean(right.file)) return left.file ? 1 : -1;
        return left.name.localeCompare(right.name, 'en');
      })
      .map((node) => ({
        name: node.name,
        path: node.path,
        file: node.file,
        children: convert(node.children),
      }));

  return convert(roots);
}
