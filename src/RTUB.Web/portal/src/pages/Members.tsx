import { useEffect, useId, useState } from 'react';
import { PageHead } from '../components/ui/PageHead';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { MemberDialog } from '../components/MemberDialogs';
import { ActiveMembersDialog, BirthdaysDialog } from '../components/MemberListDialogs';
import { MemberInfoDialog } from '../components/MemberInfoDialog';
import { membersApi, type Directory, type DirectoryFilters } from '../data/membersApi';
import { RetryNotice, SignInNotice } from '../components/Notice';
import { Group } from '../components/MemberGroup';
import { MemberTools, type Tool, ToolDialog } from '../components/MemberTools';
import { Loading } from '../components/ui/Loading';
import { ToolsBar, SearchField, SelectField } from '../components/ui/SearchTools';
import { EmptyNote } from '../components/ui/EmptyNote';
import { IconAction } from '../components/ui/IconAction';

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
 * /members - Membros. Signed-in members only. Everyone in the tuna with
 * filters (search, category, Tuno sub-category, instrument, só ativos), members by nickname and Leitões by
 * activity; a card opens the member's details (?member=id). "Ativos" and "Aniversários" are separate lists.
 * Admin and Owner also add, edit, expel / reactivate Leitões, set a Leitão's nickname, send the
 * push reminder and delete. Owner deletes any member, Admin Leitões.
 */
export default function Members() {
  const [directory, setDirectory] = useState<Directory | 'signin' | null>();
  const [filters, setFilters] = useState(filtersFromUrl);
  const [query, setQuery] = useState(filters.q);
  const [open, setOpen] = useState<string | null>(new URLSearchParams(location.search).get('member'));
  // ?info={id} (from "As minhas ações pendentes" → Membros) opens "Completar informação" once.
  const [info, setInfo] = useState<string | null>(new URLSearchParams(location.search).get('info'));
  const [dialog, setDialog] = useState<'active' | 'birthdays'>();
  const [shown, setShown] = useState(PAGE);
  const [tool, setTool] = useState<Tool>();
  // Bumped after a change, so the open details load again.
  const [version, setVersion] = useState(0);
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
    let live = true; // a slower answer for older filters never replaces the newer one
    membersApi.directory(filters).then((o) => live && setDirectory(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null));
    return () => {
      live = false;
    };
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
      <PageHead eyebrow="Área de membros" title="Membros" titleId="members-title" lead="Toda a RTUB: tunos, caloiros e leitões, com o instrumento e o cargo deste ano.">
        {/* Shown while the list loads too, so they don't push the search bar down when it arrives (CLS). */}
        {directory !== 'signin' && directory !== null && (
          <div className="events-page__actions">
            <a className="btn btn--ghost btn--sm" href={portal.membersHierarchy}>
              <Icon name="person" />
              Hierarquia
            </a>
            <a className="btn btn--ghost btn--sm" href={portal.membersMap}>
              <Icon name="geo" />
              Mapa
            </a>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDialog('active')}>
              <Icon name="check" />
              Ativos
            </button>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDialog('birthdays')}>
              <Icon name="calendar" />
              Aniversários
            </button>
            {(data?.canManage || data?.canEditInfo) && (
              <button type="button" className="btn btn--primary btn--sm" onClick={() => setTool({ kind: 'create' })}>
                <Icon name="plus" />
                Adicionar membro
              </button>
            )}
          </div>
        )}
      </PageHead>

      {directory === 'signin' ? (
        <SignInNotice path={portal.members}>A lista de membros é da área de membros.</SignInNotice>
      ) : directory === null ? (
        <RetryNotice onRetry={() => setFilters({ ...filters })}>Não conseguimos abrir a lista de membros agora.</RetryNotice>
      ) : (
        <>
          <ToolsBar className="members-tools">
            <SearchField id={ids.q} label="Procurar membro" placeholder="Procurar por nome, alcunha, email, telefone ou cidade" value={query} onChange={setQuery} />
            <SelectField id={ids.category} label="Categoria" value={filters.category} onChange={(v) => setFilters({ ...filters, category: v, subCategory: '' })}>
              <option value="">Todas as categorias</option>
              {CATEGORIES.map((c) => (
                <option key={c.value} value={c.value}>
                  {c.label}
                </option>
              ))}
            </SelectField>
            {filters.category === 'Tuno' && (
              <SelectField id={ids.sub} label="Sub-categoria" value={filters.subCategory} onChange={(v) => setFilters({ ...filters, subCategory: v })}>
                <option value="">Todas as sub-categorias</option>
                {SUB_CATEGORIES.map((c) => (
                  <option key={c.value} value={c.value}>
                    {c.label}
                  </option>
                ))}
              </SelectField>
            )}
            <SelectField id={ids.instrument} label="Instrumento" value={filters.instrument} onChange={(v) => setFilters({ ...filters, instrument: v })}>
              <option value="">Todos os instrumentos</option>
              {data?.instruments.map((i) => (
                <option key={i.value} value={i.value}>
                  {i.label}
                </option>
              ))}
            </SelectField>
            <label className="check" htmlFor={ids.active}>
              <input id={ids.active} type="checkbox" checked={filters.activeOnly} onChange={(e) => setFilters({ ...filters, activeOnly: e.target.checked })} />
              Só ativos
            </label>
          </ToolsBar>

          {!data ? (
            <Loading label="A carregar os membros…" />
          ) : data.members.length + data.leitoes.length === 0 ? (
            <EmptyNote>Nenhum membro corresponde à pesquisa.</EmptyNote>
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

      {open && (
        <MemberDialog
          key={`${open}-${version}`}
          memberId={open}
          onClose={() => openMember(null)}
          actions={
            data?.canManage
              ? (m) => <MemberTools m={m} canDelete={m.leitao || data.canDeleteMembers} onPick={setTool} />
              : data?.canEditInfo
                ? (m) => (
                    <button type="button" className="btn btn--ghost btn--sm" onClick={() => setInfo(m.id)}>
                      <Icon name="pencil" />
                      Completar informação
                    </button>
                  )
                : undefined
          }
        />
      )}
      {dialog === 'active' && (
        <ActiveMembersDialog
          onClose={() => setDialog(undefined)}
          rowActions={
            data?.canManage
              ? (m, reload) => (
                  <>
                    {m.encourage && (
                      <IconAction icon="bell" label={`Enviar lembrete push a ${m.displayName}`} onClick={() => setTool({ kind: 'reminder', member: m, reload })} large />
                    )}
                  </>
                )
              : undefined
          }
        />
      )}
      {dialog === 'birthdays' && <BirthdaysDialog onClose={() => setDialog(undefined)} />}

      {info && data?.canEditInfo && (
        <MemberInfoDialog
          memberId={info}
          instrumentOptions={data.instruments}
          onClose={() => {
            setInfo(null);
            const url = new URL(location.href);
            url.searchParams.delete('info');
            history.replaceState(null, '', url.pathname + url.search);
          }}
          onSaved={() => {
            setFilters({ ...filters });
            setVersion((v) => v + 1);
          }}
        />
      )}

      {tool && data && (
        <ToolDialog
          tool={tool}
          instruments={data.instruments}
          onClose={() => setTool(undefined)}
          onChanged={(deleted) => {
            setFilters({ ...filters });
            if (deleted) openMember(null);
            else setVersion((v) => v + 1);
          }}
        />
      )}
    </section>
  );
}
