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
  '/roles': lazy(() => import('./Governance')),
  '/gallery': lazy(() => import('./Gallery')),
  '/events': lazy(() => import('./Events')),
  '/events/my-enrollments': lazy(() => import('./MyEnrollments')),
  '/news': lazy(() => import('./News')),
  '/rehearsals': lazy(() => import('./Rehearsals')),
  '/members': lazy(() => import('./Members')),
  '/members/hierarchy': lazy(() => import('./MembersHierarchy')),
  '/leaderboard': lazy(() => import('./Leaderboard')),
  '/inventory': lazy(() => import('./Inventory')),
  '/shop': lazy(() => import('./Shop')),
  '/documentation': lazy(() => import('./Documentation')),
  '/logistics': lazy(() => import('./Logistics')),
  '/treasury': lazy(() => import('./Treasury')),
  '/treasury/calotes': lazy(() => import('./TreasuryCalotes')),
  '/treasury/mbway': lazy(() => import('./TreasuryMbway')),
  '/treasury/nerba': lazy(() => import('./TreasuryNerba')),
};
const path = location.pathname.replace(/\/+$/, '');
const Page = pages[path];

// /music/albums/{id} carries its id; Program.cs maps it with an {id:int} constraint.
const MusicAlbum = lazy(() => import('./MusicAlbum'));
const albumId = /^\/music\/albums\/(\d+)$/.exec(path)?.[1];

// /events/{id} (React track 011), mapped with {id:int} as well. Answering is a modal on that page.
const EventDetail = lazy(() => import('./EventDetail'));
const eventId = Number(/^\/events\/(\d+)$/.exec(path)?.[1]) || undefined;

// /events/{id}/discussion and /events/{id}/contacts (React track 013); Program.cs sends visitors to sign in first.
const EventDiscussion = lazy(() => import('./EventDiscussion'));
const EventContacts = lazy(() => import('./EventContacts'));
const eventPage = /^\/events\/(\d+)\/(discussion|contacts)$/.exec(path);

// /rehearsals/{id} (React track 014); Program.cs sends visitors to sign in first.
const RehearsalDetail = lazy(() => import('./RehearsalDetail'));
const rehearsalId = Number(/^\/rehearsals\/(\d+)$/.exec(path)?.[1]) || undefined;

// /logistics/{id} (React track 023); Program.cs sends visitors to sign in first.
const LogisticsBoard = lazy(() => import('./LogisticsBoard'));
const boardId = Number(/^\/logistics\/(\d+)$/.exec(path)?.[1]) || undefined;

// /treasury/reports/{id} and /treasury/nerba/{eventId} (React track 024); Program.cs sends visitors to sign in first.
const TreasuryReport = lazy(() => import('./TreasuryReport'));
const TreasuryNerbaEvent = lazy(() => import('./TreasuryNerbaEvent'));
const treasuryReportId = Number(/^\/treasury\/reports\/(\d+)$/.exec(path)?.[1]) || undefined;
const nerbaEventId = Number(/^\/treasury\/nerba\/(\d+)$/.exec(path)?.[1]) || undefined;

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Layout>
      <ErrorBoundary>
        {Page || albumId || eventId || eventPage || rehearsalId || boardId || treasuryReportId || nerbaEventId ? (
          <Suspense
            fallback={
              <div className="wrap">
                <Loading label="A carregar a página…" />
              </div>
            }
          >
            {albumId ? (
              <MusicAlbum albumId={Number(albumId)} />
            ) : eventId ? (
              <EventDetail eventId={eventId} />
            ) : eventPage?.[2] === 'discussion' ? (
              <EventDiscussion eventId={Number(eventPage[1])} />
            ) : eventPage ? (
              <EventContacts eventId={Number(eventPage[1])} />
            ) : rehearsalId ? (
              <RehearsalDetail rehearsalId={rehearsalId} />
            ) : boardId ? (
              <LogisticsBoard boardId={boardId} />
            ) : treasuryReportId ? (
              <TreasuryReport reportId={treasuryReportId} />
            ) : nerbaEventId ? (
              <TreasuryNerbaEvent eventId={nerbaEventId} />
            ) : (
              <Page />
            )}
          </Suspense>
        ) : (
          <Home />
        )}
      </ErrorBoundary>
    </Layout>
  </StrictMode>,
);
