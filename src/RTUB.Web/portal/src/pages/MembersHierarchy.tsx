import { useEffect, useState } from 'react';
import { EmptyNote } from '../components/ui/EmptyNote';
import { BackLink } from '../components/ui/BackLink';
import { portal } from '../data/content';
import { membersApi, type TreeNode } from '../data/membersApi';
import { RetryNotice, SignInNotice } from '../components/Notice';
import { FamilyTree } from '../components/FamilyTree';
import { Loading } from '../components/ui/Loading';
import { PageHead } from '../components/ui/PageHead';

/**
 * /members/hierarchy - Hierarquia (tree). Signed-in members only. The Padrinho → Afilhado family
 * of Caloiros, Tunos, Veteranos and Tunossauros (never Leitões): roots by name, each member's afilhados by the month they
 * became Caloiro (MemberDirectoryService over IMemberHierarchyService; the rules live there, unchanged).
 *
 * One semantic tree (WAI-ARIA tree view: one tab stop, arrow keys, type-ahead) drawn with CSS only: left to right on
 * wide screens (each family in its own card), indented and collapsible on phones. Members with neither padrinho nor
 * afilhados are listed together below. Search, a selected path back to the root, and "Focar ramo" for deep branches.
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
    <section className="page wrap events-page hierarchy-page" aria-labelledby="hierarchy-title">
      <BackLink href={portal.members}>Membros</BackLink>
      <PageHead plain eyebrow="Área de membros" title="Hierarquia" titleId="hierarchy-title" lead="A família da tuna: cada padrinho e os seus afilhados." />

      {tree === undefined ? (
        <Loading label="A carregar a hierarquia…" />
      ) : tree === 'signin' ? (
        <SignInNotice path={portal.membersHierarchy}>A hierarquia é da área de membros.</SignInNotice>
      ) : tree === null ? (
        <RetryNotice onRetry={load}>Não conseguimos abrir a hierarquia agora.</RetryNotice>
      ) : tree.length === 0 ? (
        <EmptyNote>Nenhum membro encontrado.</EmptyNote>
      ) : (
        <FamilyTree roots={tree} />
      )}
    </section>
  );
}
