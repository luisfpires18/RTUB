// /trintuna dialogs: one gathering (description, organizer, date poll, Vou / Não vou, who answered), the Top 3 and
// the Owner's access list.
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { PollCard } from './MeetingPollCard';
import { MemberPicker } from './LogisticsFields';
import type { Outcome } from '../data/api';
import { trintunaApi, problem, type Gathering, type Member, type Person, type Rank } from '../data/trintunaApi';
import { medal } from '../data/leaderboardApi';
import { dateLabel } from '../data/eventsDates';
import { WhoIsGoing } from './EventDetailPeople';
import { AnswerActions } from './ui/AnswerActions';
import { Avatar } from './ui/Avatar';
import { Banner } from './ui/Banner';
import { CloseFooter, DialogFooter } from './ui/DialogFooter';
import { EmptyNote } from './ui/EmptyNote';
import { Loading } from './ui/Loading';
import { PersonChip } from './ui/PersonChip';

/** Vou / Não vou, with the answer markup of /events; past gatherings take answers too. */
function Presence({ gathering, onChanged, onClose }: { gathering: Gathering; onChanged: () => void; onClose: () => void }) {
  const stored = gathering.myStatus === 'going' ? true : gathering.myStatus === 'notGoing' ? false : null;
  const [choice, setChoice] = useState<boolean | null>(stored);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const name = useId();

  useEffect(() => setChoice(stored), [stored]);

  const run = async (call: () => Promise<Outcome<unknown>>, what: string) => {
    setBusy(true);
    setError(undefined);
    const o = await call();
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, what));
    onChanged();
  };

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (choice !== null) run(() => trintunaApi.answer(gathering.id, choice), 'guardar a resposta');
  };

  return (
    <form className="answer" onSubmit={submit}>
      <fieldset className="choice">
        <legend className="sr-only">A tua resposta</legend>
        <div className="choice__options">
          <label className={choice === true ? 'choice__option choice__option--yes is-on' : 'choice__option choice__option--yes'}>
            <input type="radio" name={name} checked={choice === true} onChange={() => setChoice(true)} />
            <Icon name="check" />
            Vou
          </label>
          <label className={choice === false ? 'choice__option choice__option--no is-on' : 'choice__option choice__option--no'}>
            <input type="radio" name={name} checked={choice === false} onChange={() => setChoice(false)} />
            <Icon name="close" />
            Não vou
          </label>
        </div>
      </fieldset>
      <Banner text={error} />
      <AnswerActions cancelLabel="Fechar" onCancel={onClose} label="Guardar" busy={busy} disabled={choice === null || choice === stored} />
      {stored !== null && (
        <button type="button" className="btn btn--ghost btn--sm" onClick={() => run(() => trintunaApi.withdraw(gathering.id), 'retirar a resposta')} disabled={busy}>
          Retirar a minha resposta
        </button>
      )}
    </form>
  );
}

/** "Quem vai / Quem foi" of /events: names and photos only. */
const asParticipant = (p: Person) => ({ name: p.displayName, fullName: null, avatarUrl: p.avatarUrl ?? '', badge: null, instrument: null, notes: null });

/** One gathering: what, who organizes, the date poll, the caller's answer (past ones too) and who answered. */
export function GatheringDialog({ gathering: g, onClose, onChanged }: { gathering: Gathering; onClose: () => void; onChanged: () => void }) {
  return (
    <Dialog title={`${g.title} · ${g.place}`} onClose={onClose}>
      <p className="agenda-card__meta">
        <Icon name="clock" />
        <span>{g.date ? dateLabel({ date: g.date, endDate: null }) : 'Data por decidir'}</span>
      </p>
      {g.description && <p>{g.description}</p>}
      {g.organizer && (
        <p className="agenda-card__meta">
          <Icon name="person" />
          <span>Organiza: {g.organizer.displayName}</span>
        </p>
      )}
      {g.poll.options.length > 0 ? (
        <PollCard poll={g.poll} onChanged={onChanged} onVote={(optionId) => trintunaApi.vote(g.id, optionId)} />
      ) : (
        !g.date && !g.past && <EmptyNote>Ainda não há datas possíveis para votar.</EmptyNote>
      )}
      <Presence gathering={g} onChanged={onChanged} onClose={onClose} />
      <WhoIsGoing participants={{ going: g.going.map(asParticipant), leitoes: [], notGoing: g.notGoing.map(asParticipant) }} past={g.past} />
    </Dialog>
  );
}

/** "Top 3": who went to the most past almoços, in the /leaderboard row look; ties share a medal. */
export function TopDialog({ top, onClose }: { top: Rank[]; onClose: () => void }) {
  return (
    <Dialog title="Top 3" onClose={onClose} footer={<CloseFooter onClose={onClose} />}>
      {top.length === 0 ? (
        <EmptyNote>Ainda ninguém foi a um almoço.</EmptyNote>
      ) : (
        <ol className="leaderboard-rows" aria-label="Top 3">
          {/* No user ids reach the page: name + photo identify a ranked member. */}
          {top.map((r) => (
            <li key={`${r.displayName}|${r.avatarUrl ?? ''}`}>
              <div className={`leaderboard-row leaderboard-row--top${r.place} leaderboard-row--static`}>
                <span className="leaderboard-row__pos" aria-label={`${r.place}.º lugar`}>
                  {medal(r.place)}
                </span>
                <Avatar className="who__avatar" src={r.avatarUrl} size={48} />
                <span className="leaderboard-row__who">
                  <strong>{r.displayName}</strong>
                </span>
                <span className="leaderboard-row__score">
                  <strong>{r.count === 1 ? '1 almoço' : `${r.count} almoços`}</strong>
                </span>
              </div>
            </li>
          ))}
        </ol>
      )}
    </Dialog>
  );
}

/** "Quem tem acesso" (Owner): the one access list of the page, the layout of the Logística access field. */
export function AccessDialog({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const [members, setMembers] = useState<Member[]>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    trintunaApi.access().then((o) => {
      if (o.kind === 'ok') setMembers(o.data);
      else setError(problem(o, 'abrir a lista de acesso'));
    });
  }, []);

  const save = async () => {
    if (!members) return;
    setBusy(true);
    setError(undefined);
    const o = await trintunaApi.setAccess(members.map((m) => m.id));
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'guardar a lista de acesso'));
    onSaved();
    onClose();
  };

  return (
    <Dialog title="Quem tem acesso" onClose={onClose} footer={<DialogFooter busy={busy} disabled={!members} onClose={onClose} onSubmit={save} />}>
      <Banner text={error} />
      {members === undefined ? (
        !error && <Loading label="A carregar…" />
      ) : (
        <div className="form__field">
          <span className="form__label">Lista de acesso</span>
          {members.length > 0 ? (
            <div className="chips">
              {members.map((m) => (
                <PersonChip
                  key={m.id}
                  name={m.displayName}
                  avatar={m.avatarUrl}
                  onRemove={() => setMembers(members.filter((x) => x.id !== m.id))}
                  removeLabel={`Retirar o acesso a ${m.displayName}`}
                />
              ))}
            </div>
          ) : (
            <EmptyNote>Ainda ninguém na lista de acesso.</EmptyNote>
          )}
          <MemberPicker
            label="Dar acesso a"
            exclude={members.map((m) => m.id)}
            onPick={(m) => setMembers([...members, { id: m.id, displayName: m.displayName, avatarUrl: m.avatarUrl }])}
          />
          <small className="note">Quem está na lista vê esta página, vota nas datas e responde Vou / Não vou.</small>
        </div>
      )}
    </Dialog>
  );
}
