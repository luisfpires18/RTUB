// One recursive tree item (padrinho and afilhados) of the family tree.
import { Icon } from './icons';
import { type TreeNode } from '../data/membersApi';
import { memberHref } from '../data/hierarchyTree';
import { Avatar } from './ui/Avatar';

export type BranchContext = {
  expanded: Set<string>;
  active: string;
  selected?: string;
  path: Set<string>;
  toggle: (id: string, open?: boolean) => void;
  select: (id: string) => void;
  setActive: (id: string) => void;
};

/**
 * One member (treeitem). The card links to the member; the separate round button opens or folds the
 * afilhados. Neither is a tab stop: the tree is one, and arrows move inside it.
 */
export function Branch({ node, level, pos, size, ctx }: { node: TreeNode; level: number; pos: number; size: number; ctx: BranchContext }) {
  const count = node.children.length;
  const open = count > 0 && ctx.expanded.has(node.id);
  const classes = ['hnode-item'];
  if (level === 1) classes.push('hnode-item--root');
  if (open) classes.push('is-open');
  if (ctx.selected === node.id) classes.push('is-selected');
  if (ctx.path.has(node.id)) classes.push('is-path');

  return (
    <li
      role="treeitem"
      id={`hnode-${node.id}`}
      className={classes.join(' ')}
      aria-level={level}
      aria-setsize={size}
      aria-posinset={pos}
      aria-expanded={count > 0 ? open : undefined}
      aria-selected={ctx.selected === node.id}
      aria-labelledby={`hname-${node.id}`}
      aria-describedby={node.fullName ? `hfull-${node.id}` : undefined}
      tabIndex={ctx.active === node.id ? 0 : -1}
      onFocus={(e) => {
        if (e.target === e.currentTarget) ctx.setActive(node.id);
      }}
    >
      <div className="hnode">
        <a className="hnode__card" href={memberHref(node.id)} tabIndex={-1}>
          <Avatar className="who__avatar" src={node.avatarUrl} size={32} />
          <span className="hnode__who">
            <strong id={`hname-${node.id}`}>{node.displayName}</strong>
            {node.fullName && <small id={`hfull-${node.id}`}>{node.fullName}</small>}
          </span>
        </a>
        {count > 0 && (
          <button
            type="button"
            className="hnode__toggle"
            tabIndex={-1}
            aria-label={`${open ? 'Recolher' : 'Mostrar'} ${count === 1 ? 'o afilhado' : `os ${count} afilhados`} de ${node.displayName}`}
            onClick={() => {
              ctx.toggle(node.id);
              ctx.select(node.id);
              ctx.setActive(node.id);
            }}
          >
            <span aria-hidden="true">{count}</span>
            <Icon name="chevronRight" />
          </button>
        )}
      </div>
      {open && (
        <ul role="group" className="hnode__children">
          {node.children.map((c, i) => (
            <Branch key={c.id} node={c} level={level + 1} pos={i + 1} size={count} ctx={ctx} />
          ))}
        </ul>
      )}
    </li>
  );
}
