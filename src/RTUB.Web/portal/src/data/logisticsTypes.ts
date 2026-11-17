// Shapes the /api/logistics endpoints send and receive (React track 023).

export type LogisticsEvent = { id: number; name: string; date: string };
export type Visibility = 'Members' | 'Private';
/** A board in the list (task 067): responsible member, visibility, archive, and its active cards' counts. */
export type BoardSummary = {
  id: number;
  name: string;
  description: string;
  event: LogisticsEvent | null;
  isCompleted: boolean;
  completedAt: string | null;
  owner: Assignee | null;
  visibility: Visibility;
  archivedAt: string | null;
  cards: number;
  done: number;
  overdue: number;
  unowned: number;
  mine: number;
};
export type Boards = { active: BoardSummary[]; completed: BoardSummary[]; archived: BoardSummary[]; canManage: boolean };
export type Label = { text: string; color: string };
export type Person = { displayName: string; fullName: string | null; avatarUrl: string | null };
export type Status = 'Todo' | 'InProgress' | 'Blocked' | 'Done';
export type Priority = 'Low' | 'Normal' | 'High' | 'Urgent';
export type Card = {
  id: number;
  title: string;
  description: string;
  status: Status;
  labels: Label[];
  startDate: string | null;
  dueDate: string | null;
  eventName: string | null;
  assignedTo: Person | null;
  assignments: number;
  checklistDone: number;
  checklistTotal: number;
  links: number;
  /** The caller is the owner or a collaborator (task 065). */
  mine: boolean;
  priority: Priority;
  comments: number;
  attachments: number;
};
/** A column; `state` (task 067) is the status of every card in it. */
export type List = { id: number; name: string; cards: Card[]; state: Status };
/** `id` addresses the file ("f:…" the board's own folder, "l:year/…" its pre-067 folder of that fiscal `year`). */
export type BoardFile = { name: string; extension: string; sizeBytes: number; id: string; year: string | null };
export type Board = {
  id: number;
  name: string;
  description: string;
  event: LogisticsEvent | null;
  isCompleted: boolean;
  lists: List[];
  labels: string[];
  files: BoardFile[];
  canManage: boolean;
  owner: Assignee | null;
  visibility: Visibility;
  members: Assignee[];
  archivedAt: string | null;
  archivedCards: Card[];
};
/** `userId` only for managers (they change assignments); null for everyone else. */
export type Assignee = { userId: string | null; displayName: string; fullName: string | null; avatarUrl: string | null };
export type ChecklistItem = { task: string; done: boolean };
export type CardDetail = {
  card: Card;
  listId: number;
  assignedTo: Assignee | null;
  checklist: ChecklistItem[];
  links: { cardId: number | null; name: string }[];
  assignments: Assignee[];
  /** May change everything (Mod, Admin, Owner): owner, collaborators, labels, card links, delete. */
  canManage: boolean;
  /** Owner, collaborator or manager (task 066): edits the card's fields and checklist, status, comments, attaches. */
  canWork: boolean;
  /** Sent back with a field edit; a stale one answers 409 ('conflict'). */
  version: number;
  comments: CardComment[];
  attachments: CardAttachment[];
  activity: CardActivity[];
  archivedAt: string | null;
  /** The card or its board is archived: read-only until a manager restores it. */
  readOnly: boolean;
  /** Task 069: the reminders the caller sees (managers all; anyone else their own). */
  reminders: Reminder[];
  /** May add a reminder: owner, collaborator (for themselves) or manager (for board members). */
  canRemind: boolean;
};
export type Frequency = 'OneTime' | 'Daily' | 'Weekly';
/** `next` is Portugal wall-clock time ("2026-11-07T10:00:00"). */
export type Reminder = {
  id: number;
  frequency: Frequency;
  next: string;
  recipients: Assignee[];
  personal: boolean;
  canEdit: boolean;
  lastSentAt: string | null;
};
export type ReminderInput = { frequency: Frequency; nextReminderDate: string; userIds: string[] };
/** "As minhas tarefas" (task 069); `role` null = an open card without a responsible member (managers only). */
export type MyTask = {
  cardId: number;
  boardId: number;
  boardName: string;
  title: string;
  status: Status;
  priority: Priority;
  dueDate: string | null;
  role: 'owner' | 'collaborator' | null;
  checklistDone: number;
  checklistTotal: number;
};
export type MyTasks = { tasks: MyTask[]; canManage: boolean };
export type CardComment = {
  id: number;
  author: Person | null;
  body: string;
  mentions: string[];
  createdAt: string;
  editedAt: string | null;
  canEdit: boolean;
  canDelete: boolean;
};
export type CardAttachment = {
  id: number;
  kind: 'File' | 'Link';
  name: string;
  host: string | null;
  sizeBytes: number;
  extension: string | null;
  addedBy: Person | null;
  createdAt: string;
  canDelete: boolean;
};
export type CardActivity = { actor: Person | null; action: string; detail: string; at: string };
export type MemberOption = { id: string; displayName: string; fullName: string | null; avatarUrl: string | null };
export type CardInput = {
  title: string;
  description: string;
  startDate: string | null;
  dueDate: string | null;
  /** The owner: only a manager changes or clears it; everyone else sends null (kept). */
  assignedToUserId: string | null;
  priority: Priority;
  version: number;
};

export type BoardInput = {
  name: string;
  description: string;
  eventId: number | null;
  ownerUserId: string | null;
  visibility: Visibility;
  memberIds: string[];
};
export type TemplateCard = { title: string; description: string; priority: Priority; offsetDays: number | null; checklist: string[] };
export type Template = { key: string; name: string; description: string; lists: { name: string; state: Status; cards: TemplateCard[] }[] };
export type ComposeCard = { title: string; description: string; priority: Priority; dueDate: string | null; checklist: string[] };
export type ComposeInput = { board: BoardInput; lists: { name: string; state: Status; cards: ComposeCard[] }[] };
