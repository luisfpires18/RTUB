import { useEffect, useId, useState } from 'react';
import { Loading } from './App';
import { dateLabel, weekday, type MemberOption, type Outcome } from './eventsApi';
import { Icon } from './icons';
import { loginTo } from './musicApi';
import { CancelDialog, NoticeDialog, PresenceDialog, PresencePill, RehearsalFormDialog } from './RehearsalDialogs';
import { rehearsalsApi, type Attendee, type InstrumentCount, type RehearsalDetail as Detail } from './rehearsalsApi';

/**
 * /rehearsals/{id} - one rehearsal (React track 014): its details, the member's own presença and everyone's
 * presenças (members going, Leitões, not going) with what they play. Admin/Owner edit, notify, cancel or delete it,
 * and once it can be approved confirm, remove or add presenças. Every rule is the server's.
 */
export default function RehearsalDetail({ rehearsalId }: { rehearsalId: number }) {
  const [detail, setDetail] = useState<Detail | 'missing' | 'signin' | null>();
  const [dialog, setDialog] = useState<'presence' | 'edit' | 'notice' | 'cancel'>();
  const [confirm, setConfirm] = useState<'delete' | 'reactivate'>();
  const [error, setError] = useState<string>();

  const load = (quiet = false) => {
    if (!quiet) setDetail(undefined);
    rehearsalsApi.rehearsal(rehearsalId).then((o) =>
      setDetail(o.kind === 'ok' ? o.data : o.kind === 'notfound' ? 'missing' : o.kind === 'signin' ? 'signin' : null),
    );
  };
  useEffect(() => load(), [rehearsalId]);

  const loaded = detail && typeof detail === 'object' ? detail : null;
  useEffect(() => {
    document.title = loaded ? `Ensaio de ${dateLabel({ date: loaded.rehearsal.date, endDate: null })} · RTUB` : 'Ensaio · RTUB';
  }, [loaded]);

  const act = async (call: Promise<Outcome<void>>, after: () => void) => {
    setError(undefined);
    const o = await call;
    setConfirm(undefined);
    if (o.kind === 'ok') after();
    else setError(o.kind === 'closed' ? 'Este ensaio já não permite isto.' : 'Não foi possível concluir. Tenta outra vez.');
  };

  const back = (
    <a className="back-link" href="/rehearsals">
      <Icon name="arrow" />
      Ensaios
    </a>
  );

  if (detail === undefined) return <section className="page wrap">{back}<Loading label="A carregar o ensaio…" /></section>;
  if (detail === 'signin' || detail === 'missing' || detail === null) {
    return (
      <section className="page wrap">
        {back}
        <div className="notice" role="status">
          {detail === 'signin' ? (
            <>
              <p>Os ensaios são da área de membros.</p>
              <a className="btn btn--primary btn--sm" href={loginTo(`/rehearsals/${rehearsalId}`)}>
                <Icon name="login" />
                Entrar
              </a>
            </>
          ) : (
            <p>{detail === 'missing' ? 'Este ensaio não existe ou já foi apagado.' : 'Não conseguimos abrir este ensaio agora.'}</p>
          )}
        </div>
      </section>
    );
  }

  const r = detail.rehearsal;
  const day = dateLabel({ date: r.date, endDate: null });
  const open = !r.past && !r.cancelled;
  const a = detail.attendance;

  return (
    <section className="page wrap event-page rehearsal-page" aria-labelledby="rehearsal-title">
      <div className="event-topbar">
        {back}
        {detail.canManage && (
          <span className="event-topbar__actions">
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDialog('edit')}>
              <Icon name="pencil" />
              Editar
            </button>
            {open && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDialog('notice')}>
                <Icon name="bell" />
                Notificar
              </button>
            )}
            {!r.cancelled && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDialog('cancel')}>
                <Icon name="ban" />
                Cancelar ensaio
              </button>
            )}
            {r.cancelled && !r.past && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirm('reactivate')}>
                <Icon name="restore" />
                Reativar
              </button>
            )}
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirm('delete')}>
              <Icon name="trash" />
              Apagar
            </button>
          </span>
        )}
      </div>

      {confirm && (
        <div className="notice" role="alert">
          <p>
            {confirm === 'delete'
              ? `Apagar o ensaio de ${day}? As presenças vão com ele. Não dá para desfazer.`
              : 'Reativar este ensaio? As presenças removidas no cancelamento não voltam.'}
          </p>
          <div className="answer__actions">
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirm(undefined)}>
              Não
            </button>
            <button
              type="button"
              className={confirm === 'delete' ? 'btn btn--danger btn--sm' : 'btn btn--primary btn--sm'}
              onClick={() =>
                confirm === 'delete'
                  ? act(rehearsalsApi.remove(r.id), () => location.assign('/rehearsals'))
                  : act(rehearsalsApi.reactivate(r.id), () => load(true))
              }
            >
              {confirm === 'delete' ? 'Apagar' : 'Reativar'}
            </button>
          </div>
        </div>
      )}
      {error && <p className="form__error">{error}</p>}

      <header className="rehearsal-hero">
        <p className="eyebrow">Ensaio{r.cancelled && ' · cancelado'}</p>
        <h1 id="rehearsal-title" className="page__title">
          {r.theme ?? `Ensaio de ${weekday(r.date)}`}
        </h1>
        <ul className="rehearsal-hero__facts">
          <li>
            <Icon name="calendar" />
            {day}
          </li>
          <li>
            <Icon name="clock" />
            {r.start}–{r.end}
          </li>
          <li>
            <Icon name="geo" />
            {r.location}
          </li>
        </ul>
      </header>

      {r.cancelled && (
        <p className="warning">
          <Icon name="warning" />
          Ensaio cancelado{r.cancellationReason && `: ${r.cancellationReason}`}
        </p>
      )}

      <div className="event-layout">

        <div className="event-main">
          {(r.description || detail.notes) && (
            <section className="event-section" aria-labelledby="about-title">
              <h2 id="about-title" className="event-section__title">
                Sobre o ensaio
              </h2>
              {r.description && <p className="talk__text">{r.description}</p>}
              {detail.notes && (
                <p className="talk__text note">
                  <strong>Notas: </strong>
                  {detail.notes}
                </p>
              )}
            </section>
          )}

          <section className="event-section" aria-labelledby="presences-title">
            <h2 id="presences-title" className="event-section__title">
              Presenças
            </h2>
            {detail.canManage && r.approvable && !r.cancelled && (
              <p className="note">Confirma as presenças de quem esteve no ensaio; podes também acrescentar quem não marcou.</p>
            )}
            {a.going.length + a.leitoes.length + a.notGoing.length === 0 ? (
              <p className="note">{r.cancelled ? 'As presenças foram removidas com o cancelamento.' : 'Ainda ninguém marcou presença.'}</p>
            ) : (
              <>
                <Group title={r.past ? 'Foram' : 'Vão'} people={a.going} rehearsalId={r.id} onChanged={() => load(true)} />
                <Group title="Leitões" people={a.leitoes} rehearsalId={r.id} onChanged={() => load(true)} />
                {a.notGoing.length > 0 && (
                  <details className="who__more">
                    <summary>
                      {r.past ? 'Não foram' : 'Não vão'} · {a.notGoing.length}
                    </summary>
                    <Group title="" people={a.notGoing} rehearsalId={r.id} onChanged={() => load(true)} />
                  </details>
                )}
              </>
            )}
            {detail.canManage && r.approvable && !r.cancelled && <AddAttendee rehearsalId={r.id} onAdded={() => load(true)} />}
          </section>

          {(detail.instruments.length > 0 || detail.otherInstruments.length > 0) && (
            <section className="event-section" aria-labelledby="instruments-title">
              <h2 id="instruments-title" className="event-section__title">
                Instrumentos
              </h2>
              <Counts counts={detail.instruments} />
              {detail.otherInstruments.length > 0 && (
                <details className="who__more">
                  <summary>Outros instrumentos que tocam</summary>
                  <Counts counts={detail.otherInstruments} />
                </details>
              )}
            </section>
          )}
        </div>
        <aside className="member-panel" aria-labelledby="mine-title">
          <h2 id="mine-title" className="member-panel__title">
            A minha presença
          </h2>
          <div className="member-panel__answer">
            {r.mine ? <PresencePill rehearsal={r} /> : <span className="note">{open ? 'Ainda não marcaste.' : 'Sem presença.'}</span>}
            {r.mine?.instrument && <span className="note">{r.mine.instrument}</span>}
          </div>
          {(open || r.mine) && (
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setDialog('presence')}>
              {open ? (r.mine ? 'Alterar presença' : 'Marcar presença') : 'Ver a minha presença'}
            </button>
          )}
          <dl className="member-panel__counts">
            <div>
              <dt>{r.past ? 'Foram' : 'Vão'}</dt>
              <dd>{r.goingCount}</dd>
            </div>
            <div>
              <dt>{r.past ? 'Não foram' : 'Não vão'}</dt>
              <dd>{a.notGoing.length}</dd>
            </div>
            {detail.canManage && r.pendingCount > 0 && (
              <div>
                <dt>Por confirmar</dt>
                <dd>{r.pendingCount}</dd>
              </div>
            )}
          </dl>
        </aside>
      </div>

      {dialog === 'presence' && <PresenceDialog rehearsalId={r.id} onClose={() => setDialog(undefined)} onSaved={() => load(true)} />}
      {dialog === 'edit' && <RehearsalFormDialog rehearsal={r} notes={detail.notes} onClose={() => setDialog(undefined)} onSaved={() => load(true)} />}
      {dialog === 'notice' && <NoticeDialog rehearsal={r} onClose={() => setDialog(undefined)} />}
      {dialog === 'cancel' && <CancelDialog rehearsal={r} onClose={() => setDialog(undefined)} onDone={() => load(true)} />}
    </section>
  );
}

