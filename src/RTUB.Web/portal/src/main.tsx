import { StrictMode, Suspense, lazy } from 'react';
import { createRoot } from 'react-dom/client';
import { ErrorBoundary, Layout } from './components/App';
import { Loading } from './components/AppShared';
import { Home } from './pages/Home';
import './styles.css';
import { getCurrentUser } from './data/api';

// The session request leaves with the bundle's first bytes instead of after React's first commit (task 054);
// getCurrentUser caches its promise, so the shell's own call joins this one.
getCurrentUser();

// The server maps exactly these paths here (Program.cs), so choosing the page once at boot is all
// the routing the portal needs. Links between them are ordinary full navigations; a client router
// is a decision for when routes stop being a short, fixed list.
const pages: Record<string, ReturnType<typeof lazy>> = {
  '/privacy': lazy(() => import('./pages/Privacy')),
  '/profile': lazy(() => import('./pages/Profile')),
  '/request': lazy(() => import('./pages/Request')),
  '/music': lazy(() => import('./pages/Music')),
  '/music/learn': lazy(() => import('./pages/MusicLearn')),
  '/login': lazy(() => import('./pages/Login')),
  '/roles': lazy(() => import('./pages/Governance')),
  '/gallery': lazy(() => import('./pages/Gallery')),
  '/events': lazy(() => import('./pages/Events')),
  '/news': lazy(() => import('./pages/News')),
  '/rehearsals': lazy(() => import('./pages/Rehearsals')),
  '/members': lazy(() => import('./pages/Members')),
  '/members/hierarchy': lazy(() => import('./pages/MembersHierarchy')),
  '/members/map': lazy(() => import('./pages/MembersMap')),
  '/hall-of-fame': lazy(() => import('./pages/HallOfFame')),
  '/naipes': lazy(() => import('./pages/Naipes')),
  '/naipes/config': lazy(() => import('./pages/NaipesConfig')),
  '/leaderboard': lazy(() => import('./pages/Leaderboard')),
  '/inventory': lazy(() => import('./pages/Inventory')),
  '/shop': lazy(() => import('./pages/Shop')),
  '/documentation': lazy(() => import('./pages/Documentation')),
  '/logistics': lazy(() => import('./pages/Logistics')),
  '/logistics/tasks': lazy(() => import('./pages/LogisticsTasks')),
  '/treasury': lazy(() => import('./pages/Treasury')),
  '/treasury/calotes': lazy(() => import('./pages/TreasuryCalotes')),
  '/treasury/mbway': lazy(() => import('./pages/TreasuryMbway')),
  '/treasury/nerba': lazy(() => import('./pages/TreasuryNerba')),
  '/requests': lazy(() => import('./pages/Requests')),
  '/questions': lazy(() => import('./pages/Questions')),
  '/meetings': lazy(() => import('./pages/Meetings')),
  '/meetings/polls': lazy(() => import('./pages/MeetingPolls')),
  '/messages': lazy(() => import('./pages/Messages')),
  '/pending-actions': lazy(() => import('./pages/PendingActions')),
  '/users': lazy(() => import('./pages/OwnerUsers')),
  '/owner/tracing': lazy(() => import('./pages/OwnerTracing')),
  '/owner/dashboard': lazy(() => import('./pages/OwnerDashboard')),
  '/owner/db': lazy(() => import('./pages/OwnerDatabase')),
  '/emails': lazy(() => import('./pages/OwnerEmails')),
  '/notifications': lazy(() => import('./pages/OwnerNotifications')),
  '/forgot-password': lazy(() => import('./pages/ForgotPassword')),
  '/reset-password': lazy(() => import('./pages/ResetPassword')),
  '/confirm-email': lazy(() => import('./pages/ConfirmEmail')),
  '/share': lazy(() => import('./pages/Share')),
};
const path = location.pathname.replace(/\/+$/, '');
const Page = pages[path];

// /music/albums/{id} carries its id; Program.cs maps it with an {id:int} constraint.
const MusicAlbum = lazy(() => import('./pages/MusicAlbum'));
const albumId = /^\/music\/albums\/(\d+)$/.exec(path)?.[1];

// /music/play/{songId} (task 094): the play-along page; Program.cs sends visitors to sign in first.
const MusicPlayAlong = lazy(() => import('./pages/MusicPlayAlong'));
const playSongId = Number(/^\/music\/play\/(\d+)$/.exec(path)?.[1]) || undefined;

// /events/{id} (React track 011), mapped with {id:int} as well. Answering is a modal on that page.
const EventDetail = lazy(() => import('./pages/EventDetail'));
const eventId = Number(/^\/events\/(\d+)$/.exec(path)?.[1]) || undefined;

// /events/{id}/discussion and /events/{id}/contacts (React track 013); Program.cs sends visitors to sign in first.
const EventDiscussion = lazy(() => import('./pages/EventDiscussion'));
const EventContacts = lazy(() => import('./pages/EventContacts'));
const eventPage = /^\/events\/(\d+)\/(discussion|contacts)$/.exec(path);

// /rehearsals/{id} (React track 014); Program.cs sends visitors to sign in first.
const RehearsalDetail = lazy(() => import('./pages/RehearsalDetail'));
const rehearsalId = Number(/^\/rehearsals\/(\d+)$/.exec(path)?.[1]) || undefined;

// /logistics/{id} (React track 023); Program.cs sends visitors to sign in first.
const LogisticsBoard = lazy(() => import('./pages/LogisticsBoard'));
const boardId = Number(/^\/logistics\/(\d+)$/.exec(path)?.[1]) || undefined;

// /treasury/reports/{id} and /treasury/nerba/{eventId} (React track 024); Program.cs sends visitors to sign in first.
const TreasuryReport = lazy(() => import('./pages/TreasuryReport'));
const TreasuryNerbaEvent = lazy(() => import('./pages/TreasuryNerbaEvent'));
const treasuryReportId = Number(/^\/treasury\/reports\/(\d+)$/.exec(path)?.[1]) || undefined;
const nerbaEventId = Number(/^\/treasury\/nerba\/(\d+)$/.exec(path)?.[1]) || undefined;

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Layout>
      <ErrorBoundary>
        {Page || albumId || playSongId || eventId || eventPage || rehearsalId || boardId || treasuryReportId || nerbaEventId ? (
          <Suspense
            fallback={
              <div className="wrap">
                <Loading label="A carregar a página…" />
              </div>
            }
          >
            {albumId ? (
              <MusicAlbum albumId={Number(albumId)} />
            ) : playSongId ? (
              <MusicPlayAlong songId={playSongId} />
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
