import { StrictMode, Suspense, lazy } from 'react';
import { createRoot } from 'react-dom/client';
import { ErrorBoundary, Layout, Loading } from './App';
import { Home } from './Home';
import './styles.css';

// The server maps exactly these paths here (Program.cs), so choosing the page once at boot is all
// the routing the portal needs. Links between them are ordinary full navigations; a client router
// is a decision for when routes stop being a short, fixed list.
const pages: Record<string, ReturnType<typeof lazy>> = {
  '/privacy': lazy(() => import('./Privacy')),
  '/profile': lazy(() => import('./Profile')),
  '/request': lazy(() => import('./Request')),
  '/music': lazy(() => import('./Music')),
  '/login': lazy(() => import('./Login')),
};
const path = location.pathname.replace(/\/+$/, '');
const Page = pages[path];

// /music/albums/{id} carries its id; Program.cs maps it with an {id:int} constraint.
const MusicAlbum = lazy(() => import('./MusicAlbum'));
const albumId = /^\/music\/albums\/(\d+)$/.exec(path)?.[1];

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Layout>
      <ErrorBoundary>
        {Page || albumId ? (
          <Suspense
            fallback={
              <div className="wrap">
                <Loading label="A carregar a página…" />
              </div>
            }
          >
            {albumId ? <MusicAlbum albumId={Number(albumId)} /> : <Page />}
          </Suspense>
        ) : (
          <Home />
        )}
      </ErrorBoundary>
    </Layout>
  </StrictMode>,
);
