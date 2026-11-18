// The event form's draft shape, blank / prefilled values and the image-picker constants.

import type { EventEdit, EventInput } from './eventsApi';
import { today } from './format';

/** The limit on the picked file; the cropped image the server takes is ≤5 MB. */
export const MAX_SOURCE_BYTES = 10 * 1024 * 1024;

export const EVENT_IMAGE = {
  title: 'Recortar a imagem',
  label: 'Pré-visualização da imagem da atuação (3:2). Arraste ou use as setas para enquadrar.',
  note: 'As imagens das atuações ficam em 3:2. Arraste a imagem para escolher o enquadramento.',
};

export type Form = EventInput & { multiDay: boolean };

export const blank = (): Form => ({
  name: '',
  date: today(),
  time: '19:00',
  endDate: null,
  location: '',
  type: 'Atuacao',
  description: '',
  membersOnly: false,
  multiDay: false,
});

export const fromEdit = (e: EventEdit): Form => ({ ...e, multiDay: e.endDate !== null });
