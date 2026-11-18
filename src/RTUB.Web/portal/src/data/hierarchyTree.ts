// Pure helpers for the /members/hierarchy tree: index, ancestors, initial expansion, link and text folding.
import { type TreeNode } from './membersApi';

type Index = {
  byId: Map<string, TreeNode>;
  parent: Map<string, string>;
  depth: Map<string, number>;
  /** Roots with afilhados: one card each. */
  families: TreeNode[];
  /** Roots with neither padrinho nor afilhados (still roots; only shown together). */
  loose: TreeNode[];
  all: TreeNode[];
};

export const memberHref = (id: string) => `/members?member=${encodeURIComponent(id)}`;

const WIDE = '(min-width: 900px)';

export const fold = (s: string | null) => (s ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

export const reducedMotion = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;

export function indexTree(roots: TreeNode[]): Index {
  const index: Index = { byId: new Map(), parent: new Map(), depth: new Map(), families: [], loose: [], all: [] };
  const visit = (node: TreeNode, depth: number, parent?: string) => {
    index.byId.set(node.id, node);
    index.depth.set(node.id, depth);
    if (parent) index.parent.set(node.id, parent);
    index.all.push(node);
    node.children.forEach((c) => visit(c, depth + 1, node.id));
  };
  roots.forEach((r) => {
    visit(r, 1);
    (r.children.length > 0 ? index.families : index.loose).push(r);
  });
  return index;
}

/** The ids above a member, from its root down (the member excluded). */
export function ancestorsOf(index: Index, id: string): string[] {
  const path: string[] = [];
  for (let p = index.parent.get(id); p; p = index.parent.get(p)) path.unshift(p);
  return path;
}

/** Wide screens open everything (94 members fit); phones open the first two levels, deeper branches stay folded. */
export function initialExpanded(index: Index): Set<string> {
  const wide = window.matchMedia?.(WIDE).matches ?? true;
  return new Set(index.all.filter((n) => n.children.length > 0 && (wide || (index.depth.get(n.id) ?? 1) <= 2)).map((n) => n.id));
}
