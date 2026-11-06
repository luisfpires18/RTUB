import { useEffect, useId, useState } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { AccessFields, EventField } from './Logistics';
import { logisticsApi, PRIORITY, problem, STATUS, type LogisticsEvent, type Priority, type Template, type Visibility } from './logisticsApi';

type Person = { id: string; name: string; avatar: string | null };
type Draft = { include: boolean; title: string; priority: Priority; dueDate: string; checklist: string };

/** "yyyy-mm-dd" + n days. */
const plus = (anchor: string, days: number) => {
  const d = new Date(`${anchor}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};

const PRIORITIES: Priority[] = ['Low', 'Normal', 'High', 'Urgent'];

/**
 * "A partir de modelo" (task 067, managers): pick Organizar FITAB or Preparar deslocação/atuação, anchor the deadlines
 * to the event's date (or a date of your choice), review and edit every suggested task - include or leave out, title,
 * priority, deadline, checklist - and who sees the board, then create it. Nothing is created before "Criar quadro";
 * nothing is assigned or notified.
 */
export function TemplateDialog({ onClose }: { onClose: () => void }) {
  const [templates, setTemplates] = useState<Template[] | null>();
  const [key, setKey] = useState<string>();
  const [name, setName] = useState('');
  const [event, setEvent] = useState<LogisticsEvent | null>(null);
  const [anchor, setAnchor] = useState('');
  const [drafts, setDrafts] = useState<Draft[]>([]);
  const [owner, setOwner] = useState<Person | null>(null);
  const [visibility, setVisibility] = useState<Visibility>('Members');
  const [members, setMembers] = useState<Person[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const fid = useId();

  useEffect(() => {
    logisticsApi.templates().then((o) => setTemplates(o.kind === 'ok' ? o.data : null));
  }, []);

  const template = templates?.find((t) => t.key === key);
  const cards = template?.lists.flatMap((l) => l.cards) ?? [];

  // A new template or anchor recomputes the suggestions (titles and checklists edited for another template are dropped).
  const pick = (t: Template) => {
    setKey(t.key);
    setName(event ? `${t.name.replace(/^Organizar |^Preparar /, '')} · ${event.name}` : t.name);
    setDrafts(
      t.lists.flatMap((l) =>
        l.cards.map((c) => ({
          include: true,
          title: c.title,
          priority: c.priority,
          dueDate: anchor && c.offsetDays !== null ? plus(anchor, c.offsetDays) : '',
          checklist: c.checklist.join('\n'),
        })),
      ),
    );
  };

  const setAnchorDate = (date: string) => {
    setAnchor(date);
    if (template && date) setDrafts(drafts.map((d, i) => ({ ...d, dueDate: cards[i].offsetDays !== null ? plus(date, cards[i].offsetDays!) : d.dueDate })));
  };

  const chooseEvent = (e: LogisticsEvent | null) => {
    setEvent(e);
    if (e) setAnchorDate(e.date.slice(0, 10));
  };

  const set = (i: number, patch: Partial<Draft>) => setDrafts(drafts.map((d, j) => (j === i ? { ...d, ...patch } : d)));

  const create = async () => {
    if (!template) return;
    setBusy(true);
    setErrors({});
    let at = 0;
    const lists = template.lists.map((l) => ({
      name: l.name,
      state: l.state,
      cards: l.cards
        .map(() => drafts[at++])
        .filter((d) => d.include && d.title.trim())
        .map((d) => ({
          title: d.title.trim(),
          description: '',
          priority: d.priority,
          dueDate: d.dueDate || null,
          checklist: d.checklist.split('\n').map((x) => x.trim()).filter(Boolean),
        })),
    }));
    const o = await logisticsApi.compose({
      board: { name, description: template.description, eventId: event?.id ?? null, ownerUserId: owner?.id ?? null, visibility, memberIds: visibility === 'Private' ? members.map((m) => m.id) : [] },
      lists,
    });
    setBusy(false);
    if (o.kind === 'ok') location.href = `/logistics/${o.data.id}`;
    else if (o.kind === 'invalid') setErrors(o.errors);
    else setErrors({ form: problem(o, 'criar o quadro') });
  };

  const included = drafts.filter((d) => d.include).length;

  return (
    <Dialog
      title="Novo quadro a partir de modelo"
      size="xl"
      className="lx-card-modal"
      onClose={onClose}
      footer={
        <>
          <span className="lx-ws__state">{template ? `${included} de ${drafts.length} tarefas · nada é atribuído nem notificado` : ''}</span>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--primary" onClick={create} disabled={busy || !template || !name.trim()}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Criar quadro
          </button>
        </>
      }
    >
      {templates === undefined ? (
        <Loading label="A carregar os modelos…" />
      ) : templates === null ? (
        <p className="form__banner">Não foi possível carregar os modelos.</p>
      ) : (
        <div className="form lx-template">
          {errors.form && (
            <p className="form__banner" role="alert">
              {errors.form}
            </p>
          )}
          <fieldset className="form__field">
            <legend>Modelo</legend>
            <div className="lx-template__choices">
              {templates.map((t) => (
                <label key={t.key} className={`lx-choice lx-choice--card${key === t.key ? ' lx-choice--on' : ''}`}>
                  <input type="radio" name={`${fid}-tpl`} checked={key === t.key} onChange={() => pick(t)} />
                  <span>
                    <strong>{t.name}</strong>
                    <small>{t.description}</small>
                  </span>
                </label>
              ))}
            </div>
          </fieldset>

          {template && (
            <>
              <EventField event={event} setEvent={chooseEvent} label="Atuação (a sua data define os prazos)" error={errors['board.eventId'] ?? errors.eventId} />
              <div className="form__row">
                <div className="form__field">
                  <label htmlFor={`${fid}-anchor`}>Data de referência</label>
                  <input id={`${fid}-anchor`} type="date" value={anchor} onChange={(e) => setAnchorDate(e.target.value)} />
                  <small className="note">Os prazos contam a partir daqui (ex.: 14 dias antes).</small>
                </div>
                <div className={errors.name ? 'form__field form__field--error' : 'form__field'}>
                  <label htmlFor={`${fid}-name`}>Nome do quadro</label>
                  <input id={`${fid}-name`} type="text" maxLength={200} value={name} onChange={(e) => setName(e.target.value)} />
                  {errors.name && <p className="form__error">{errors.name}</p>}
                </div>
              </div>

              <section className="lx-detail__section" aria-labelledby={`${fid}-preview`}>
                <h3 id={`${fid}-preview`} className="lx-detail__title">
                  Pré-visualização
                </h3>
                <p className="note">
                  Colunas: {template.lists.map((l) => `${l.name} (${STATUS[l.state].label})`).join(' · ')}. As tarefas entram em «{template.lists[0].name}».
                </p>
                <ol className="lx-template__cards">
                  {drafts.map((d, i) => (
                    <li key={i} className={d.include ? 'lx-template__card' : 'lx-template__card lx-template__card--off'}>
                      <label className="lx-template__include">
                        <input type="checkbox" checked={d.include} onChange={() => set(i, { include: !d.include })} />
                        <span className="sr-only">Incluir {d.title}</span>
                      </label>
                      <div className="lx-template__fields">
                        <input type="text" aria-label="Título da tarefa" maxLength={200} value={d.title} disabled={!d.include} onChange={(e) => set(i, { title: e.target.value })} />
                        <div className="lx-template__row">
                          <label>
                            <span>Prazo</span>
                            <input type="date" value={d.dueDate} disabled={!d.include} onChange={(e) => set(i, { dueDate: e.target.value })} />
                          </label>
                          <label>
                            <span>Prioridade</span>
                            <select value={d.priority} disabled={!d.include} onChange={(e) => set(i, { priority: e.target.value as Priority })}>
                              {PRIORITIES.map((p) => (
                                <option key={p} value={p}>
                                  {PRIORITY[p]}
                                </option>
                              ))}
                            </select>
                          </label>
                          {cards[i]?.offsetDays !== null && cards[i]?.offsetDays !== undefined && (
                            <small className="note">{cards[i].offsetDays === 0 ? 'no dia' : `${-cards[i].offsetDays!} dias antes`}</small>
                          )}
                        </div>
                        <label className="lx-template__check">
                          <span>Checklist (uma por linha)</span>
                          <textarea rows={Math.max(2, d.checklist.split('\n').length)} value={d.checklist} disabled={!d.include} onChange={(e) => set(i, { checklist: e.target.value })} />
                        </label>
                      </div>
                    </li>
                  ))}
                </ol>
                {(errors.cards || errors.lists) && (
                  <p className="form__error" role="alert">
                    {errors.cards ?? errors.lists}
                  </p>
                )}
              </section>

              <AccessFields
                owner={owner}
                setOwner={setOwner}
                visibility={visibility}
                setVisibility={setVisibility}
                members={members}
                setMembers={setMembers}
                errors={Object.fromEntries(Object.entries(errors).map(([k, v]) => [k.replace(/^board\./, ''), v]))}
              />
              {!anchor && (
                <p className="note">
                  <Icon name="calendar" /> Sem data de referência, as tarefas ficam sem prazo.
                </p>
              )}
            </>
          )}
        </div>
      )}
    </Dialog>
  );
}
