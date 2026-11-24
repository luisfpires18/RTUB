// /trintuna: on a past gathering, the Owner or its organizer marks who went (Fui). A mark is the person's own Fui
// answer, so "Quem foi" and the Hall of Fame count it (TrintunaService.Attendance.cs). Removing a mark is the X on the
// "Quem foi" tile, with a confirmation (GatheringDialog).
import { useMemo, useState } from 'react';
import { MemberPicker } from './LogisticsFields';
import { trintunaApi, problem, type Gathering, type Rank } from '../data/trintunaApi';
import { Banner } from './ui/Banner';

/** `people`: everyone who can see the page (the Hall of Fame list), by key; no user ids. Searched by nickname or name. */
export function MarkWhoWent({ gathering: g, people, onChanged }: { gathering: Gathering; people: Rank[]; onChanged: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const options = useMemo(() => people.map((p) => ({ id: p.key, displayName: p.displayName, fullName: p.fullName, avatarUrl: p.avatarUrl })), [people]);

  const mark = async (key: string) => {
    if (busy) return; // one write at a time
    setBusy(true);
    setError(undefined);
    const o = await trintunaApi.mark(g.id, key);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'marcar quem foi'));
    onChanged();
  };

  return (
    <div className="form__field" aria-busy={busy}>
      <span className="form__label">Marcar quem foi</span>
      <Banner text={error} />
      <MemberPicker label="Marcar quem foi" options={options} exclude={g.going.map((p) => p.key)} onPick={(m) => mark(m.id)} />
    </div>
  );
}
