// Pieces of the family tree widget: the search box, the members without links, the selected-member bar.
import { Icon } from './icons';
import { IconAction } from './ui/IconAction';
import { Avatar } from './ui/Avatar';
import type { TreeNode } from '../data/membersApi';
import { ancestorsOf, indexTree, memberHref } from '../data/hierarchyTree';

type TreeIndex = ReturnType<typeof indexTree>;

const LOOSE_AVATAR_PX = 28;

export function TreeSearch({
  inputId,
  resultsId,
  query,
  onQuery,
  matches,
  index,
  choose,
}: {
  inputId: string;
  resultsId: string;
  query: string;
  onQuery: (query: string) => void;
  matches: TreeNode[];
  index: TreeIndex;
  choose: (id: string) => void;
}) {
  return (
    <div className="htree-search" role="search">
      <label className="control" htmlFor={inputId}>
        <span className="sr-only">Procurar membro na hierarquia</span>
        <Icon name="search" />
        <input
          id={inputId}
          type="search"
          placeholder="Procurar membro"
          autoComplete="off"
          aria-controls={resultsId}
          value={query}
          onChange={(e) => onQuery(e.target.value)}
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
        <ul id={resultsId} className="htree-results">
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
  );
}

/** Members with no godfather and no godchildren: listed apart, not drawn as one-node trees. */
export function LooseMembers({ members, titleId, selected }: { members: TreeNode[]; titleId: string; selected: string | undefined }) {
  return (
    <section className="hloose" aria-labelledby={titleId}>
      <h2 id={titleId} className="hloose__title">
        Sem ligações de padrinho <span className="note">({members.length})</span>
      </h2>
      <p className="note">Membros sem padrinho nem afilhados registados na hierarquia.</p>
      <ul className="hloose__list">
        {members.map((n) => (
          <li key={n.id}>
            <a
              id={`hloose-${n.id}`}
              className={selected === n.id ? 'hloose__member is-selected' : 'hloose__member'}
              href={memberHref(n.id)}
              aria-current={selected === n.id ? 'true' : undefined}
            >
              <Avatar className="who__avatar" src={n.avatarUrl} size={LOOSE_AVATAR_PX} />
              <span>
                <strong>{n.displayName}</strong>
                {n.fullName && <small>{n.fullName}</small>}
              </span>
            </a>
          </li>
        ))}
      </ul>
    </section>
  );
}

/** The sticky bar for the selected member: path from the root, focus the branch, open the member, clear. */
export function TreeSelectedBar({
  index,
  node,
  focusRoot,
  choose,
  onFocusBranch,
  onClear,
}: {
  index: TreeIndex;
  node: TreeNode;
  focusRoot: string | undefined;
  choose: (id: string) => void;
  onFocusBranch: () => void;
  onClear: () => void;
}) {
  return (
    <div className="htree-selected" role="region" aria-label="Membro selecionado">
      <nav aria-label="Caminho até ao padrinho" className="htree-crumbs">
        <ol>
          {ancestorsOf(index, node.id).map((a) => (
            <li key={a}>
              <button type="button" onClick={() => choose(a)}>
                {index.byId.get(a)!.displayName}
              </button>
            </li>
          ))}
          <li aria-current="true">
            <strong>{node.displayName}</strong>
          </li>
        </ol>
      </nav>
      <div className="htree-selected__actions">
        {node.children.length > 0 && focusRoot !== node.id && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={onFocusBranch}>
            <Icon name="diagram" />
            Focar ramo
          </button>
        )}
        <a className="btn btn--primary btn--sm" href={memberHref(node.id)}>
          <Icon name="person" />
          Ver membro
        </a>
        <IconAction icon="close" label="Limpar a seleção" onClick={onClear} />
      </div>
    </div>
  );
}
