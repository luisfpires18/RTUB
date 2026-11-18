// Form pieces shared by the logistics dialogs and panels: field error, member picker, person chip.
import { useEffect, useId, useState } from 'react';
import { Icon } from './icons';
import { logisticsApi, type MemberOption } from '../data/logisticsApi';
import { Avatar } from './ui/Avatar';
import { EmptyNote } from './ui/EmptyNote';

/**
 * Search members by nickname or name (the server returns at most 20) and pick one. With `boardId`: only
 * members who may see that board (a private board's access list, Mod, Admin, Owner).
 */
export function MemberPicker({
  label,
  exclude = [],
  onPick,
  boardId,
}: {
  label: string;
  exclude?: string[];
  onPick: (m: MemberOption) => void;
  boardId?: number;
}) {
  const [query, setQuery] = useState('');
  const [found, setFound] = useState<MemberOption[]>();
  const id = useId();

  useEffect(() => {
    if (!query.trim()) return setFound(undefined);
    let live = true; // a slower answer for an older query never replaces the newer one
    const timer = setTimeout(() => logisticsApi.members(query.trim(), boardId).then((o) => live && setFound(o.kind === 'ok' ? o.data : [])), 250);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [query, boardId]);

  const shown = found?.filter((m) => !exclude.includes(m.id));
  return (
    <div className="lx-picker">
      <label className="sr-only" htmlFor={id}>
        {label}
      </label>
      <span className="control control--sm">
        <Icon name="search" />
        <input id={id} type="search" placeholder="Procurar por alcunha ou nome" value={query} onChange={(e) => setQuery(e.target.value)} />
      </span>
      {shown && shown.length === 0 && <EmptyNote>Ninguém encontrado.</EmptyNote>}
      {shown && shown.length > 0 && (
        <ul className="talk__picks">
          {shown.map((m) => (
            <li key={m.id}>
              <button
                type="button"
                onClick={() => {
                  onPick(m);
                  setQuery('');
                }}
              >
                <Avatar src={m.avatarUrl} size={28} />
                <span>
                  {m.displayName}
                  {m.fullName && <small>{m.fullName}</small>}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
