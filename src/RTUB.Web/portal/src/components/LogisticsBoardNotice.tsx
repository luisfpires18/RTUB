// The notice shown when a board cannot be opened.
import { Notice, RetryNotice, SignInNotice } from './Notice';
import { BackLink } from './ui/BackLink';

/** Why a board did not open: sign in, no access, missing, or a failure with a retry. */
export function BoardNotice({ board, boardId, retry }: { board: 'signin' | 'forbidden' | 'missing' | null; boardId: number; retry: () => void }) {
  return (
    <section className="page wrap events-page lx-page">
      <BackLink href="/logistics">Quadros</BackLink>
      {board === 'signin' ? (
        <SignInNotice path={`/logistics/${boardId}${location.search}`}>A logística é da área de membros.</SignInNotice>
      ) : board === 'forbidden' ? (
        <Notice>Não tem acesso a este quadro.</Notice>
      ) : board === 'missing' ? (
        <Notice>Este quadro não existe ou não tem acesso a ele.</Notice>
      ) : (
        <RetryNotice onRetry={retry}>Não conseguimos abrir o quadro agora.</RetryNotice>
      )}
    </section>
  );
}
