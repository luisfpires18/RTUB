// Form to write a discussion post or a lift offer.

import { useId, useState, type FormEvent } from 'react';
import { useCurrentUser } from './AppShared';
import { eventsApi, type EventDiscussion as Discussion } from '../data/eventsApi';
import { Icon } from './icons';
import { BODY_MAX, problem, submitOnCtrlEnter } from './EventDiscussionShared';
import { FormField } from './ui/FormField';

// ---------- writing a post or a lift offer ----------

export function Composer({ eventId, onPosted }: { eventId: number; onPosted: (d: Discussion) => void }) {
  const { user } = useCurrentUser();
  const [mode, setMode] = useState<'note' | 'lift'>('note');
  const [title, setTitle] = useState('');
  const [withTitle, setWithTitle] = useState(false);
  const [body, setBody] = useState('');
  const [lift, setLift] = useState({ vehicle: '', seats: 4, notes: '' });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const ids = { body: useId(), title: useId(), vehicle: useId(), seats: useId(), notes: useId() };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o =
      mode === 'note'
        ? await eventsApi.addPost(eventId, withTitle ? title : '', body)
        : await eventsApi.addTransport(eventId, lift.vehicle, lift.seats, lift.notes);
    setBusy(false);
    if (o.kind !== 'ok') return setErrors(o.kind === 'invalid' ? o.errors : { form: problem(o) });
    setErrors({});
    setTitle('');
    setWithTitle(false);
    setBody('');
    setLift({ vehicle: '', seats: 4, notes: '' });
    setMode('note');
    onPosted(o.data);
  };

  const error = (key: string) => errors[key] && <p className="form__error">{errors[key]}</p>;

  return (
    <form className="talk__composer" onSubmit={submit} noValidate>
      {user?.authenticated && <img className="talk__avatar" src={user.avatarUrl} alt="" width="44" height="44" />}
      <div className="talk__composer-main">
        <div className="talk__modes" role="group" aria-label="O que queres publicar">
          <button type="button" className={`chip-toggle${mode === 'note' ? ' is-on' : ''}`} aria-pressed={mode === 'note'} onClick={() => setMode('note')}>
            <Icon name="chat" />
            Mensagem
          </button>
          <button type="button" className={`chip-toggle${mode === 'lift' ? ' is-on' : ''}`} aria-pressed={mode === 'lift'} onClick={() => setMode('lift')}>
            <Icon name="car" />
            Oferecer boleia
          </button>
        </div>

        {mode === 'note' ? (
          <>
            {withTitle && (
              <div className={errors.title ? 'form__field form__field--error' : 'form__field'}>
                <label htmlFor={ids.title} className="sr-only">
                  Título
                </label>
                <input id={ids.title} className="talk__title-input" placeholder="Título" maxLength={120} value={title} onChange={(e) => setTitle(e.target.value)} />
                {error('title')}
              </div>
            )}
            <div className={errors.body ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor={ids.body} className="sr-only">
                Mensagem
              </label>
              <textarea
                id={ids.body}
                rows={body ? 4 : 2}
                placeholder="Escreve uma nota para a atuação…"
                maxLength={BODY_MAX}
                value={body}
                onChange={(e) => setBody(e.target.value)}
                onKeyDown={submitOnCtrlEnter}
              />
              {error('body')}
            </div>
            <div className="talk__composer-foot">
              {!withTitle && (
                <button type="button" className="link-btn" onClick={() => setWithTitle(true)}>
                  Adicionar título
                </button>
              )}
              <span className="talk__hint">Usa @alcunha para chamar alguém.</span>
              <button type="submit" className="btn btn--primary btn--sm" disabled={busy || body.trim().length === 0}>
                <Icon name="send" />
                {busy ? 'A publicar…' : 'Publicar'}
              </button>
            </div>
          </>
        ) : (
          <>
            <div className="talk__lift-fields">
              <FormField id={ids.vehicle} label="Viatura" error={errors.vehicle}>
                <input id={ids.vehicle} placeholder="Ex.: Clio cinzento" maxLength={200} value={lift.vehicle} onChange={(e) => setLift({ ...lift, vehicle: e.target.value })} />
              </FormField>
              <FormField id={ids.seats} label="Lugares" error={errors.seats}>
                <input id={ids.seats} type="number" min={2} max={20} value={lift.seats} onChange={(e) => setLift({ ...lift, seats: Number(e.target.value) })} />
              </FormField>
            </div>
            <FormField id={ids.notes} label="Notas" error={errors.notes}>
              <textarea id={ids.notes} rows={2} maxLength={500} placeholder="Ex.: saída às 18h da residência" value={lift.notes} onChange={(e) => setLift({ ...lift, notes: e.target.value })} />
            </FormField>
            <div className="talk__composer-foot">
              <span className="talk__hint">Depois juntas quem vai contigo.</span>
              <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !lift.vehicle.trim()}>
                <Icon name="car" />
                {busy ? 'A publicar…' : 'Oferecer boleia'}
              </button>
            </div>
          </>
        )}
        {error('form')}
      </div>
    </form>
  );
}
