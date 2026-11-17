// Naipes limits, accepted formats and error text.
import { type Outcome } from './eventsApi';
import { type NaipeItem } from './naipesApi';
import { outcomeText } from './api';

export const PAGE_SIZES = [10, 20, 30, 40, 50];
export const MAX_VIDEO = 100 * 1024 * 1024;
export const MAX_IMAGE = 10 * 1024 * 1024;
export const MAX_COMMENT = 1000;
// The formats NaipeBoardService accepts (it checks the bytes too).
export const VIDEO_ACCEPT = 'video/mp4,video/quicktime,video/webm,video/3gpp,.mp4,.m4v,.mov,.webm,.3gp';
export const IMAGE_ACCEPT = 'image/jpeg,image/png,image/webp,image/gif';

export const problem = (o: Outcome<unknown>, what: string) =>
  outcomeText(o, what, { forbidden: 'Só quem adicionou este conteúdo o pode alterar.', notfound: 'Este conteúdo já não existe.' });

export type Editing = { kind: 'new'; isVideo: boolean } | { kind: 'edit'; item: NaipeItem };
