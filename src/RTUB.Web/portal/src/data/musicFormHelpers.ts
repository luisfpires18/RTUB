// Small helpers shared by the music forms: error text and accent-insensitive search.

export const failedMessage = (title?: string) => title ?? 'Não foi possível guardar. Tente novamente.';

export const normalize = (s: string) =>
  s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .trim();
