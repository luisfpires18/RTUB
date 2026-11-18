import { useEffect, useId, useState } from 'react';
import { portal } from '../data/content';
import { Icon } from './icons';
import { treasuryApi, type Person } from '../data/treasuryApi';
import { Notice, RetryNotice, SignInNotice, LinkNotice } from './Notice';
import { EmptyNote } from './ui/EmptyNote';
import { Avatar } from './ui/Avatar';

// Shared pieces of the React /treasury pages.

/** The four Tesouraria areas ("Ver Calotes", "Ver MBWAY", "Nerba"). */
export function TreasuryTabs({ current, fiscalYear }: { current: 'reports' | 'calotes' | 'mbway' | 'nerba'; fiscalYear?: string }) {
  const tabs = [
    { key: 'reports', label: 'Relatórios', href: portal.treasury },
    { key: 'calotes', label: 'Calotes', href: fiscalYear ? `${portal.treasuryCalotes}?fy=${encodeURIComponent(fiscalYear)}` : portal.treasuryCalotes },
    { key: 'mbway', label: 'MBWAY', href: portal.treasuryMbway },
    { key: 'nerba', label: 'Nerba', href: portal.treasuryNerba },
  ];
  return (
    <nav className="tr-tabs" aria-label="Tesouraria">
      {tabs.map((t) => (
        <a key={t.key} href={t.href} className="tr-tabs__tab" aria-current={t.key === current ? 'page' : undefined}>
          {t.label}
        </a>
      ))}
    </nav>
  );
}

/** What a page shows when it cannot show its data: sign in, not for you, failed. */
export function Refusal({ state, path, onRetry }: { state: 'signin' | 'forbidden' | 'failed' | 'missing'; path: string; onRetry: () => void }) {
  if (state === 'signin') {
    return (
      <SignInNotice path={path}>A tesouraria é da área de membros.</SignInNotice>
    );
  }
  if (state === 'forbidden') {
    return (
      <LinkNotice href={portal.treasuryCalotes} label="Os meus calotes">A tesouraria não está disponível para Caloiros e Leitões. Os teus calotes estão em Calotes.</LinkNotice>
    );
  }
  if (state === 'missing') {
    return (
      <Notice>Isto não existe ou já foi apagado.</Notice>
    );
  }
  return (
    <RetryNotice onRetry={onRetry}>Não conseguimos abrir a tesouraria agora.</RetryNotice>
  );
}

/** Search members (managers only): names and avatars; the server also matches e-mail, never returns it. */
export function MemberPicker({ forDebts, label, onPick }: { forDebts: boolean; label: string; onPick: (m: Person) => void }) {
  const [query, setQuery] = useState('');
  const [found, setFound] = useState<Person[]>();
  const id = useId();

  useEffect(() => {
    if (!query.trim()) return setFound(undefined);
    let live = true; // a slower answer for an older query never replaces the newer one
    const timer = setTimeout(() => treasuryApi.members(query.trim(), forDebts).then((o) => live && setFound(o.kind === 'ok' ? o.data : [])), 250);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [query, forDebts]);

  return (
    <div className="lx-picker">
      <label className="sr-only" htmlFor={id}>
        {label}
      </label>
      <span className="control control--sm">
        <Icon name="search" />
        <input id={id} type="search" placeholder="Procurar por alcunha, nome ou e-mail" value={query} onChange={(e) => setQuery(e.target.value)} />
      </span>
      {found && found.length === 0 && <EmptyNote>Ninguém encontrado.</EmptyNote>}
      {found && found.length > 0 && (
        <ul className="talk__picks">
          {found.map((m) => (
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
