// /trintuna dialogs: one gathering (description, organizer, date poll, Vou / Não vou or Fui / Não fui, who answered,
// its share link), the Hall of Fame and the Owner's access list.
import { useEffect, useId, useState } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { PollCard } from './MeetingPollCard';
import { MemberPicker } from './LogisticsFields';
import type { Outcome } from '../data/api';
import { portal } from '../data/content';
import { trintunaApi, problem, type Gathering, type Member, type Person, type Rank } from '../data/trintunaApi';
import { medal } from '../data/leaderboardApi';
import { dateLabel } from '../data/eventsDates';
import { WhoIsGoing } from './EventDetailPeople';
import { Avatar } from './ui/Avatar';
import { Banner } from './ui/Banner';
import { CloseFooter, DialogFooter } from './ui/DialogFooter';
import { EmptyNote } from './ui/EmptyNote';
import { Loading } from './ui/Loading';
import { PersonChip } from './ui/PersonChip';
import { SimplePager } from './ui/SimplePager';

/** Hall of Fame rows per page (the list is whole in the board; the dialog slices it). */
const HALL_PAGE = 15;

/** Vou / Não vou, with the answer markup of /events; past gatherings take answers too, worded Fui / Não fui. */
function Presence({ gathering, onChanged }: { gathering: Gathering; onChanged: () => void }) {
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
    if (o.kind !== 'ok') {
      setChoice(stored); // the saved answer stays shown when the save fails
      return setError(problem(o, what));
    }
    onChanged();
  };

  // Picking an answer saves it at once (no Guardar step).
  const pick = (value: boolean) => {
    if (busy || value === stored) return setChoice(value);
    setChoice(value);
    run(() => trintunaApi.answer(gathering.id, value), 'guardar a resposta');
  };

  return (
    <div className="answer">
      <fieldset className="choice" disabled={busy}>
        <legend className="sr-only">A tua resposta</legend>
        <div className="choice__options">
          <label className={choice === true ? 'choice__option choice__option--yes is-on' : 'choice__option choice__option--yes'}>
            <input type="radio" name={name} checked={choice === true} onChange={() => pick(true)} />
            <Icon name="check" />
            {gathering.past ? 'Fui' : 'Vou'}
          </label>
          <label className={choice === false ? 'choice__option choice__option--no is-on' : 'choice__option choice__option--no'}>
            <input type="radio" name={name} checked={choice === false} onChange={() => pick(false)} />
            <Icon name="close" />
            {gathering.past ? 'Não fui' : 'Não vou'}
          </label>
        </div>
      </fieldset>
      <Banner text={error} />
      {stored !== null && (
        <button type="button" className="btn btn--ghost btn--sm" onClick={() => run(() => trintunaApi.withdraw(gathering.id), 'retirar a resposta')} disabled={busy}>
          Retirar a minha resposta
        </button>
      )}
    </div>
  );
}

/** "Quem vai / Quem foi" of /events: names and photos only. */
const asParticipant = (p: Person) => ({ key: p.key, name: p.displayName, fullName: null, avatarUrl: p.avatarUrl ?? '', badge: null, instrument: null, notes: null });

/** One gathering: what, who organizes, the date poll, the caller's answer (past ones too), who answered, its link. */
export function GatheringDialog({ gathering: g, onClose, onChanged }: { gathering: Gathering; onClose: () => void; onChanged: () => void }) {
  const [copied, setCopied] = useState(false);
  const [linkError, setLinkError] = useState<string>();

  // The MeetingPollCard pattern: the link opens this gathering for the Owner and listed members; others see the notice.
  const copyLink = async () => {
    const link = `${location.origin}${portal.trintuna}?almoco=${g.id}`;
    try {
      await navigator.clipboard.writeText(link);
      setLinkError(undefined);
      setCopied(true);
    } catch {
      // The browser refused the clipboard: show the link to select by hand.
      setLinkError(`Não foi possível copiar. Link: ${link}`);
    }
  };

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
      <Presence gathering={g} onChanged={onChanged} />
      <WhoIsGoing participants={{ going: g.going.map(asParticipant), leitoes: [], notGoing: g.notGoing.map(asParticipant) }} past={g.past} />
      <Banner text={linkError} />
      <button type="button" className="btn btn--ghost btn--sm" onClick={copyLink}>
        <Icon name={copied ? 'check' : 'link'} />
        <span role="status">{copied ? 'Link copiado' : 'Copiar link'}</span>
      </button>
    </Dialog>
  );
}

/**
 * "Hall of Fame": everyone who can see the page, by past almoços (zeros included), in the /leaderboard row look; ties
 * share a place, medals for places 1-3, 15 per page.
 */
export function HallOfFameDialog({ ranks, onClose }: { ranks: Rank[]; onClose: () => void }) {
  const [page, setPage] = useState(1);
  const pages = Math.ceil(ranks.length / HALL_PAGE);
  const shown = ranks.slice((page - 1) * HALL_PAGE, page * HALL_PAGE);
  return (
    <Dialog title="Hall of Fame" onClose={onClose} footer={<CloseFooter onClose={onClose} />}>
      {ranks.length === 0 ? (
        <EmptyNote>Ainda ninguém foi a um almoço.</EmptyNote>
      ) : (
        <>
          <ol className="leaderboard-rows" aria-label="Hall of Fame">
            {shown.map((r) => (
              <li key={r.key}>
                <div className={`leaderboard-row${r.place <= 3 ? ` leaderboard-row--top${r.place}` : ''} leaderboard-row--static`}>
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
          <SimplePager page={page} pages={pages} onPage={setPage} words />
        </>
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
