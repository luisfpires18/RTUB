import { useEffect, useId, useState } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Icon } from './icons';
import { ActiveMembersDialog, BirthdaysDialog, MemberDialog, MemberFace } from './MemberDialogs';
import { membersApi, type Directory, type DirectoryFilters, type MemberCard } from './membersApi';
import { loginTo } from './musicApi';

const CATEGORIES = [
  { value: 'Leitao', label: 'Leitão' },
  { value: 'Caloiro', label: 'Caloiro' },
  { value: 'Tuno', label: 'Tuno' },
  { value: 'TunoHonorario', label: 'Tuno Honorário' },
];
const SUB_CATEGORIES = [
  { value: 'Tuno', label: 'Tuno' },
  { value: 'Veterano', label: 'Veterano' },
  { value: 'Tunossauro', label: 'Tunossauro' },
  { value: 'Fundador', label: 'Fundador' },
];
const PAGE = { members: 30, leitoes: 18 };

function filtersFromUrl(): DirectoryFilters {
  const p = new URLSearchParams(location.search);
  return {
    q: p.get('q') ?? '',
    category: p.get('category') ?? '',
    subCategory: p.get('subCategory') ?? '',
    instrument: p.get('instrument') ?? '',
    activeOnly: p.get('activeOnly') === 'true',
  };
}

/**
 * /members - Membros (React track 017; was the Blazor /members). Signed-in members only. Everyone in the tuna with
 * the old filters (search, category, Tuno sub-category, instrument, só ativos), members by nickname and Leitões by
 * activity; a card opens the member's details (?member=id). "Ativos" and "Aniversários" are the old lists, read-only.
 * Admin and Owner reach the tools that stay on the Blazor /members/manage.
 */
export default function Members() {
  const [directory, setDirectory] = useState<Directory | 'signin' | null>();
  const [filters, setFilters] = useState(filtersFromUrl);
  const [query, setQuery] = useState(filters.q);
  const [open, setOpen] = useState<string | null>(new URLSearchParams(location.search).get('member'));
  const [dialog, setDialog] = useState<'active' | 'birthdays'>();
  const [shown, setShown] = useState(PAGE);
  const ids = { q: useId(), category: useId(), sub: useId(), instrument: useId(), active: useId() };

  useEffect(() => {
    document.title = 'Membros · RTUB';
  }, []);

  // The search waits for a pause in typing; the selects apply at once.
  useEffect(() => {
    const timer = setTimeout(() => setFilters((f) => (f.q === query ? f : { ...f, q: query })), 250);
    return () => clearTimeout(timer);
  }, [query]);

  useEffect(() => {
    const url = new URL(location.href);
    for (const [k, v] of Object.entries(filters)) {
      if (v) url.searchParams.set(k, String(v));
      else url.searchParams.delete(k);
    }
    history.replaceState(null, '', url.pathname + url.search);
    setShown(PAGE);
    membersApi.directory(filters).then((o) => setDirectory(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null));
  }, [filters]);

  const openMember = (id: string | null) => {
    const url = new URL(location.href);
    if (id) url.searchParams.set('member', id);
    else url.searchParams.delete('member');
    history.replaceState(null, '', url.pathname + url.search);
    setOpen(id);
  };

  const data = directory && directory !== 'signin' ? directory : null;

  return (
    <section className="page wrap events-page members-page" aria-labelledby="members-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="members-title" className="page__title">
            Membros
          </h1>
          <p className="page__lead">Toda a RTUB: tunos, caloiros e leitões, com o instrumento e o cargo deste ano.</p>
        </div>
        {data && (
          <div className="events-page__actions">
            <a className="btn btn--ghost btn--sm" href={portal.membersHierarchy}>
              <Icon name="person" />
              Hierarquia
            </a>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDialog('active')}>
              <Icon name="check" />
              Ativos
            </button>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDialog('birthdays')}>
              <Icon name="calendar" />
              Aniversários
            </button>
            {data.canManage && (
              <a className="btn btn--primary btn--sm" href="/members/manage">
                <Icon name="pencil" />
                Gerir membros
              </a>
            )}
          </div>
        )}
      </header>

      {directory === 'signin' ? (
        <div className="notice" role="status">
          <p>A lista de membros é da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.members)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : directory === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir a lista de membros agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => setFilters({ ...filters })}>
            Tentar novamente
          </button>
        </div>
      ) : (
        <>
          <div className="events-tools members-tools" role="search">
            <label className="control" htmlFor={ids.q}>
              <span className="sr-only">Procurar membro</span>
              <Icon name="search" />
              <input id={ids.q} type="search" placeholder="Procurar por nome, alcunha, email, telefone ou cidade" value={query} onChange={(e) => setQuery(e.target.value)} />
            </label>
            <label className="control control--select" htmlFor={ids.category}>
              <span className="sr-only">Categoria</span>
              <select id={ids.category} value={filters.category} onChange={(e) => setFilters({ ...filters, category: e.target.value, subCategory: '' })}>
                <option value="">Todas as categorias</option>
                {CATEGORIES.map((c) => (
                  <option key={c.value} value={c.value}>
                    {c.label}
                  </option>
                ))}
              </select>
            </label>
            {filters.category === 'Tuno' && (
              <label className="control control--select" htmlFor={ids.sub}>
                <span className="sr-only">Sub-categoria</span>
                <select id={ids.sub} value={filters.subCategory} onChange={(e) => setFilters({ ...filters, subCategory: e.target.value })}>
                  <option value="">Todas as sub-categorias</option>
                  {SUB_CATEGORIES.map((c) => (
                    <option key={c.value} value={c.value}>
                      {c.label}
                    </option>
                  ))}
                </select>
              </label>
            )}
            <label className="control control--select" htmlFor={ids.instrument}>
              <span className="sr-only">Instrumento</span>
              <select id={ids.instrument} value={filters.instrument} onChange={(e) => setFilters({ ...filters, instrument: e.target.value })}>
                <option value="">Todos os instrumentos</option>
                {data?.instruments.map((i) => (
                  <option key={i.value} value={i.value}>
                    {i.label}
                  </option>
                ))}
              </select>
            </label>
            <label className="check" htmlFor={ids.active}>
              <input id={ids.active} type="checkbox" checked={filters.activeOnly} onChange={(e) => setFilters({ ...filters, activeOnly: e.target.checked })} />
              Só ativos
            </label>
          </div>

          {!data ? (
            <Loading label="A carregar os membros…" />
          ) : data.members.length + data.leitoes.length === 0 ? (
            <p className="note">Nenhum membro corresponde à pesquisa.</p>
          ) : (
            <>
              {data.members.length > 0 && (
                <Group title="Membros" people={data.members} limit={shown.members} onMore={() => setShown({ ...shown, members: shown.members + PAGE.members })} onOpen={openMember} />
              )}
              {data.leitoes.length > 0 && (
                <Group title="Leitões" people={data.leitoes} limit={shown.leitoes} onMore={() => setShown({ ...shown, leitoes: shown.leitoes + PAGE.leitoes })} onOpen={openMember} />
              )}
            </>
          )}
        </>
      )}

      {open && <MemberDialog memberId={open} onClose={() => openMember(null)} />}
      {dialog === 'active' && <ActiveMembersDialog onClose={() => setDialog(undefined)} />}
      {dialog === 'birthdays' && <BirthdaysDialog onClose={() => setDialog(undefined)} />}
    </section>
  );
}

