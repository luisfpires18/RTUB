import { useEffect, useId, useState } from 'react';
import { useLatest } from '../components/AppShared';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { MemberBadge } from '../components/MemberBadge';
import { RetryNotice, SignInNotice } from '../components/Notice';
import { type Card, type List, api, PAGE, conditionClass } from '../data/inventoryApi';
import { Thumb, DetailDialog, DeleteDialog } from '../components/InventoryDialogs';
import { FormDialog } from '../components/InventoryForm';
import { Loading } from '../components/ui/Loading';
import { PageHead } from '../components/ui/PageHead';
import { ToolsBar, SearchField, SelectField } from '../components/ui/SearchTools';
import { EmptyNote } from '../components/ui/EmptyNote';
import { LoadMore } from '../components/ui/LoadMore';
import { IconAction } from '../components/ui/IconAction';

// manage. The server decides every rule (InstrumentInventoryService); hiding a button here is only a convenience.

/**
 * /inventory - Instrumentos. Signed-in members only. Every instrument of the
 * RTUB by name, with counters, search (name, type, brand) and type / condition filters; a card opens its
 * details. Mod, Admin and Owner add, edit (image included) and delete.
 */
export default function Inventory() {
  const [list, setList] = useState<List | 'signin' | null>();
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [category, setCategory] = useState('');
  const [condition, setCondition] = useState('');
  const [shown, setShown] = useState(PAGE);
  const [open, setOpen] = useState<number>();
  const [form, setForm] = useState<{ id?: number }>();
  const [deleting, setDeleting] = useState<Card>();
  const ids = { q: useId(), category: useId(), condition: useId() };

  useEffect(() => {
    document.title = 'Instrumentos · RTUB';
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);

  const latest = useLatest();
  const load = () => latest(api.list(q, category, condition), (o) => setList(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null));

  useEffect(() => {
    setShown(PAGE);
    load();
  }, [q, category, condition]);

  const data = list && list !== 'signin' ? list : null;

  return (
    <section className="page wrap events-page inventory-page" aria-labelledby="inventory-title">
      <PageHead eyebrow="Área de membros" title="Instrumentos" titleId="inventory-title" lead="O material da tuna: onde está, de que marca é e em que estado.">
        {data?.canManage && (
          <div className="events-page__actions">
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setForm({})}>
              <Icon name="plus" />
              Adicionar instrumento
            </button>
          </div>
        )}
      </PageHead>

      {list === 'signin' ? (
        <SignInNotice path={portal.inventory}>O inventário é da área de membros.</SignInNotice>
      ) : list === null ? (
        <RetryNotice onRetry={load}>Não conseguimos abrir o inventário agora.</RetryNotice>
      ) : !data ? (
        <Loading label="A carregar os instrumentos…" />
      ) : (
        <>
          {data.total > 0 && (
            <dl className="inventory-stats">
              <div>
                <dt>Total</dt>
                <dd>{data.total}</dd>
              </div>
              {data.stats.map((s) => (
                <div key={s.condition}>
                  <dt>{s.label}</dt>
                  <dd>{s.count}</dd>
                </div>
              ))}
            </dl>
          )}

          <ToolsBar>
            <SearchField id={ids.q} label="Procurar instrumento" placeholder="Procurar por nome, tipo ou marca" value={text} onChange={setText} />
            <SelectField id={ids.category} label="Tipo" value={category} onChange={setCategory}>
              <option value="">Todos os tipos</option>
              {data.categories.map((c) => (
                <option key={c.value} value={c.value}>
                  {c.label}
                </option>
              ))}
            </SelectField>
            <SelectField id={ids.condition} label="Condição" value={condition} onChange={setCondition}>
              <option value="">Todas as condições</option>
              {data.conditions.map((c) => (
                <option key={c.value} value={c.value}>
                  {c.label}
                </option>
              ))}
            </SelectField>
          </ToolsBar>

          {data.total === 0 ? (
            <EmptyNote>Ainda não há instrumentos registados.</EmptyNote>
          ) : data.instruments.length === 0 ? (
            <EmptyNote>Nenhum instrumento corresponde à pesquisa.</EmptyNote>
          ) : (
            <>
              <ul className="inventory-grid">
                {data.instruments.slice(0, shown).map((i) => (
                  <li key={i.id} className="inventory-card">
                    <button type="button" className="inventory-card__open" onClick={() => setOpen(i.id)}>
                      <Thumb url={i.thumbnailUrl} size={96} />
                      <strong className="inventory-card__name">{i.name}</strong>
                      <span className="member-badges">
                        <MemberBadge kind="position">{i.categoryLabel}</MemberBadge>
                        <span className={conditionClass(i.condition)}>{i.conditionLabel}</span>
                      </span>
                      {i.brand && <small>Marca: {i.brand}</small>}
                      {i.location && <small>Local: {i.location}</small>}
                    </button>
                    {data.canManage && (
                      <span className="inventory-card__tools">
                        <IconAction icon="pencil" label={`Editar ${i.name}`} onClick={() => setForm({ id: i.id })} large />
                        <IconAction icon="trash" label={`Eliminar ${i.name}`} onClick={() => setDeleting(i)} large danger />
                      </span>
                    )}
                  </li>
                ))}
              </ul>
              <LoadMore remaining={data.instruments.length - shown} onMore={() => setShown(shown + PAGE)} />
            </>
          )}

          {open !== undefined && <DetailDialog id={open} onClose={() => setOpen(undefined)} />}
          {form && (
            <FormDialog
              id={form.id}
              categories={data.categories}
              conditions={data.conditions}
              onClose={() => setForm(undefined)}
              onSaved={load}
            />
          )}
          {deleting && <DeleteDialog instrument={deleting} onClose={() => setDeleting(undefined)} onDone={load} />}
        </>
      )}
    </section>
  );
}
