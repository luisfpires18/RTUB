// Entry of the lazy event-management chunk (EventManageLazy.tsx imports this file); the dialogs live in EventManage*.tsx.

export { EventFormDialog } from './EventManageForm';
export { DeleteEventDialog, CancelEventDialog, ReactivateEventDialog } from './EventManageCancel';
export { ParticipantsManagerDialog } from './EventManageParticipants';
export { RepertoireManagerDialog } from './EventManageRepertoire';
export { VideoManagerDialog } from './EventManageVideos';
export { PrizeManager } from './EventManagePrizes';
export { NoticeDialog } from './EventManageNotice';

// Admin/Owner event management on the React agenda: create, edit, delete; the image,
// cancel / reactivate and email / push notices. The server enforces who may do it; these
// modals only appear for people it would accept. Prizes, videos, repertoire and
// other members' answers are managed from the event page.
