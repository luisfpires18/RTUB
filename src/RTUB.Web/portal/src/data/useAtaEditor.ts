import { useEffect, useState } from 'react';
import { problem, type Errors } from '../components/MeetingUi';
import { meetingsApi } from './meetingsApi';
import type { MeetingAtaEditor } from './meetingsTypes';
import { type Point, type Values, toPoint, fromEditor, toInput, download, editorProblem } from './meetingAtaForm';

export const MAX_POINTS = 100;

/** State and actions of the ata editor dialog: load once per meeting, then save / PDF / publish. */
export function useAtaEditor(meetingId: number, onChanged: () => void) {
  const [editor, setEditor] = useState<MeetingAtaEditor | null>();
  const [values, setValues] = useState<Values>();
  const [newPoint, setNewPoint] = useState('');
  const [errors, setErrors] = useState<Errors>({});
  const [banner, setBanner] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [busy, setBusy] = useState<'save' | 'pdf' | 'publish'>();
  const [confirmPublish, setConfirmPublish] = useState(false);

  useEffect(() => {
    meetingsApi.ataEditor(meetingId).then((o) => {
      if (o.kind === 'ok') {
        setEditor(o.data);
        setValues(fromEditor(o.data));
      } else {
        setEditor(null);
        setBanner(editorProblem(o.kind, problem(o, 'abrir a ata')));
      }
    });
  }, [meetingId]);

  const set = (patch: Partial<Values>) => setValues((v) => v && { ...v, ...patch });
  const setPoint = (key: number, patch: Partial<Point>) =>
    setValues((v) => v && { ...v, points: v.points.map((p) => (p.key === key ? { ...p, ...patch } : p)) });

  const addPoint = () => {
    const title = newPoint.trim();
    if (!title || !values || values.points.length >= MAX_POINTS) return;
    set({ points: [...values.points, toPoint({ title })] });
    setNewPoint('');
  };

  /** Saves the form; the saved editor, or null (errors shown). */
  const save = async (): Promise<MeetingAtaEditor | null> => {
    if (!editor || !values) return null;
    setBanner(undefined);
    setNotice(undefined);
    const o = await meetingsApi.saveAta(meetingId, toInput(values, editor.kind));
    if (o.kind === 'ok') {
      setErrors({});
      setEditor(o.data);
      setValues(fromEditor(o.data));
      onChanged();
      return o.data;
    }
    if (o.kind === 'invalid') {
      setErrors(o.errors);
      setBanner(o.errors.agendaPoints ?? 'Há campos por corrigir.');
    } else setBanner(editorProblem(o.kind, problem(o, 'guardar a ata')));
    return null;
  };

  /** Saves while `busy` shows `action`, then runs `afterSave` if the save worked. */
  const saveThen = async (action: 'save' | 'pdf', afterSave: () => void) => {
    setBusy(action);
    const saved = await save();
    setBusy(undefined);
    if (saved) afterSave();
  };

  const onSave = () => saveThen('save', () => setNotice('Ata guardada.'));
  const onPdf = () => saveThen('pdf', () => download(meetingsApi.ataPdfUrl(meetingId)));

  const publish = async (): Promise<string | void> => {
    setBusy('publish');
    const saved = await save();
    if (!saved) {
      setBusy(undefined);
      return 'A ata não foi guardada: veja os campos por corrigir.';
    }
    const o = await meetingsApi.publishAta(meetingId);
    setBusy(undefined);
    if (o.kind !== 'ok') return o.kind === 'closed' ? 'Só uma ata guardada e ainda por publicar pode ser publicada.' : problem(o, 'publicar a ata');
    setEditor(o.data);
    setValues(fromEditor(o.data));
    setNotice('Ata publicada.');
    onChanged();
  };

  return { editor, values, newPoint, setNewPoint, errors, banner, notice, busy, confirmPublish, setConfirmPublish, set, setPoint, addPoint, onSave, onPdf, publish };
}
