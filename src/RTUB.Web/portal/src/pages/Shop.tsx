import { useEffect, useId, useState } from 'react';
import { LoadMore } from '../components/ui/LoadMore';
import { SearchField, SelectField, ToolsBar } from '../components/ui/SearchTools';
import { useLatest } from '../components/AppShared';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { MemberBadge } from '../components/MemberBadge';
import { PromotionsSection } from '../components/ShopPromotions';
import { euros } from '../data/format';
import { RetryNotice, SignInNotice } from '../components/Notice';
import { type Product, type Catalogue, api, PAGE } from '../data/shopApi';
import { DetailDialog, ReserveDialog, MineDialog, ReservationsDialog } from '../components/ShopDialogs';
import { Confirm, ProductForm } from '../components/ShopProductForm';
import { Loading } from '../components/ui/Loading';
import { PageHead } from '../components/ui/PageHead';

type Open =
  | { kind: 'detail' | 'reserve' | 'mine' | 'reservations' | 'delete'; product: Product }
  | { kind: 'form'; id?: number };

/**
 * /shop - Loja RTUB. Visitors browse the public catalogue; members retain their existing actions. The products of the fiscal year
 * (this one by default), by type then name, with stock and price; members reserve members-only products in stock and
 * cancel their own reservation. Mod adds products; Admin and Owner edit, delete and see every reservation.
 */
