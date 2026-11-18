// Shapes of the /api/meetings payloads and inputs (see meetingsApi.ts).

export type MeetingOption = { value: string; label: string };
/** A person to pick: id + what the select shows. */
export type MeetingPersonOption = { id: string; label: string };

type MeetingBoardAccess = {
  canCreate: boolean;
  meetingTypes: MeetingOption[];
  canProposeCv: boolean;
  canProposeDirecao: boolean;
  canProposeAg: boolean;
  canSeeRequests: boolean;
  canAddParticipants: boolean;
};

/** The card's buttons, decided by the server. */
type MeetingCan = {
  manage: boolean;
  notify: boolean;
  uncancel: boolean;
  respond: boolean;
  removeOwn: boolean;
  participants: boolean;
  writeAta: boolean;
  viewAta: boolean;
};

type MeetingMyParticipation = { id: number; willAttend: boolean; notes: string | null };

export type MeetingCard = {
  id: number;
  type: string;
  typeLabel: string;
  title: string;
  date: string;
  location: string | null;
  statement: string;
  organizerName: string | null;
  organizerPosition: string | null;
  tunoRepresentative: string | null;
  tunoRepresentativeId: string | null;
  cancelled: boolean;
  completed: boolean;
  past: boolean;
  goingCount: number;
  myParticipation: MeetingMyParticipation | null;
  ataStatus: 'draft' | 'published' | null;
  can: MeetingCan;
};

/** GET /api/meetings. `fiscalYear` null = every year. */
export type MeetingBoard = {
  upcoming: MeetingCard[];
  past: MeetingCard[];
  fiscalYears: string[];
  currentFiscalYear: string;
  fiscalYear: string | null;
  access: MeetingBoardAccess;
};

export type MeetingForm = { types: MeetingOption[]; tunoRepresentatives: MeetingPersonOption[] };

export type MeetingInput = {
  type: string;
  title: string;
  date: string;
  location: string;
  statement: string;
  tunoRepresentativeId: string | null;
};

/** `card` is null when the member who saved it cannot see it. */
export type MeetingSaved = { id: number; card: MeetingCard | null };

/** Someone in a "who receives" list: name, picture and, for a CV, years as Tuno. Never an email address. */
export type MeetingPerson = { name: string; avatarUrl: string; detail: string | null };

export type MeetingEmailDraft = {
  subject: string;
  body: string;
  recipients: MeetingPerson[];
  notReceiving: MeetingPerson[];
  tunoOptions: MeetingPersonOption[];
};

export type MeetingNoticeResult = { sent: number; warning: string | null };
export type MeetingCancelDraft = { recipients: MeetingPerson[]; notReceiving: MeetingPerson[]; total: number };
export type MeetingPushDraft = { recipients: MeetingPerson[]; notSubscribed: MeetingPerson[]; tunoOptions: MeetingPersonOption[] };

export type MeetingParticipant = {
  id: number;
  nickname: string;
  fullName: string;
  avatarUrl: string;
  notes: string | null;
  badge: string | null;
  canRemove: boolean;
};

export type MeetingParticipants = {
  meetingId: number;
  title: string;
  going: MeetingParticipant[];
  notGoing: MeetingParticipant[];
  canAdd: boolean;
};

export type MeetingCandidate = { id: string; name: string; avatarUrl: string };

export type MeetingAgendaPoint = {
  number: number;
  title: string;
  discussion: string | null;
  decision: string | null;
  votesFor: number | null;
  votesAgainst: number | null;
  votesAbstain: number | null;
  result: string | null;
};

type MeetingAtaKind = 'cv' | 'ag' | 'direcao';

/** The ata editor. `id` null = not saved yet. */
export type MeetingAtaEditor = {
  id: number | null;
  status: 'draft' | 'published';
  kind: MeetingAtaKind;
  meetingTypeLabel: string;
  meetingTitle: string;
  meetingDate: string;
  ataNumber: string | null;
  actualStartTime: string;
  actualEndTime: string | null;
  location: string;
  quorumBasis: string;
  presidentName: string | null;
  firstSecretaryId: string | null;
  secondSecretaryId: string | null;
  firstSecretaryOptions: MeetingPersonOption[];
  secondSecretaryOptions: MeetingPersonOption[];
  present: string[];
  agendaPoints: MeetingAgendaPoint[];
  closingText: string | null;
  generatedAt: string | null;
};

type MeetingAgendaPointInput = Omit<MeetingAgendaPoint, 'number'>;

export type MeetingAtaInput = {
  ataNumber: string | null;
  actualStartTime: string;
  actualEndTime: string | null;
  location: string;
  quorumBasis: string | null;
  firstSecretaryId: string | null;
  secondSecretaryId: string | null;
  agendaPoints: MeetingAgendaPointInput[];
  closingText: string | null;
};

/** "Ver Ata". `mine` only for someone who attended a published ata; `mine.confirmed` null = not answered yet. */
export type MeetingAtaView = {
  meetingId: number;
  status: 'draft' | 'published';
  kind: MeetingAtaKind;
  meetingTypeLabel: string;
  ataNumber: string | null;
  meetingDate: string;
  location: string;
  presidentName: string | null;
  firstSecretaryName: string | null;
  secondSecretaryName: string | null;
  present: string[];
  confirmed: string[];
  mine: { confirmed: boolean | null; notes: string | null } | null;
  agendaPoints: MeetingAgendaPoint[];
  closingText: string | null;
};

type MeetingRequestStatus = 'Pending' | 'Confirmed' | 'Rejected' | 'Analysing';

export type MeetingRequest = {
  id: number;
  type: string;
  typeLabel: string;
  title: string;
  authorName: string;
  proposedDate: string;
  location: string | null;
  description: string;
  status: MeetingRequestStatus;
  canDelete: boolean;
  canRemind: boolean;
  canDecide: boolean;
};

export type MeetingRequestPage = { items: MeetingRequest[]; total: number; page: number; pageSize: number };

/** A proposal. `type`: ConselhoVeteranos, ReuniaoDirecao or AssembleiaGeralExtraordinaria. */
export type MeetingRequestInput = { type: string; title: string; proposedDate: string; location: string; description: string };

/** An accepted request, as the create form opens prefilled with it. */
export type MeetingDraft = { type: string; title: string; date: string; location: string | null; statement: string };

export type MeetingPollAudienceOption = { value: string; label: string };
type MeetingPollOption = { id: number; text: string; votes: number };
export type MeetingPoll = {
  id: number;
  audience: string;
  audienceLabel: string;
  question: string;
  createdAt: string;
  closesAt: string | null;
  closed: boolean;
  canManage: boolean;
  canVote: boolean;
  /** Its creator, or Owner. The server checks again on DELETE. */
  canDelete: boolean;
  myOptionId: number | null;
  totalVotes: number;
  options: MeetingPollOption[];
};
export type MeetingPollBoard = { createAudiences: MeetingPollAudienceOption[]; polls: MeetingPoll[] };
export type MeetingPollInput = { audience: string; question: string; options: string[]; closesAt: string | null };

/** Page sizes of the /meetings and /meetings/polls grids. */
export const MEETING_PAGE_SIZES = [6, 12, 18, 24];
