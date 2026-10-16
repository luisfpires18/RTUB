import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Icon } from './icons';
import { MemberFace } from './MemberDialogs';
import { membersApi, type TreeNode } from './membersApi';
import { loginTo } from './musicApi';

/**
 * /members/hierarchy - Hierarquia (React track 017; #223 tree). Signed-in members only. The Padrinho → Afilhado family
 * of Caloiros, Tunos, Veteranos and Tunossauros (never Leitões): roots by name, each member's afilhados by the month they
 * became Caloiro (MemberDirectoryService over IMemberHierarchyService; the rules live there, unchanged).
 *
 * One semantic tree (WAI-ARIA tree view: one tab stop, arrow keys, type-ahead) drawn with CSS only: left to right on
 * wide screens (each family in its own card), indented and collapsible on phones. Members with neither padrinho nor
 * afilhados are listed together below. Search, a selected path back to the root, and "Focar ramo" for deep branches.
 */

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

const memberHref = (id: string) => `/members?member=${encodeURIComponent(id)}`;
const WIDE = '(min-width: 900px)';
const fold = (s: string | null) => (s ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
const reducedMotion = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;

function indexTree(roots: TreeNode[]): Index {
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
function ancestorsOf(index: Index, id: string): string[] {
  const path: string[] = [];
  for (let p = index.parent.get(id); p; p = index.parent.get(p)) path.unshift(p);
  return path;
}

/** Wide screens open everything (94 members fit); phones open the first two levels, deeper branches stay folded. */
function initialExpanded(index: Index): Set<string> {
  const wide = window.matchMedia?.(WIDE).matches ?? true;
  return new Set(index.all.filter((n) => n.children.length > 0 && (wide || (index.depth.get(n.id) ?? 1) <= 2)).map((n) => n.id));
}

export default function MembersHierarchy() {
  const [tree, setTree] = useState<TreeNode[] | 'signin' | null>();

  useEffect(() => {
    document.title = 'Hierarquia · Membros · RTUB';
  }, []);

  const load = () => {
    setTree(undefined);
    membersApi.hierarchy().then((o) => setTree(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null));
  };
  useEffect(load, []);

  return (
    <section className="page wrap events-page hierarchy-page" aria-labelledby="hierarchy-title">
      <a className="back-link" href={portal.members}>
        <Icon name="arrow" />
        Membros
      </a>
      <header className="page__head">
        <p className="eyebrow">Área de membros</p>
        <h1 id="hierarchy-title" className="page__title">
          Hierarquia
        </h1>
        <p className="page__lead">A família da tuna: cada padrinho e os seus afilhados.</p>
      </header>

      {tree === undefined ? (
        <Loading label="A carregar a hierarquia…" />
      ) : tree === 'signin' ? (
        <div className="notice" role="status">
          <p>A hierarquia é da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.membersHierarchy)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : tree === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir a hierarquia agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={load}>
            Tentar novamente
          </button>
        </div>
      ) : tree.length === 0 ? (
        <p className="note">Nenhum membro encontrado.</p>
      ) : (
        <FamilyTree roots={tree} />
      )}
    </section>
  );
}

function FamilyTree({ roots }: { roots: TreeNode[] }) {
  const index = useMemo(() => indexTree(roots), [roots]);
  const [expanded, setExpanded] = useState(() => initialExpanded(index));
  const [selected, setSelected] = useState<string>();
  const [active, setActive] = useState(() => index.families[0]?.id ?? index.loose[0]?.id ?? '');
  const [focusRoot, setFocusRoot] = useState<string>();
  const [query, setQuery] = useState('');
  const pendingFocus = useRef(false);
  const typed = useRef({ text: '', at: 0 });
  const ids = { search: useId(), results: useId(), tree: useId() };

  const shownRoots = focusRoot ? [index.byId.get(focusRoot)!] : index.families;
  const path = useMemo(() => new Set(selected ? ancestorsOf(index, selected) : []), [index, selected]);

  /** The treeitems a sighted user sees, in order: what Up / Down / Home / End walk. */
  const visible = useMemo(() => {
    const out: string[] = [];
    const walk = (n: TreeNode) => {
      out.push(n.id);
      if (expanded.has(n.id)) n.children.forEach(walk);
    };
    shownRoots.forEach(walk);
    return out;
  }, [shownRoots, expanded]);

  // Move DOM focus only after a keyboard move or a search choice, never on load.
  useEffect(() => {
    if (!pendingFocus.current) return;
    pendingFocus.current = false;
    const el = document.getElementById(`hnode-${active}`) ?? document.getElementById(`hloose-${active}`);
    if (!el) return;
    el.focus({ preventScroll: true });
    // The member's own card (not its whole branch) must sit between the page header and the selected-path bar; only
    // when it does not, it is brought to the middle of the screen - arrow moves inside the view never scroll.
    const card = el.querySelector(':scope > .hnode .hnode__card') ?? el;
    const r = card.getBoundingClientRect();
    const top = (document.querySelector('.header')?.getBoundingClientRect().bottom ?? 0) + 8;
    const bottom = (document.querySelector('.htree-selected')?.getBoundingClientRect().top ?? window.innerHeight) - 8;
    if (r.top < top || r.bottom > bottom || r.left < 0 || r.right > document.documentElement.clientWidth) {
      card.scrollIntoView({ block: 'center', inline: 'nearest', behavior: reducedMotion() ? 'auto' : 'smooth' });
    }
  }, [active, expanded, focusRoot]);

  const moveTo = (id: string) => {
    pendingFocus.current = true;
    setActive(id);
    setSelected(id);
  };

  const toggle = (id: string, open?: boolean) => {
    const willOpen = open ?? !expanded.has(id);
    setExpanded((prev) => {
      const next = new Set(prev);
      if (willOpen) next.add(id);
      else next.delete(id);
      return next;
    });
    // A collapsed branch hides its descendants: keep the roving focus on a visible member.
    if (!willOpen && active !== id && ancestorsOf(index, active).includes(id)) setActive(id);
  };

  /** From the search or the path: open the way to the member, select it, bring it into view and focus it. */
  const choose = (id: string) => {
    setQuery('');
    const above = ancestorsOf(index, id);
    if (focusRoot && id !== focusRoot && !above.includes(focusRoot)) setFocusRoot(undefined);
    setExpanded((prev) => new Set([...prev, ...above]));
    moveTo(id);
  };

  const parentInView = (id: string) => {
    const p = index.parent.get(id);
    return p && (!focusRoot || id !== focusRoot) ? p : undefined;
  };

  const onKeyDown = (e: KeyboardEvent<HTMLUListElement>) => {
    const i = visible.indexOf(active);
    const node = index.byId.get(active);
    if (i < 0 || !node) return;
    const open = node.children.length > 0 && expanded.has(node.id);
    let next: string | undefined;
    switch (e.key) {
      case 'ArrowDown':
        next = visible[i + 1];
        break;
      case 'ArrowUp':
        next = visible[i - 1];
        break;
      case 'Home':
        next = visible[0];
        break;
      case 'End':
        next = visible[visible.length - 1];
        break;
      case 'ArrowRight':
        if (node.children.length > 0 && !open) {
          toggle(node.id, true);
          setSelected(node.id);
        } else if (open) next = node.children[0].id;
        break;
      case 'ArrowLeft':
        if (open) {
          toggle(node.id, false);
          setSelected(node.id);
        } else next = parentInView(node.id);
        break;
      case 'Enter':
        window.location.assign(memberHref(node.id));
        break;
      default: {
        if (e.key.length !== 1 || e.ctrlKey || e.metaKey || e.altKey || !/\S/.test(e.key)) return;
        // Type-ahead: the next visible member whose name starts with what was typed in the last half second.
        const now = Date.now();
        typed.current = { text: (now - typed.current.at < 500 ? typed.current.text : '') + fold(e.key), at: now };
        const order = [...visible.slice(i + 1), ...visible.slice(0, i + 1)];
        next = order.find((id) => fold(index.byId.get(id)!.displayName).startsWith(typed.current.text));
      }
    }
    e.preventDefault();
    if (next) moveTo(next);
  };

  const matches = useMemo(() => {
    const q = fold(query.trim());
    if (!q) return [];
    return index.all.filter((n) => fold(n.displayName).includes(q) || fold(n.fullName).includes(q)).slice(0, 8);
  }, [index, query]);

  const selectedNode = selected ? index.byId.get(selected) : undefined;
  const ctx: BranchContext = { expanded, active, selected, path, toggle, select: setSelected, setActive };

  return (
    <>
      <div className="htree-tools">
        <div className="htree-search" role="search">
          <label className="control" htmlFor={ids.search}>
            <span className="sr-only">Procurar membro na hierarquia</span>
            <Icon name="search" />
            <input
              id={ids.search}
              type="search"
              placeholder="Procurar membro"
              autoComplete="off"
              aria-controls={ids.results}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && matches[0]) {
                  e.preventDefault();
                  choose(matches[0].id);
                }
              }}
            />
          </label>
          <p className="sr-only" role="status" aria-live="polite">
            {query.trim() ? `${matches.length} ${matches.length === 1 ? 'membro encontrado' : 'membros encontrados'}` : ''}
          </p>
          {query.trim() && (
            <ul id={ids.results} className="htree-results">
              {matches.length === 0 ? (
                <li className="note">Nenhum membro com esse nome.</li>
              ) : (
                matches.map((n) => {
                  const root = ancestorsOf(index, n.id)[0];
                  return (
                    <li key={n.id}>
                      <button type="button" onClick={() => choose(n.id)}>
                        <strong>{n.displayName}</strong>
                        <small>
                          {[n.fullName, root ? `família de ${index.byId.get(root)!.displayName}` : n.children.length ? 'padrinho de família' : null]
                            .filter(Boolean)
                            .join(' · ')}
                        </small>
                      </button>
                    </li>
                  );
                })
              )}
            </ul>
          )}
        </div>
        <div className="htree-actions">
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => setExpanded(new Set(index.all.filter((n) => n.children.length).map((n) => n.id)))}>
            Expandir tudo
          </button>
          <button
            type="button"
            className="btn btn--ghost btn--sm"
            onClick={() => {
              setExpanded(new Set());
              setActive((a) => (focusRoot ? focusRoot : (ancestorsOf(index, a)[0] ?? a)));
            }}
          >
            Recolher tudo
          </button>
        </div>
      </div>
      <p className="note htree-summary">
        {index.all.length} membros · {index.families.length} {index.families.length === 1 ? 'família' : 'famílias'}
        {index.loose.length > 0 && ` · ${index.loose.length} sem ligações de padrinho`}
      </p>

      {focusRoot && (
        <div className="htree-focus" role="status">
          <span>
            A ver o ramo de <strong>{index.byId.get(focusRoot)!.displayName}</strong>
          </span>
          <button
            type="button"
            className="btn btn--ghost btn--sm"
            onClick={() => {
              setFocusRoot(undefined);
              pendingFocus.current = true;
            }}
          >
            <Icon name="arrowLeft" />
            Ver toda a hierarquia
          </button>
        </div>
      )}

      {shownRoots.length > 0 && (
        <ul role="tree" id={ids.tree} className="htree" aria-label="Hierarquia de padrinhos e afilhados" onKeyDown={onKeyDown}>
          {shownRoots.map((n, i) => (
            <Branch key={n.id} node={n} level={1} pos={i + 1} size={shownRoots.length} ctx={ctx} />
          ))}
        </ul>
      )}

      {!focusRoot && index.loose.length > 0 && (
        <section className="hloose" aria-labelledby={`${ids.tree}-loose`}>
          <h2 id={`${ids.tree}-loose`} className="hloose__title">
            Sem ligações de padrinho <span className="note">({index.loose.length})</span>
          </h2>
          <p className="note">Membros sem padrinho nem afilhados registados na hierarquia.</p>
          <ul className="hloose__list">
            {index.loose.map((n) => (
              <li key={n.id}>
                <a
                  id={`hloose-${n.id}`}
                  className={selected === n.id ? 'hloose__member is-selected' : 'hloose__member'}
                  href={memberHref(n.id)}
                  aria-current={selected === n.id ? 'true' : undefined}
                >
                  <MemberFace avatarUrl={n.avatarUrl} size={28} />
                  <span>
                    <strong>{n.displayName}</strong>
                    {n.fullName && <small>{n.fullName}</small>}
                  </span>
                </a>
              </li>
            ))}
          </ul>
        </section>
      )}

      {selectedNode && (
        <div className="htree-selected" role="region" aria-label="Membro selecionado">
          <nav aria-label="Caminho até ao padrinho" className="htree-crumbs">
            <ol>
              {ancestorsOf(index, selectedNode.id).map((a) => (
                <li key={a}>
                  <button type="button" onClick={() => choose(a)}>
                    {index.byId.get(a)!.displayName}
                  </button>
                </li>
              ))}
              <li aria-current="true">
                <strong>{selectedNode.displayName}</strong>
              </li>
            </ol>
          </nav>
          <div className="htree-selected__actions">
            {selectedNode.children.length > 0 && focusRoot !== selectedNode.id && (
              <button
                type="button"
                className="btn btn--ghost btn--sm"
                onClick={() => {
                  setFocusRoot(selectedNode.id);
                  setExpanded((prev) => new Set([...prev, selectedNode.id]));
                  setActive(selectedNode.id);
                  pendingFocus.current = true;
                }}
              >
                <Icon name="diagram" />
                Focar ramo
              </button>
            )}
            <a className="btn btn--primary btn--sm" href={memberHref(selectedNode.id)}>
              <Icon name="person" />
              Ver membro
            </a>
            <button type="button" className="icon-btn icon-btn--sm" onClick={() => setSelected(undefined)}>
              <Icon name="close" />
              <span className="sr-only">Limpar a seleção</span>
            </button>
          </div>
        </div>
      )}
    </>
  );
}

type BranchContext = {
  expanded: Set<string>;
  active: string;
  selected?: string;
  path: Set<string>;
  toggle: (id: string, open?: boolean) => void;
  select: (id: string) => void;
  setActive: (id: string) => void;
};

/**
 * One member (treeitem). The card links to the member, as before; the separate round button opens or folds the
 * afilhados. Neither is a tab stop: the tree is one, and arrows move inside it.
 */
function Branch({ node, level, pos, size, ctx }: { node: TreeNode; level: number; pos: number; size: number; ctx: BranchContext }) {
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
          <MemberFace avatarUrl={node.avatarUrl} size={32} />
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