function Counts({ counts }: { counts: InstrumentCount[] }) {
  return (
    <ul className="chips">
      {counts.map((c) => (
        <li key={c.instrument} className="chip">
          {c.instrument} · {c.count}
        </li>
      ))}
    </ul>
  );
}

/** Avatar-led tiles, as "Quem vai" on events; Admin/Owner confirm or remove, a member removes their own. */
function Group({ title, people, rehearsalId, onChanged }: { title: string; people: Attendee[]; rehearsalId: number; onChanged: () => void }) {
  const [removing, setRemoving] = useState<number>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  if (people.length === 0) return null;

  const run = async (call: Promise<Outcome<void>>) => {
    setBusy(true);
    setError(undefined);
    const o = await call;
    setBusy(false);
    setRemoving(undefined);
    if (o.kind === 'ok') onChanged();
    else setError(o.kind === 'closed' ? 'Este ensaio já não permite isto.' : 'Não foi possível guardar. Tenta outra vez.');
  };

  return (
    <div className="presence-group">
      {title && (
        <h3 className="who__group">
          {title} · {people.length}
        </h3>
      )}
      <ul className="who">
        {people.map((p) => (
          <li key={p.id} className="who__person" title={p.member.fullName ?? undefined}>
            <img className="who__avatar" src={p.member.avatarUrl} alt="" loading="lazy" width="72" height="72" />
            <p className="who__name">{p.member.name}</p>
            {p.member.badge && <span className="who__badge">{p.member.badge}</span>}
            {p.status !== 'notGoing' && (
              <span className={p.status === 'approved' ? 'pill pill--yes' : 'pill pill--wait'}>{p.status === 'approved' ? 'Confirmada' : 'Pendente'}</span>
            )}
            {p.instrument && <p className="who__meta">{p.instrument}</p>}
            {p.notes && <p className="who__note">“{p.notes}”</p>}
            {(p.canApprove || p.canRemove) && (
              <div className="presence-actions">
                {p.canApprove && (
                  <button type="button" className="btn btn--primary btn--sm" disabled={busy} onClick={() => run(rehearsalsApi.approve(rehearsalId, p.id))}>
                    <Icon name="check" />
                    Confirmar
                  </button>
                )}
                {p.canRemove &&
                  (removing === p.id ? (
                    <>
                      <button type="button" className="btn btn--danger btn--sm" disabled={busy} onClick={() => run(rehearsalsApi.removeAttendee(rehearsalId, p.id))}>
                        Remover
                      </button>
                      <button type="button" className="btn btn--ghost btn--sm" onClick={() => setRemoving(undefined)}>
                        Não
                      </button>
                    </>
                  ) : (
                    <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => setRemoving(p.id)} aria-label={`Remover a presença de ${p.member.name}`}>
                      <Icon name="trash" />
                    </button>
                  ))}
              </div>
            )}
          </li>
        ))}
      </ul>
      {error && <p className="form__error">{error}</p>}
    </div>
  );
}