export default function ShopPage() {
  const [shop, setShop] = useState<Catalogue | 'signin' | null>();
  const [fiscalYear, setFiscalYear] = useState<string | null>(null);
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [type, setType] = useState('');
  const [shown, setShown] = useState(PAGE);
  const [open, setOpen] = useState<Open>();
  const ids = { q: useId(), type: useId(), year: useId() };

  useEffect(() => {
    document.title = 'Loja · RTUB';
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);

  const latest = useLatest();
  const load = () => latest(api.shop(fiscalYear, q, type), (o) => setShop(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null));

  useEffect(() => {
    setShown(PAGE);
    load();
  }, [fiscalYear, q, type]);

  const data = shop && shop !== 'signin' ? shop : null;
  const memberShop = data && 'canManage' in data ? data : null;
  const close = () => setOpen(undefined);

  return (
    <section className="page wrap events-page shop-page" aria-labelledby="shop-title">
      <PageHead
        eyebrow={memberShop && 'Área de membros'}
        title="Loja RTUB"
        titleId="shop-title"
        lead="Os produtos da tuna, com o preço e o que ainda há em stock."
      >
        {memberShop?.canCreate && (
          <div className="events-page__actions">
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setOpen({ kind: 'form' })}>
              <Icon name="plus" />
              Adicionar produto
            </button>
          </div>
        )}
      </PageHead>

      {shop === 'signin' ? (
        <SignInNotice path={portal.shop}>A loja é da área de membros.</SignInNotice>
      ) : shop === null ? (
        <RetryNotice onRetry={load}>Não conseguimos abrir a loja agora.</RetryNotice>
      ) : !data ? (
        <Loading label="A carregar os produtos…" />
      ) : (
        <>
          <PromotionsSection />
          <ToolsBar>
            <SearchField id={ids.q} label="Procurar produto" placeholder="Procurar por nome ou tipo" value={text} onChange={setText} />
            <SelectField id={ids.type} label="Tipo" value={type} onChange={setType}>
              <option value="">Todos os tipos</option>
              {data.types.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </SelectField>
            <SelectField id={ids.year} label="Ano letivo" value={data.fiscalYear} onChange={setFiscalYear}>
              <option value="">Todos os anos</option>
              {data.fiscalYear && !data.fiscalYears.some((y) => y.value === data.fiscalYear) && (
                <option value={data.fiscalYear}>{data.fiscalYear} (ATUAL)</option>
              )}
              {data.fiscalYears.map((y) => (
                <option key={y.value} value={y.value}>
                  {y.label}
                </option>
              ))}
            </SelectField>
          </ToolsBar>

          {data.products.length === 0 ? (
            <p className="note">{type ? 'Neste momento não há produtos deste tipo.' : 'Neste momento não há produtos na loja.'}</p>
          ) : (
            <>
              <ul className="shop-grid">
                {data.products.slice(0, shown).map((p) => {
                  const memberProduct = 'myReservation' in p;
                  return (
                  <li key={p.id} className="shop-card">
                    {p.imageUrl ? (
                      <img className="shop-card__image" src={p.imageUrl} alt="" loading="lazy" />
                    ) : (
                      <span className="shop-card__image shop-card__image--empty" aria-hidden="true">
                        <Icon name="images" />
                      </span>
                    )}
                    <div className="shop-card__body">
                      <strong>{p.name}</strong>
                      <span className="member-badges">
                        <MemberBadge kind="position">{p.type}</MemberBadge>
                        {p.stock > 0 ? (
                          <MemberBadge>{p.stock} em stock</MemberBadge>
                        ) : (
                          <MemberBadge kind="retired">Esgotado</MemberBadge>
                        )}
                        {memberProduct && !p.isPublic && <MemberBadge kind="active">Apenas membros</MemberBadge>}
                        {p.availableAllYears && (!memberProduct || memberShop?.canManage) && <MemberBadge>Todos os anos</MemberBadge>}
                      </span>
                      {!memberProduct && p.description && <span className="shop-description">{p.description}</span>}
                      <span className="shop-card__price">{euros(p.price)}</span>
                      {memberProduct && <span className="shop-card__actions">
                        <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'detail', product: p })}>
                          <Icon name="search" />
                          Ver detalhes
                        </button>
                        {p.myReservation ? (
                          <>
                            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'mine', product: p })}>
                              <Icon name="check" />
                              A minha reserva
                            </button>
                          </>
                        ) : (
                          p.canReserve && (
                            <button type="button" className="btn btn--primary btn--sm" onClick={() => setOpen({ kind: 'reserve', product: p })}>
                              <Icon name="calendar" />
                              Reservar
                            </button>
                          )
                        )}
                      </span>}
                      {(memberShop?.canCreate || memberShop?.canManageReservations) && memberProduct && (
                        <span className="shop-card__actions">
                          {memberShop.canManageReservations && !p.isPublic && (
                            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setOpen({ kind: 'reservations', product: p })}>
                              <Icon name="person" />
                              Ver reservas
                            </button>
                          )}
                          {memberShop.canCreate && (
                            <button type="button" className="icon-btn" onClick={() => setOpen({ kind: 'form', id: p.id })}>
                              <Icon name="pencil" />
                              <span className="sr-only">Editar {p.name}</span>
                            </button>
                          )}
                          {memberShop.canManage && (
                            <>
                              <button type="button" className="icon-btn icon-btn--danger" onClick={() => setOpen({ kind: 'delete', product: p })}>
                                <Icon name="trash" />
                                <span className="sr-only">Eliminar {p.name}</span>
                              </button>
                            </>
                          )}
                        </span>
                      )}
                    </div>
                  </li>
                  );
                })}
              </ul>
              <LoadMore remaining={data.products.length - shown} onMore={() => setShown(shown + PAGE)} />
            </>
          )}

          {memberShop && open?.kind === 'detail' && <DetailDialog id={open.product.id} onClose={close} />}
          {memberShop && open?.kind === 'reserve' && <ReserveDialog product={open.product} sizes={memberShop.sizes} onClose={close} onDone={load} />}
          {memberShop && open?.kind === 'mine' && open.product.myReservation && (
            <MineDialog product={open.product} reservation={open.product.myReservation} onClose={close} onDone={load} />
          )}
          {memberShop && open?.kind === 'reservations' && <ReservationsDialog product={open.product} onClose={close} onChanged={load} />}
          {memberShop && open?.kind === 'delete' && (
            <Confirm
              title="Eliminar produto"
              label="Eliminar"
              run={() => api.remove(open.product.id)}
              onClose={close}
              onDone={load}
            >
              <p>
                Eliminar <strong>{open.product.name}</strong> da loja?
              </p>
              {(open.product.reservationCount ?? 0) > 0 && (
                <p className="warning">
                  <Icon name="warning" />
                  {open.product.reservationCount === 1
                    ? 'Este produto tem 1 reserva, que também é apagada.'
                    : `Este produto tem ${open.product.reservationCount} reservas, que também são apagadas.`}
                </p>
              )}
              <p className="warning">
                <Icon name="warning" />
                Apaga também a imagem e as reservas deste produto. Não dá para desfazer.
              </p>
            </Confirm>
          )}
          {memberShop && open?.kind === 'form' && <ProductForm id={open.id} canManage={memberShop.canCreate} onClose={close} onSaved={load} />}
        </>
      )}
    </section>
  );
}
