import { call, queryString } from './api';

// /api/meetings (Endpoints/MeetingEndpoints.cs). Signed-in members only; a Leitão is refused everything
// and a meeting the member does not see is a 404. The server decides every rule (MeetingBoardService): the
// `access`, `can` and `canX` flags only spare people buttons that would be refused. Dates travel as local
// "yyyy-MM-ddTHH:mm" text and are never handed to a Date parser, so no time zone can move them.

import type { MeetingCard, MeetingBoard, MeetingForm, MeetingInput, MeetingSaved, MeetingEmailDraft, MeetingNoticeResult, MeetingCancelDraft, MeetingPushDraft, MeetingParticipants, MeetingCandidate, MeetingAtaEditor, MeetingAtaInput, MeetingAtaView, MeetingRequestPage, MeetingRequestInput, MeetingDraft, MeetingPollBoard, MeetingPollInput } from './meetingsTypes';

const base = '/api/meetings';

export const meetingsApi = {
  // fy: null = the server's default (the current fiscal year), "all" = every year, else "YYYY-YYYY".
  board: (fy: string | null, q: string) => call<MeetingBoard>('GET', `${base}${queryString({ fy, q })}`),
  meeting: (id: number) => call<MeetingCard>('GET', `${base}/${id}`),
  form: () => call<MeetingForm>('GET', `${base}/form`),
  create: (input: MeetingInput) => call<MeetingSaved>('POST', base, input),
  update: (id: number, input: MeetingInput) => call<MeetingSaved>('PUT', `${base}/${id}`, input),
  remove: (id: number) => call<void>('DELETE', `${base}/${id}`),
  cancelDraft: (id: number) => call<MeetingCancelDraft>('GET', `${base}/${id}/cancel`),
  cancel: (id: number, reason: string, notifyByEmail: boolean) =>
    call<MeetingNoticeResult>('POST', `${base}/${id}/cancel`, { reason, notifyByEmail }),
  uncancel: (id: number) => call<MeetingCard>('POST', `${base}/${id}/uncancel`),
  emailDraft: (id: number) => call<MeetingEmailDraft>('GET', `${base}/${id}/email`),
  emailPreview: (id: number, body: string) => call<{ html: string }>('POST', `${base}/${id}/email/preview`, { body }),
  sendEmail: (id: number, subject: string, body: string, tunoId: string | null) =>
    call<MeetingNoticeResult>('POST', `${base}/${id}/email`, { subject, body, tunoId }),
  pushDraft: (id: number) => call<MeetingPushDraft>('GET', `${base}/${id}/push`),
  sendPush: (id: number, message: string, tunoId: string | null) =>
    call<MeetingNoticeResult>('POST', `${base}/${id}/push`, { message, tunoId }),
  // Participations.
  respond: (id: number, willAttend: boolean, notes: string | null) =>
    call<MeetingCard>('PUT', `${base}/${id}/participation`, { willAttend, notes }),
  participants: (id: number) => call<MeetingParticipants>('GET', `${base}/${id}/participants`),
  removeParticipation: (id: number, participationId: number) =>
    call<MeetingParticipants>('DELETE', `${base}/${id}/participants/${participationId}`),
  candidates: (id: number, q: string) => call<MeetingCandidate[]>('GET', `${base}/${id}/participants/candidates${queryString({ q })}`),
  addParticipant: (id: number, userId: string) => call<MeetingParticipants>('POST', `${base}/${id}/participants`, { userId }),
  // Atas.
  ata: (id: number) => call<MeetingAtaView>('GET', `${base}/${id}/ata`),
  ataEditor: (id: number) => call<MeetingAtaEditor>('GET', `${base}/${id}/ata/edit`),
  saveAta: (id: number, input: MeetingAtaInput) => call<MeetingAtaEditor>('PUT', `${base}/${id}/ata`, input),
  publishAta: (id: number) => call<MeetingAtaEditor>('POST', `${base}/${id}/ata/publish`),
  confirmAta: (id: number, confirm: boolean) => call<MeetingAtaView>('POST', `${base}/${id}/ata/confirmation`, { confirm }),
  ataPdfUrl: (id: number) => `${base}/${id}/ata/pdf`,
  // Meeting requests ("Pedidos de Reuniões").
  requests: (status: string, fy: string | null, page: number, pageSize: number) =>
    call<MeetingRequestPage>('GET', `${base}/requests${queryString({ status, fy, page, pageSize })}`),
  propose: (input: MeetingRequestInput) => call<{ id: number }>('POST', `${base}/requests`, input),
  accept: (requestId: number) => call<MeetingDraft>('POST', `${base}/requests/${requestId}/accept`),
  reject: (requestId: number) => call<void>('POST', `${base}/requests/${requestId}/reject`),
  removeRequest: (requestId: number) => call<void>('DELETE', `${base}/requests/${requestId}`),
  remind: (requestId: number) => call<void>('POST', `${base}/requests/${requestId}/reminder`),
  // Anonymous-result polls (the server stores one private ballot per eligible member).
  polls: () => call<MeetingPollBoard>('GET', `${base}/polls`),
  createPoll: (input: MeetingPollInput) => call<number>('POST', `${base}/polls`, input),
  votePoll: (pollId: number, optionId: number) =>
    call<boolean>('PUT', `${base}/polls/${pollId}/vote`, { optionId }),
  closePoll: (pollId: number) => call<boolean>('POST', `${base}/polls/${pollId}/close`),
  removePoll: (pollId: number) => call<void>('DELETE', `${base}/polls/${pollId}`),
};
