// /trintuna: on a past gathering, the Owner or its organizer marks who went (Fui) and removes a Fui. A mark is the
// person's own Fui answer, so "Quem foi" and the Hall of Fame count it (TrintunaService.Attendance.cs).
import { useMemo, useState } from 'react';
import { MemberPicker } from './LogisticsFields';
import type { Outcome } from '../data/api';
import { trintunaApi, problem, type Gathering, type Person } from '../data/trintunaApi';
import { Banner } from './ui/Banner';
import { PersonChip } from './ui/PersonChip';

/** `people`: everyone who can see the page (the Hall of Fame list), by key; no user ids. */
export function MarkWhoWent({ gathering: g, people, onChanged }: { gathering: Gathering; people: Person[]; onChanged: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const options = useMemo(() => people.map((p) => ({ id: p.key, displayName: p.displayName, fullName: null, avatarUrl: p.avatarUrl })), [people]);

  const run = async (write: () => Promise<Outcome<unknown>>, what: string) => {
    if (busy) return; // one write at a time
    setBusy(true);
    setError(undefined);
    const o = await write();
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, what));
    onChanged();
  };

  return (
    <div className="form__field" aria-busy={busy}>
      <span className="form__label">Marcar quem foi</span>
      <Banner text={error} />
      {g.going.length > 0 && (
        <div className="chips">
          {g.going.map((p) => (
            <PersonChip
              key={p.key}
              name={p.displayName}
              avatar={p.avatarUrl}
              onRemove={() => run(() => trintunaApi.unmark(g.id, p.key), 'retirar o Fui')}
              removeLabel={`Retirar Fui a ${p.displayName}`}
            />
          ))}
        </div>
      )}
      <MemberPicker
        label="Marcar quem foi"
        options={options}
        exclude={g.going.map((p) => p.key)}
        onPick={(m) => run(() => trintunaApi.mark(g.id, m.id), 'marcar quem foi')}
      />
    </div>
  );
}
