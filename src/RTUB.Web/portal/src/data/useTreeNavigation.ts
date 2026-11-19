import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import type { TreeNode } from './membersApi';
import type { BranchContext } from '../components/HierarchyBranch';
import { ancestorsOf, fold, indexTree, initialExpanded, memberHref, reducedMotion } from './hierarchyTree';

/** Letters typed within this long of each other extend one type-ahead search. */
const TYPE_AHEAD_WINDOW_MS = 500;
const MAX_SEARCH_RESULTS = 8;
/** Gap kept between a focused member and the header above / the selected-path bar below. */
const VIEW_MARGIN_PX = 8;

/** State, keyboard navigation and search of the family tree (WAI-ARIA tree: roving focus, Up / Down / Left / Right / Home / End, type-ahead). */
export function useTreeNavigation(roots: TreeNode[]) {
  const index = useMemo(() => indexTree(roots), [roots]);
  const [expanded, setExpanded] = useState(() => initialExpanded(index));
  const [selected, setSelected] = useState<string>();
  const [active, setActive] = useState(() => index.families[0]?.id ?? index.loose[0]?.id ?? '');
  const [focusRoot, setFocusRoot] = useState<string>();
  const [query, setQuery] = useState('');
  const pendingFocus = useRef(false);
  const typed = useRef({ text: '', at: 0 });

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
    const top = (document.querySelector('.header')?.getBoundingClientRect().bottom ?? 0) + VIEW_MARGIN_PX;
    const bottom = (document.querySelector('.htree-selected')?.getBoundingClientRect().top ?? window.innerHeight) - VIEW_MARGIN_PX;
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
        typed.current = { text: (now - typed.current.at < TYPE_AHEAD_WINDOW_MS ? typed.current.text : '') + fold(e.key), at: now };
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
    return index.all.filter((n) => fold(n.displayName).includes(q) || fold(n.fullName).includes(q)).slice(0, MAX_SEARCH_RESULTS);
  }, [index, query]);

  const selectedNode = selected ? index.byId.get(selected) : undefined;
  const ctx: BranchContext = { expanded, active, selected, path, toggle, select: setSelected, setActive };

  const expandAll = () => setExpanded(new Set(index.all.filter((n) => n.children.length).map((n) => n.id)));
  const collapseAll = () => {
    setExpanded(new Set());
    setActive((a) => (focusRoot ? focusRoot : (ancestorsOf(index, a)[0] ?? a)));
  };
  const showWholeTree = () => {
    setFocusRoot(undefined);
    pendingFocus.current = true;
  };
  const focusBranch = (id: string) => {
    setFocusRoot(id);
    setExpanded((prev) => new Set([...prev, id]));
    setActive(id);
    pendingFocus.current = true;
  };
  const clearSelection = () => setSelected(undefined);

  return {
    index, selected, selectedNode, focusRoot, query, setQuery, shownRoots, matches, ctx,
    onKeyDown, choose, expandAll, collapseAll, showWholeTree, focusBranch, clearSelection,
  };
}