function Group({ title, people, limit, onMore, onOpen }: { title: string; people: MemberCard[]; limit: number; onMore: () => void; onOpen: (id: string) => void }) {
  return (
    <section className="events-block" aria-label={title}>
      <h2 className="events-block__title">
        {title} · {people.length}
      </h2>
      <ul className="who members-grid">
        {people.slice(0, limit).map((p) => (
          <li key={p.id}>
            <button type="button" className={p.expelled ? 'who__person member-card member-card--expelled' : 'who__person member-card'} onClick={() => onOpen(p.id)}>
              <span className={p.online ? 'member-card__online is-on' : 'member-card__online'}>{p.online ? 'Online' : 'Offline'}</span>
              <MemberFace avatarUrl={p.avatarUrl} size={72} />
              <span className="who__name">{p.displayName}</span>
              {p.fullName && <span className="who__meta">{p.fullName}</span>}
              <span className="member-badges">
                {p.badges.map((b) => (
                  <span key={b.kind} className={`member-badge member-badge--${b.kind}`}>
                    {b.label}
                  </span>
                ))}
                {p.position && <span className="member-badge member-badge--position">{p.position}</span>}
                {p.noRoles && <span className="member-badge">Sem cargos</span>}
              </span>
              {p.instrument && <span className="who__meta">{p.instrument}</span>}
              {p.inactive && (
                <span className="member-card__inactive">
                  <Icon name="clock" />
                  Não tem participado
                </span>
              )}
            </button>
          </li>
        ))}
      </ul>
      {people.length > limit && (
        <button type="button" className="btn btn--ghost btn--sm members-more" onClick={onMore}>
          Mostrar mais ({people.length - limit})
        </button>
      )}
    </section>
  );
}
