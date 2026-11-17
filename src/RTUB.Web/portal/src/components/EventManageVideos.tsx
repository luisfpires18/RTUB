// Dialog to upload, order, rename and delete an event's videos.

import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './AppShared';
import { Dialog } from './Dialog';
import { eventsApi, MAX_EVENT_VIDEO_BYTES, type ManagedVideo, type EventSummary, type Outcome } from '../data/eventsApi';
import { Icon } from './icons';
import { failure, EventLine } from './EventManageShared';

/**
 * The event's videos for Admin/Owner (012C): upload (past events, as the old page; file ≤100 MB and a
 * required title), rename, move up / down, delete. Every write answers the list in order; `onChanged`
 * refreshes the event page so its player follows.
 */
export function VideoManagerDialog({ event, onClose, onChanged }: { event: EventSummary; onClose: () => void; onChanged: () => void }) {
  const [videos, setVideos] = useState<ManagedVideo[] | null>();
  const [file, setFile] = useState<File | null>(null);
  const [title, setTitle] = useState('');
  const [editing, setEditing] = useState<{ id: number; title: string }>();
  const [deleting, setDeleting] = useState<number>();
  const [busy, setBusy] = useState<'upload' | 'other'>();
  const [errors, setErrors] = useState<Record<string, string>>({});
  const ids = { file: useId(), title: useId(), edit: useId() };

  useEffect(() => {
    eventsApi.videos(event.id).then((o) => setVideos(o.kind === 'ok' ? o.data : null));
  }, [event.id]);

  const run = async (kind: 'upload' | 'other', call: Promise<Outcome<ManagedVideo[]>>, done: () => void) => {
    setBusy(kind);
    setErrors({});
    const o = await call;
    setBusy(undefined);
    if (o.kind === 'ok') {
      setVideos(o.data);
      done();
      onChanged();
    } else if (o.kind === 'invalid') setErrors(o.errors);
    else if (o.kind === 'closed') setErrors({ form: 'Só se juntam vídeos a atuações que já aconteceram.' });
    else setErrors({ form: failure(o, kind === 'upload' ? 'enviar o vídeo' : 'guardar') });
  };

  const pick = (f: File | undefined) => {
    setErrors({});
    if (!f) return setFile(null);
    if (!f.type.startsWith('video/') && !/\.(mp4|mov|m4v|webm|3gp|mkv|avi)$/i.test(f.name))
      return setErrors({ file: 'Escolha um ficheiro de vídeo.' });
    if (f.size > MAX_EVENT_VIDEO_BYTES) return setErrors({ file: 'O vídeo não pode exceder 100 MB.' });
    setFile(f);
  };

  const upload = (e: FormEvent) => {
    e.preventDefault();
    if (!file) return setErrors({ file: 'Escolha um ficheiro de vídeo.' });
    if (!title.trim()) return setErrors({ title: 'Indique o título do vídeo.' });
    run('upload', eventsApi.uploadVideo(event.id, file, title.trim()), () => {
      setFile(null);
      setTitle('');
    });
  };

  const move = (index: number, by: -1 | 1) => {
    if (!videos) return;
    const order = videos.map((v) => v.id);
    [order[index], order[index + by]] = [order[index + by], order[index]];
    run('other', eventsApi.reorderVideos(event.id, order), () => undefined);
  };

  const label = (v: ManagedVideo, i: number) => v.title || `Vídeo ${i + 1}`;

  return (
    <Dialog title="Gerir vídeos" onClose={onClose}>
      <div className="answer">
        <EventLine event={event} />
        {videos === undefined ? (
          <Loading label="A carregar os vídeos…" />
        ) : videos === null ? (
          <p className="form__error">Não foi possível carregar os vídeos desta atuação.</p>
        ) : videos.length === 0 ? (
          <p className="note">Ainda não há vídeos desta atuação.</p>
        ) : (
          <ol className="prize-manager__list video-manager__list">
            {videos.map((v, i) => (
              <li key={v.id}>
                {editing?.id === v.id ? (
                  <form
                    className="prize-manager__row"
                    onSubmit={(e) => {
                      e.preventDefault();
                      run('other', eventsApi.renameVideo(event.id, v.id, editing.title.trim()), () => setEditing(undefined));
                    }}
                  >
                    <label className="sr-only" htmlFor={ids.edit}>
                      Novo título do vídeo
                    </label>
                    <input
                      id={ids.edit}
                      type="text"
                      maxLength={200}
                      value={editing.title}
                      onChange={(e) => setEditing({ id: v.id, title: e.target.value })}
                      disabled={!!busy}
                      autoFocus
                    />
                    <button type="submit" className="btn btn--primary btn--sm" disabled={!!busy}>
                      Guardar
                    </button>
                    <button type="button" className="btn btn--ghost btn--sm" onClick={() => setEditing(undefined)} disabled={!!busy}>
                      Cancelar
                    </button>
                  </form>
                ) : deleting === v.id ? (
                  <div className="prize-manager__row" role="alert">
                    <span className="prize-manager__name">Apagar «{label(v, i)}»? O ficheiro também é apagado.</span>
                    <button
                      type="button"
                      className="btn btn--danger btn--sm"
                      onClick={() => run('other', eventsApi.deleteVideo(event.id, v.id), () => setDeleting(undefined))}
                      disabled={!!busy}
                    >
                      Apagar
                    </button>
                    <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDeleting(undefined)} disabled={!!busy}>
                      Voltar
                    </button>
                  </div>
                ) : (
                  <div className="prize-manager__row">
                    <Icon name="video" />
                    <span className="prize-manager__name">{label(v, i)}</span>
                    <button type="button" className="icon-btn icon-btn--sm" onClick={() => move(i, -1)} disabled={!!busy || i === 0} title="Subir">
                      <Icon name="up" />
                      <span className="sr-only">Subir {label(v, i)}</span>
                    </button>
                    <button
                      type="button"
                      className="icon-btn icon-btn--sm"
                      onClick={() => move(i, 1)}
                      disabled={!!busy || i === videos.length - 1}
                      title="Descer"
                    >
                      <Icon name="down" />
                      <span className="sr-only">Descer {label(v, i)}</span>
                    </button>
                    <button
                      type="button"
                      className="icon-btn icon-btn--sm"
                      onClick={() => {
                        setDeleting(undefined);
                        setEditing({ id: v.id, title: v.title ?? '' });
                      }}
                      disabled={!!busy}
                      title="Mudar o título"
                    >
                      <Icon name="pencil" />
                      <span className="sr-only">Mudar o título de {label(v, i)}</span>
                    </button>
                    <button
                      type="button"
                      className="icon-btn icon-btn--sm icon-btn--danger"
                      onClick={() => {
                        setEditing(undefined);
                        setDeleting(v.id);
                      }}
                      disabled={!!busy}
                      title="Apagar"
                    >
                      <Icon name="trash" />
                      <span className="sr-only">Apagar {label(v, i)}</span>
                    </button>
                  </div>
                )}
              </li>
            ))}
          </ol>
        )}
        {errors.videoIds && <p className="form__error">{errors.videoIds}</p>}

        {event.past ? (
          <form className="form video-manager__upload" onSubmit={upload} noValidate>
            <p className="eyebrow">Adicionar vídeo</p>
            {/* Not a .form__field: its input rule would give the hidden file input full width. */}
            <div className="video-manager__file">
              <label className="btn btn--ghost btn--sm" htmlFor={ids.file}>
                <Icon name="upload" />
                {file ? 'Trocar ficheiro' : 'Escolher vídeo'}
              </label>
              <input
                id={ids.file}
                className="sr-only"
                type="file"
                accept="video/*"
                disabled={!!busy}
                onChange={(e) => {
                  pick(e.target.files?.[0]);
                  e.target.value = '';
                }}
              />
              <p className="form__hint">{file ? `${file.name} · ${(file.size / 1024 / 1024).toFixed(1)} MB` : 'Até 100 MB.'}</p>
              {errors.file && <p className="form__error">{errors.file}</p>}
            </div>
            <div className={errors.title ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor={ids.title}>Título</label>
              <input id={ids.title} type="text" maxLength={200} value={title} onChange={(e) => setTitle(e.target.value)} disabled={!!busy} />
              {errors.title && <p className="form__error">{errors.title}</p>}
            </div>
            <div className="answer__actions">
              <button type="submit" className="btn btn--primary" disabled={!!busy || !file}>
                {busy === 'upload' && <span className="spinner spinner--small" aria-hidden="true" />}
                {busy === 'upload' ? 'A enviar…' : 'Enviar vídeo'}
              </button>
            </div>
            <p className="form__hint">Os outros membros recebem uma notificação do novo vídeo, como antes.</p>
          </form>
        ) : (
          <p className="form__hint">Os vídeos juntam-se depois da atuação.</p>
        )}
        {errors.form && (
          <p className="form__error" role="alert">
            {errors.form}
          </p>
        )}
      </div>
    </Dialog>
  );
}
