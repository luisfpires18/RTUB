import { useEffect, useState } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Icon } from './icons';
import { MemberFace } from './MemberDialogs';
import { membersApi, type TreeNode } from './membersApi';
import { loginTo } from './musicApi';

/**
 * /members/hierarchy - Hierarquia (React track 017; was the Blazor /hierarchy). Signed-in members only. The
 * Padrinho → Afilhado family of Caloiros, Tunos, Veteranos and Tunossauros (never Leitões): roots by name, each
 * member's afilhados by the month they became Caloiro (MemberDirectoryService over the old IMemberHierarchyService).
 */
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
    <section className="page wrap events-page" aria-labelledby="hierarchy-title">
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
        <ul className="family">
          {tree.map((n) => (
            <Branch key={n.id} node={n} />
          ))}
        </ul>
      )}
    </section>
  );
}

function Branch({ node }: { node: TreeNode }) {
  return (
    <li className="family__branch">
      <a className="family__person" href={`/members?member=${encodeURIComponent(node.id)}`}>
        <MemberFace avatarUrl={node.avatarUrl} size={48} />
        <span className="member-row__who">
          <strong>{node.displayName}</strong>
          {node.fullName && <small>{node.fullName}</small>}
          {node.children.length > 0 && <small>{node.children.length === 1 ? '1 afilhado' : `${node.children.length} afilhados`}</small>}
        </span>
      </a>
      {node.children.length > 0 && (
        <ul className="family__children">
          {node.children.map((c) => (
            <Branch key={c.id} node={c} />
          ))}
        </ul>
      )}
    </li>
  );
}
