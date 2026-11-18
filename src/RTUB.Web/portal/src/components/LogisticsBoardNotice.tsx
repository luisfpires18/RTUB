// The notice shown when a board cannot be opened.
import { Icon } from './icons';
import { loginTo } from '../data/content';
import { BackLink } from './ui/BackLink';

/** Why a board did not open: sign in, no access, missing, or a failure with a retry. */
export function BoardNotice({ board, boardId, retry }: { board: 'signin' | 'forbidden' | 'missing' | null; boardId: number; retry: () => void }) {
  return (
    <section className="page wrap events-page lx-page">
      <BackLink href="/logistics">Quadros</BackLink>
      <div className="notice" role="status">
        {board === 'signin' ? (
          <>
            <p>A logística é da área de membros.</p>
            <a className="btn btn--primary btn--sm" href={loginTo(`/logistics/${boardId}${location.search}`)}>
              <Icon name="login" />
              Entrar
            </a>
          </>
        ) : board === 'forbidden' ? (
          <p>Não tem acesso a este quadro.</p>
        ) : board === 'missing' ? (
          <p>Este quadro não existe ou não tem acesso a ele.</p>
        ) : (
          <>
            <p>Não conseguimos abrir o quadro agora.</p>
            <button type="button" className="btn btn--ghost btn--sm" onClick={retry}>
              Tentar novamente
            </button>
          </>
        )}
      </div>
    </section>
  );
}
