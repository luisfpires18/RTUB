import { call } from './eventsApi';
import type { MemberDetail, MemberInstrument, Mentor, Option } from './membersApi';

// /api/me, /api/members/map and /api/hall-of-fame (Endpoints/MemberAreaEndpoints.cs, task 032). Signed-in members
// only; the server decides every rule (MyProfileService, MemberMapService, HallOfFameService).

export type Person = { displayName: string; fullName: string | null; avatarUrl: string | null };

// ---------- Hall of Fame ----------

export type FameRecord = { key: string; title: string; winners: Person[]; value: string | null };
export type HallOfFame = { categories: FameRecord[] };

// ---------- members map ----------

export type MapCity = { name: string; latitude: number; longitude: number; members: Person[] };
export type MemberMap = { total: number; cities: MapCity[]; withoutCity: Person[]; pending: string[] };

// ---------- my profile ----------

export type Personal = {
  firstName: string | null;
  lastName: string | null;
  nickname: string | null;
  nicknameLocked: boolean;
  email: string | null;
  phoneNumber: string | null;
  dateOfBirth: string | null;
  city: string | null;
  degree: string | null;
};
export type Tuna = {
  showMentor: boolean;
  mentorId: string | null;
  mentorName: string | null;
  showLeitao: boolean;
  showCaloiro: boolean;
  showTuno: boolean;
  yearLeitao: number | null;
  monthLeitao: number | null;
  yearCaloiro: number | null;
  monthCaloiro: number | null;
  yearTuno: number | null;
  monthTuno: number | null;
  lockedDates: { leitao: boolean; caloiro: boolean; tuno: boolean };
};
export type Rank = {
  level: number;
  name: string;
  xp: number;
  maxLevel: boolean;
  xpToNext: number;
  xpInLevel: number;
  xpForLevel: number;
  percent: number;
  nextName: string | null;
};
export type MyProfile = {
  member: MemberDetail;
  personal: Personal;
  tuna: Tuna;
  instruments: MemberInstrument[];
  instrumentOptions: Option[];
  subscribed: boolean;
  requirePasswordChange: boolean;
  rank: Rank;
};
export type PersonalInput = Omit<Personal, 'nicknameLocked'>;
export type TunaInput = Omit<Tuna, 'showMentor' | 'mentorName' | 'showLeitao' | 'showCaloiro' | 'showTuno' | 'lockedDates'>;

export const memberAreaApi = {
  hallOfFame: () => call<HallOfFame>('GET', '/api/hall-of-fame'),
  map: () => call<MemberMap>('GET', '/api/members/map'),
  profile: () => call<MyProfile>('GET', '/api/me/profile'),
  savePersonal: (input: PersonalInput) => call<MyProfile>('PUT', '/api/me/profile/personal', input),
  saveTuna: (input: TunaInput) => call<MyProfile>('PUT', '/api/me/profile/tuna', input),
  mentors: (q: string) => call<Mentor[]>('GET', `/api/me/mentors?q=${encodeURIComponent(q)}`),
  addInstrument: (instrument: string) => call<MemberInstrument[]>('POST', '/api/me/instruments', { instrument, primary: false }),
  removeInstrument: (id: number) => call<MemberInstrument[]>('DELETE', `/api/me/instruments/${id}`),
  primaryInstrument: (id: number) => call<MemberInstrument[]>('PUT', `/api/me/instruments/${id}/primary`),
  photo: (photo: Blob) => {
    const form = new FormData();
    form.set('photo', photo, photo.type === 'image/jpeg' ? 'profile-picture.jpg' : 'profile-picture.webp');
    return call<{ avatarUrl: string }>('POST', '/api/me/photo', form);
  },
  subscription: (subscribed: boolean) => call<{ subscribed: boolean }>('PUT', '/api/me/subscription', { subscribed }),
  password: (currentPassword: string, newPassword: string, confirmPassword: string) =>
    call<void>('POST', '/api/me/password', { currentPassword, newPassword, confirmPassword }),
};
