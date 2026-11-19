// Member form constants, form type and blank/from-edit builders shared by the member admin dialogs.
import { type Outcome, outcomeText } from './api';
import { type MemberEdit, type MemberInput } from './membersApi';

export const CATEGORIES = [
  { value: 'Leitao', label: 'Leitão' },
  { value: 'Caloiro', label: 'Caloiro' },
  { value: 'Tuno', label: 'Tuno' },
];

export const MONTHS = ['Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho', 'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro'];

export const MIN_YEAR = 1990;

export const LOCKED_HINT = 'Data fechada: só o Magister deste ano a corrige.';

export const problem = (o: Outcome<unknown>, what: string) =>
  outcomeText(o, what, {
    forbidden: 'Não tem permissão para esta ação.',
    notfound: 'Este membro já não existe.',
    inuse: 'Este membro tem transferências MBWay em seu nome. Trata delas na Tesouraria antes de o eliminar.',
  });

/** As UsernameHelper.NormalizeUsername: lower case, no accents, letters and digits only. */
export const username = (nickname: string) =>
  nickname
    .toLowerCase()
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z0-9]/g, '');

export type Form = MemberInput & { mentorName: string | null };

export const blank = (): Form => ({
  firstName: '',
  lastName: '',
  nickname: '',
  phoneNumber: '',
  email: '',
  city: '',
  degree: '',
  dateOfBirth: null,
  category: '',
  fundador: false,
  honorario: false,
  noNickname: false,
  yearLeitao: null,
  monthLeitao: null,
  yearCaloiro: null,
  monthCaloiro: null,
  yearTuno: null,
  monthTuno: null,
  mentorId: null,
  mentorName: null,
  instruments: null,
});

export const fromEdit = ({ id: _, instruments: __, lockedDates: ___, ...m }: MemberEdit): Form => ({ ...m, noNickname: false, instruments: null });
