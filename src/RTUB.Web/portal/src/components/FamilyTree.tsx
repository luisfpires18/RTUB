// The family tree widget of /members/hierarchy: search, keyboard navigation, selected branch.
import { useId } from 'react';
import { Icon } from './icons';
import { type TreeNode } from '../data/membersApi';
import { Branch } from './HierarchyBranch';
import { LooseMembers, TreeSearch, TreeSelectedBar } from './FamilyTreeParts';
import { useTreeNavigation } from '../data/useTreeNavigation';

export function FamilyTree({ roots }: { roots: TreeNode[] }) {
  const {
    index, selected, selectedNode, focusRoot, query, setQuery, shownRoots, matches, ctx,
    onKeyDown, choose, expandAll, collapseAll, showWholeTree, focusBranch, clearSelection,
  } = useTreeNavigation(roots);
  const ids = { search: useId(), results: useId(), tree: useId() };

  return (
    <>
      <div className="htree-tools">
        <TreeSearch inputId={ids.search} resultsId={ids.results} query={query} onQuery={setQuery} matches={matches} index={index} choose={choose} />
        <div className="htree-actions">
          <button type="button" className="btn btn--ghost btn--sm" onClick={expandAll}>
            Expandir tudo
          </button>
          <button type="button" className="btn btn--ghost btn--sm" onClick={collapseAll}>
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
          <button type="button" className="btn btn--ghost btn--sm" onClick={showWholeTree}>
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

      {!focusRoot && index.loose.length > 0 && <LooseMembers members={index.loose} titleId={`${ids.tree}-loose`} selected={selected} />}

      {selectedNode && (
        <TreeSelectedBar
          index={index}
          node={selectedNode}
          focusRoot={focusRoot}
          choose={choose}
          onFocusBranch={() => focusBranch(selectedNode.id)}
          onClear={clearSelection}
        />
      )}
    </>
  );
}
