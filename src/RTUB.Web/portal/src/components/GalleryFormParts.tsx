// People picker and details fields shared by the gallery upload and edit dialogs.
import { useEffect, useId, useState } from 'react';
import { FieldError } from './ui/FieldError';
import { GalleryPerson } from '../data/api';
import { Icon } from './icons';
import { type Taggable, type Form, galleryApi } from '../data/galleryUploadApi';

/** Who appears: chosen chips plus a search over members (not expelled). */
export function PeoplePicker({ people, onChange }: { people: GalleryPerson[]; onChange: (people: GalleryPerson[]) => void }) {
  const [query, setQuery] = useState('');
  const [found, setFound] = useState<Taggable[]>();
  const id = useId();

  useEffect(() => {
    if (!query.trim()) return setFound(undefined);
    let live = true; // a slower answer for an older query never replaces the newer one
    const timer = setTimeout(() => {
      galleryApi.people(query.trim()).then((o) => live && setFound(o.kind === 'ok' ? o.data.filter((m) => !people.some((p) => p.id === m.id)) : []));
    }, 250);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [query, people]);

  return (
    <div className="form__field">
      <label htmlFor={id}>Quem aparece</label>
      {people.length > 0 && (
        <ul className="chips" aria-label="Pessoas marcadas">
          {people.map((p) => (
            <li key={p.id} className="chip chip--removable">
              {p.name}
              <button type="button" className="chip__remove" onClick={() => onChange(people.filter((x) => x.id !== p.id))} aria-label={`Tirar ${p.name}`}>
                <Icon name="close" />
              </button>
            </li>
          ))}
        </ul>
      )}
      <span className="control control--sm">
        <Icon name="search" />
        <input id={id} type="search" placeholder="Procurar por alcunha ou nome" value={query} onChange={(e) => setQuery(e.target.value)} />
      </span>
      {found && found.length === 0 && <p className="note">Ninguém encontrado.</p>}
      {found && found.length > 0 && (
        <ul className="talk__picks">
          {found.map((m) => (
            <li key={m.id}>
              <button
                type="button"
                onClick={() => {
                  onChange([...people, { id: m.id, name: m.name }]);
                  setQuery('');
                }}
              >
                <img src={m.avatarUrl} alt="" width="28" height="28" loading="lazy" />
                <span>
                  {m.name}
                  {m.fullName && m.fullName !== m.name && <small>{m.fullName}</small>}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export function Details({ form, set, errors, idPrefix }: { form: Form; set: (f: Partial<Form>) => void; errors: Record<string, string>; idPrefix: string }) {
  return (
    <>
      <div className={errors.title ? 'form__field form__field--error' : 'form__field'}>
        <label htmlFor={`${idPrefix}-title`}>Título</label>
        <input id={`${idPrefix}-title`} maxLength={200} value={form.title} onChange={(e) => set({ title: e.target.value })} />
        <FieldError text={errors.title} alert={false} />
      </div>
      <div className={errors.date ? 'form__field form__field--error' : 'form__field'}>
        <label htmlFor={`${idPrefix}-date`}>Data</label>
        <input id={`${idPrefix}-date`} type="date" value={form.date} onChange={(e) => set({ date: e.target.value })} />
        <p className="form__hint">Sem a data exata? Escolhe o dia 1 do mês (guarda só o mês) ou o 1 de janeiro (guarda só o ano).</p>
        <FieldError text={errors.date} alert={false} />
      </div>
      <label className="check">
        <input type="checkbox" checked={form.membersOnly} onChange={(e) => set({ membersOnly: e.target.checked })} />
        <Icon name="lock" />
        Só para membros
      </label>
      <p className="form__hint">
        {form.membersOnly ? 'Só membros com sessão iniciada a veem na galeria.' : 'Qualquer visitante a vê na galeria pública.'}
      </p>
    </>
  );
}
