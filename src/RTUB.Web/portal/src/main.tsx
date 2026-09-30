import { StrictMode, Suspense, lazy } from 'react';
import { createRoot } from 'react-dom/client';
import { ErrorBoundary, Layout, Loading } from './App';
import { Home } from './Home';
import './styles.css';

// The server maps exactly /portal and /portal/privacidade here (Program.cs), so choosing the
// page once at boot is all the routing the pilot needs. Links between the two are ordinary
// full navigations; a client router is a decision for the real migration.
const Privacy = lazy(() => import('./Privacy'));
const isPrivacy = location.pathname.replace(/\/+$/, '') === '/portal/privacidade';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Layout>
      <ErrorBoundary>
        {isPrivacy ? (
          <Suspense fallback={<Loading label="A carregar a Política de Privacidade…" />}>
            <Privacy />
          </Suspense>
        ) : (
          <Home />
        )}
      </ErrorBoundary>
    </Layout>
  </StrictMode>,
);