/** Admin/Owner, once the rehearsal can be approved: add someone who was there (confirmed, primary instrument). */
function AddAttendee({ rehearsalId, onAdded }: { rehearsalId: number; onAdded: () => void }) {
  const [query, setQuery] = useState('');
  const [members, setMembers] = useState<MemberOption[]>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();

  useEffect(() => {
    if (!query.trim()) return setMembers(undefined);
    const timer = setTimeout(() => {
      rehearsalsApi.members(rehearsalId, query.trim()).then((o) => setMembers(o.kind === 'ok' ? o.data : []));
    }, 250);
    return () => clearTimeout(timer);
  }, [rehearsalId, query]);

  const add = async (m: MemberOption) => {
    setBusy(true);
    const o = await rehearsalsApi.addAttendee(rehearsalId, m.id);
    setBusy(false);
    if (o.kind !== 'ok') return setError(o.kind === 'invalid' ? Object.values(o.errors)[0] : 'Não foi possível acrescentar. Tenta outra vez.');
    setError(undefined);
    setQuery('');
    onAdded();
  };

  return (
    <div className="talk__seat-picker presence-add">
      <label className="control control--sm" htmlFor={id}>
        <span className="sr-only">Acrescentar presença</span>
        <Icon name="search" />
        <input id={id} type="search" placeholder="Acrescentar presença (alcunha ou nome)" value={query} onChange={(e) => setQuery(e.target.value)} />
      </label>
      {members && members.length === 0 && <p className="note">Ninguém encontrado.</p>}
      {members && members.length > 0 && (
        <ul className="talk__picks">
          {members.map((m) => (
            <li key={m.id}>
              <button type="button" disabled={busy} onClick={() => add(m)}>
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
      {error && <p className="form__error">{error}</p>}
    </div>
  );
}
