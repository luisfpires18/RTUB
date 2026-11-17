import { call } from './api';

// Task 083 (#264): the running build's release notes for signed-in members (Endpoints/ReleaseEndpoints.cs). The
// server decides what is pending from its receipts; the client only shows and acknowledges. Nothing is stored in the
// browser: a receipt is per member and per version, on the server, so another account on this device has its own.

export type ReleaseNote = { version: string; title: string; items: string[] };

/** version: the running build's release ('' for a DEV build whose notes are hidden); current: its notes; pending: unread, newest first. */
export type ReleaseNotesState = { version: string; current: ReleaseNote | null; pending: ReleaseNote[] };

export const releaseNotesApi = {
  state: () => call<ReleaseNotesState>('GET', '/api/releases/notes'),
  acknowledge: (version: string) => call<void>('POST', '/api/releases/notes/ack', { version }),
};
