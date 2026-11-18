// Typed client for /api/events (Endpoints/EventEndpoints.cs). The server decides
// what the caller may see and do; `isMember` and `member` only spare people buttons that would be
// refused. Dates are local "yyyy-MM-dd" / "HH:mm" text, never parsed as UTC.

export * from './eventsTypes';
export * from './eventsDates';
import type {
  EnrollmentInput,
  EnrollmentList,
  EventAgenda,
  EventContacts,
  EventDetail,
  EventDiscussion,
  EventEdit,
  EventEnrollment,
  EventInput,
  EventPrize,
  EventStats,
  EventSummary,
  ManagedVideo,
  MemberOption,
  NoticeAudience,
  NoticeInput,
  NoticeResult,
  RepertoireManage,
  RepertoireSong,
} from './eventsTypes';

// The shared request helper lives in api.ts (the shell's unread badge needs it on every page, and
// importing it from here pulled this whole client into the entry chunk); re-exported for the pages that import it here.
export { antiforgeryToken, call, type Outcome } from './api';
import { call } from './api';

export const eventsApi = {
  agenda: () => call<EventAgenda>('GET', '/api/events'),
  // Discussion (members).
  discussion: (id: number) => call<EventDiscussion>('GET', `/api/events/${id}/discussion`),
  addPost: (id: number, title: string, body: string) => call<EventDiscussion>('POST', `/api/events/${id}/discussion/posts`, { title, body }),
  editPost: (id: number, postId: number, title: string, body: string) =>
    call<EventDiscussion>('PUT', `/api/events/${id}/discussion/posts/${postId}`, { title, body }),
  deletePost: (id: number, postId: number) => call<EventDiscussion>('DELETE', `/api/events/${id}/discussion/posts/${postId}`),
  setPostFlags: (id: number, postId: number, pinned: boolean, locked: boolean) =>
    call<EventDiscussion>('PUT', `/api/events/${id}/discussion/posts/${postId}/flags`, { pinned, locked }),
  addComment: (id: number, postId: number, body: string) =>
    call<EventDiscussion>('POST', `/api/events/${id}/discussion/posts/${postId}/comments`, { body }),
  editComment: (id: number, commentId: number, body: string) =>
    call<EventDiscussion>('PUT', `/api/events/${id}/discussion/comments/${commentId}`, { body }),
  deleteComment: (id: number, commentId: number) => call<EventDiscussion>('DELETE', `/api/events/${id}/discussion/comments/${commentId}`),
  addTransport: (id: number, vehicle: string, seats: number, notes: string) =>
    call<EventDiscussion>('POST', `/api/events/${id}/discussion/transport`, { vehicle, seats, notes }),
  editTransport: (id: number, postId: number, vehicle: string, seats: number, notes: string) =>
    call<EventDiscussion>('PUT', `/api/events/${id}/discussion/posts/${postId}/transport`, { vehicle, seats, notes }),
  passengerMembers: (id: number, postId: number, q: string) =>
    call<MemberOption[]>('GET', `/api/events/${id}/discussion/posts/${postId}/passengers/members?q=${encodeURIComponent(q)}`),
  addPassenger: (id: number, postId: number, userId: string) =>
    call<EventDiscussion>('POST', `/api/events/${id}/discussion/posts/${postId}/passengers`, { userId }),
  removePassenger: (id: number, postId: number, passengerId: number) =>
    call<EventDiscussion>('DELETE', `/api/events/${id}/discussion/posts/${postId}/passengers/${passengerId}`),
  // Contact tracking (Mod and above).
  contacts: (id: number) => call<EventContacts>('GET', `/api/events/${id}/contacts`),
  saveContact: (id: number, userId: string, willAttend: boolean | null, notes: string) =>
    call<EventContacts>('PUT', `/api/events/${id}/contacts/${encodeURIComponent(userId)}`, { willAttend, notes }),
  resetContact: (id: number, userId: string) => call<EventContacts>('DELETE', `/api/events/${id}/contacts/${encodeURIComponent(userId)}`),
  stats: (from?: string, to?: string) =>
    call<EventStats>('GET', `/api/events/stats${from && to ? `?from=${from}&to=${to}` : ''}`),
  event: (id: number) => call<EventDetail>('GET', `/api/events/${id}`),
  getEnrollment: (id: number) => call<EventEnrollment>('GET', `/api/events/${id}/enrollment`),
  saveEnrollment: (id: number, input: EnrollmentInput) =>
    call<EventEnrollment>('PUT', `/api/events/${id}/enrollment`, input),
  removeEnrollment: (id: number) => call<EventEnrollment>('DELETE', `/api/events/${id}/enrollment`),
  videoPlayed: (videoId: number) => call<void>('POST', `/api/events/videos/${videoId}/plays`),
  // Admin/Owner; the server refuses everyone else.
  eventForEdit: (id: number) => call<EventEdit>('GET', `/api/events/${id}/edit`),
  createEvent: (input: EventInput) => call<EventSummary>('POST', '/api/events', input),
  updateEvent: (id: number, input: EventInput) => call<EventSummary>('PUT', `/api/events/${id}`, input),
  deleteEvent: (id: number) => call<void>('DELETE', `/api/events/${id}`),
  // Admin/Owner. The image arrives already cropped (WebP, JPEG where WebP cannot be encoded).
  setImage: (id: number, image: Blob) => {
    const form = new FormData();
    form.set('image', image, image.type === 'image/jpeg' ? 'event-image.jpg' : 'event-image.webp');
    return call<void>('POST', `/api/events/${id}/image`, form);
  },
  removeImage: (id: number) => call<void>('DELETE', `/api/events/${id}/image`),
  cancelEvent: (id: number, reason: string, notifyByEmail: boolean) =>
    call<NoticeResult>('POST', `/api/events/${id}/cancel`, { reason, notifyByEmail }),
  reactivateEvent: (id: number) => call<void>('POST', `/api/events/${id}/reactivate`),
  noticeAudience: (id: number) => call<NoticeAudience>('GET', `/api/events/${id}/notices`),
  sendNotice: (id: number, input: NoticeInput) => call<NoticeResult>('POST', `/api/events/${id}/notices`, input),
  // Admin/Owner. Every write answers the event's prizes as they now are.
  prizes: (id: number) => call<EventPrize[]>('GET', `/api/events/${id}/prizes`),
  addPrize: (id: number, name: string) => call<EventPrize[]>('POST', `/api/events/${id}/prizes`, { name }),
  renamePrize: (id: number, prizeId: number, name: string) =>
    call<EventPrize[]>('PUT', `/api/events/${id}/prizes/${prizeId}`, { name }),
  deletePrize: (id: number, prizeId: number) => call<EventPrize[]>('DELETE', `/api/events/${id}/prizes/${prizeId}`),
  // Admin/Owner. Every write answers the event's videos in order.
  videos: (id: number) => call<ManagedVideo[]>('GET', `/api/events/${id}/videos`),
  uploadVideo: (id: number, file: File, title: string) => {
    const form = new FormData();
    form.set('file', file);
    form.set('title', title);
    return call<ManagedVideo[]>('POST', `/api/events/${id}/videos`, form);
  },
  renameVideo: (id: number, videoId: number, title: string) =>
    call<ManagedVideo[]>('PUT', `/api/events/${id}/videos/${videoId}`, { title }),
  reorderVideos: (id: number, videoIds: number[]) => call<ManagedVideo[]>('POST', `/api/events/${id}/videos/reorder`, { videoIds }),
  deleteVideo: (id: number, videoId: number) => call<ManagedVideo[]>('DELETE', `/api/events/${id}/videos/${videoId}`),
  // Admin/Owner. Every write answers the whole repertoire.
  repertoire: (id: number) => call<RepertoireManage>('GET', `/api/events/${id}/repertoire`),
  repertoireSongs: (id: number, q: string) =>
    call<RepertoireSong[]>('GET', `/api/events/${id}/repertoire/songs?q=${encodeURIComponent(q)}`),
  addToRepertoire: (id: number, songId: number, date: string) =>
    call<RepertoireManage>('POST', `/api/events/${id}/repertoire`, { songId, date }),
  removeFromRepertoire: (id: number, itemId: number) => call<RepertoireManage>('DELETE', `/api/events/${id}/repertoire/${itemId}`),
  clearRepertoireDay: (id: number, date: string) => call<RepertoireManage>('DELETE', `/api/events/${id}/repertoire/days/${date}`),
  reorderRepertoire: (id: number, date: string, itemIds: number[]) =>
    call<RepertoireManage>('POST', `/api/events/${id}/repertoire/reorder`, { date, itemIds }),
  // Admin/Owner: other members' answers. Every write answers the whole list.
  enrollments: (id: number) => call<EnrollmentList>('GET', `/api/events/${id}/enrollments`),
  enrollmentMembers: (id: number, q: string) =>
    call<MemberOption[]>('GET', `/api/events/${id}/enrollments/members?q=${encodeURIComponent(q)}`),
  addEnrollment: (id: number, userId: string) => call<EnrollmentList>('POST', `/api/events/${id}/enrollments`, { userId }),
  removeMemberEnrollment: (id: number, enrollmentId: number) =>
    call<EnrollmentList>('DELETE', `/api/events/${id}/enrollments/${enrollmentId}`),
};
