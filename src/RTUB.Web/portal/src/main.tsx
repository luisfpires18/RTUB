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
};
const Page = pages[location.pathname.replace(/\/+$/, '')];

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Layout>
      <ErrorBoundary>
        {Page ? (
          <Suspense
            fallback={
              <div className="wrap">
                <Loading label="A carregar a página…" />
              </div>
            }
          >
            <Page />
          </Suspense>
        ) : (
          <Home />
        )}
      </ErrorBoundary>
    </Layout>
  </StrictMode>,
);
