import { Component, useEffect, type ReactNode } from 'react';
import { portal } from '../data/content';
import { resetStartup, StartupModals } from './Startup';
import { useCurrentUser } from './AppShared';
import { useSession, revealApp } from './AppLaunch';
import { Header, Footer } from './AppChrome';
import { SystemAnnouncement } from './AppAnnouncement';
import { EmptyState } from './ui/EmptyState';


export function Layout({ children }: { children: ReactNode }) {
  useEffect(revealApp, []);
  const session = useSession();
  const shell = session.kind !== 'visitor';
  const member = session.kind === 'member' ? session.user : undefined;

  return (
    <div className={shell ? 'shell shell--member' : 'shell'}>
      <a className="skip" href="#conteudo">Saltar para o conteúdo</a>
      <Header shell={shell} member={member} />
      <div className="shell__body">
        <main id="conteudo" tabIndex={-1}>
          {children}
        </main>
        <Footer />
      </div>
      <SystemAnnouncement />
      {member && <StartupModals />}
      <StartupReset />
    </div>
  );
}

/** A confirmed visitor (signed out, or on /login before signing in or switching account) starts a new launch. */
function StartupReset() {
  const { user } = useCurrentUser();
  useEffect(() => {
    if (user && !user.authenticated) resetStartup();
  }, [user]);
  return null;
}

/** Catches render errors and failed lazy chunks (e.g. offline) with a way out. */
export class ErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  render() {
    if (!this.state.failed) return this.props.children;
    return (
      <EmptyState
        title="Não foi possível carregar esta página."
        className="wrap"
        role="alert"
        actions={
          <>
            <button type="button" className="btn btn--primary" onClick={() => location.reload()}>
              Tentar novamente
            </button>
            <a className="btn btn--ghost" href={portal.home}>
              Voltar ao início
            </a>
          </>
        }
      >
        <p>Verifica a ligação e tenta outra vez.</p>
      </EmptyState>
    );
  }
}
