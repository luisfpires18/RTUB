import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { day, euros, isoDay, matches, problem, today, treasuryApi, type Person, type Transfer, type Transfers } from './treasuryApi';
import { Confirm, Field, MemberPicker, PersonChip, Refusal, SaveFooter, TreasuryTabs } from './TreasuryUi';

const PAGE = 12;

/**
 * /treasury/mbway - MBWAY (React track 024; was the Blazor /mbway, which visitors could open). Treasury members: every
 * transfer, newest first, totals per recipient, search. Mod, Admin and Owner add; the Owner edits and deletes.
 * MBWay transfers never count in the report totals.
 */
export default function TreasuryMbway() {
  const [data, setData] = useState<Transfers | 'signin' | 'forbidden' | 'failed'>();
  const [search, setSearch] = useState('');
  const [limit, setLimit] = useState(PAGE);
  const [form, setForm] = useState<{ transfer?: Transfer }>();
  const [deleting, setDeleting] = useState<Transfer>();
  const id = useId();

  useEffect(() => {
    document.title = 'MBWAY · Tesouraria · RTUB';
  }, []);

  const load = () =>
    treasuryApi.transfers().then((o) => setData(o.kind === 'ok' ? o.data : o.kind === 'signin' || o.kind === 'forbidden' ? o.kind : 'failed'));

  useEffect(() => {
    load();
  }, []);

  const t = data && typeof data === 'object' ? data : null;
  const shown = t?.transfers.filter((x) => matches(search, x.transferTo, x.transferFrom, x.phone, x.description)) ?? [];

  return (
    <section className="page wrap events-page tr-page" aria-labelledby="tr-title">
      <a className="back-link" href={portal.treasury}>
        <Icon name="arrow" />
        Tesouraria
      </a>
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="tr-title" className="page__title">
            MBWAY
          </h1>
          <p className="page__lead">Registo das transferências MBWAY. Não entram nos totais dos relatórios.</p>
        </div>
        {t?.canAdd && (
          <div className="events-page__actions">
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setForm({})}>
              <Icon name="plus" />
              Adicionar transferência
            </button>
          </div>
        )}
      </header>

      {typeof data === 'string' ? (
        <Refusal state={data} path={portal.treasuryMbway} onRetry={load} />
      ) : !t ? (
        <Loading label="A carregar as transferências…" />
      ) : (
        <>
          <TreasuryTabs current="mbway" />
          {t.transfers.length === 0 ? (
            <p className="note">Não há transferências MBWAY registadas.</p>
          ) : (
            <>
              <dl className="tr-tiles">
                {t.totals.map((x) => (
                  <div key={x.name} className="tr-tile">
                    <dt>{x.name}</dt>
                    <dd className="tr-in">{euros(x.amount)}</dd>
                  </div>
                ))}
              </dl>
              <div className="events-tools" role="search">
                <label className="control" htmlFor={id}>
                  <span className="sr-only">Procurar transferência</span>
                  <Icon name="search" />
                  <input
                    id={id}
                    type="search"
                    placeholder="Procurar por nome, telefone ou descrição"
                    value={search}
                    onChange={(e) => {
                      setSearch(e.target.value);
                      setLimit(PAGE);
                    }}
                  />
                </label>
              </div>
              {shown.length === 0 ? (
                <p className="note">Nenhuma transferência corresponde à pesquisa.</p>
              ) : (
                <ul className="tr-cards">
                  {shown.slice(0, limit).map((x) => (
                    <li key={x.id} className="tr-card">
                      <div className="tr-card__head">
                        <strong className="tr-in">+{euros(x.amount)}</strong>
                        <span className="tr-muted">{day(x.date)}</span>
                        {t.canEdit && (
                          <span className="tr-card__tools">
                            <button type="button" className="icon-btn icon-btn--sm" onClick={() => setForm({ transfer: x })}>
                              <Icon name="pencil" />
                              <span className="sr-only">Editar transferência</span>
                            </button>
                            <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => setDeleting(x)}>
                              <Icon name="trash" />
                              <span className="sr-only">Eliminar transferência</span>
                            </button>
                          </span>
                        )}
                      </div>
                      <dl className="tr-facts">
                        <dt>Para</dt>
                        <dd>{x.transferTo || '—'}</dd>
                        {x.transferFrom && (
                          <>
                            <dt>De</dt>
                            <dd>{x.transferFrom}</dd>
                          </>
                        )}
                        {x.phone && (
                          <>
                            <dt>Telefone</dt>
                            <dd>{x.phone}</dd>
                          </>
                        )}
                        {x.description && (
                          <>
                            <dt>Descrição</dt>
                            <dd className="tr-pre">{x.description}</dd>
                          </>
                        )}
                        <dt>Criado por</dt>
                        <dd>{x.createdBy || '—'}</dd>
                        {x.updatedBy && (
                          <>
                            <dt>Editado por</dt>
                            <dd>
                              {x.updatedBy}
                              {x.updatedAt && ` · ${new Date(x.updatedAt).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' })}`}
                            </dd>
                          </>
                        )}
                      </dl>
                    </li>
                  ))}
                </ul>
              )}
              {shown.length > limit && (
                <button type="button" className="btn btn--ghost btn--sm tr-more" onClick={() => setLimit(limit + PAGE)}>
                  Mostrar mais ({shown.length - limit})
                </button>
              )}
            </>
          )}

          {form && <TransferDialog transfer={form.transfer} onClose={() => setForm(undefined)} onSaved={setData} />}
          {deleting && (
            <Confirm
              title="Eliminar transferência"
              action="Eliminar"
              onClose={() => setDeleting(undefined)}
              onConfirm={async () => {
                const o = await treasuryApi.removeTransfer(deleting.id);
                if (o.kind !== 'ok') return problem(o, 'eliminar a transferência');
                setData(o.data);
              }}
            >
              <p>
                Eliminar a transferência de {euros(deleting.amount)} para <strong>{deleting.transferTo}</strong> ({day(deleting.date)})?
              </p>
              <p className="warning">
                <Icon name="warning" />
                Não dá para desfazer.
              </p>
            </Confirm>
          )}
        </>
      )}
    </section>
  );
}

/** "Adicionar transferência" / "Editar": date, amount, the member who received it (required, as the old service), sender, phone, notes. */
function TransferDialog({ transfer, onClose, onSaved }: { transfer?: Transfer; onClose: () => void; onSaved: (t: Transfers) => void }) {
  const [date, setDate] = useState(isoDay(transfer?.date ?? null) || today());
  const [amount, setAmount] = useState(transfer ? String(transfer.amount) : '');
  const [member, setMember] = useState<Person | null>(transfer?.member ?? null);
  const [from, setFrom] = useState(transfer?.transferFrom ?? '');
  const [phone, setPhone] = useState(transfer?.phone ?? '');
  const [description, setDescription] = useState(transfer?.description ?? '');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const input = {
      date,
      amount: amount.trim() === '' ? null : Number(amount.replace(',', '.')),
      memberUserId: member?.id ?? null,
      transferFrom: from,
      phone,
      description,
    };
    const o = transfer ? await treasuryApi.updateTransfer(transfer.id, input) : await treasuryApi.addTransfer(input);
    setBusy(false);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'guardar a transferência') });
    onSaved(o.data);
    onClose();
  };

  return (
    <Dialog
      title={transfer ? 'Editar transferência MBWAY' : 'Adicionar transferência MBWAY'}
      onClose={onClose}
      footer={<SaveFooter formId={`${fid}-form`} busy={busy} disabled={!member || !amount || !date} onClose={onClose} />}
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        {errors.form && (
          <p className="form__banner" role="alert">
            {errors.form}
          </p>
        )}
        <div className="form__row">
          <Field id={`${fid}-date`} label="Data" error={errors.date}>
            <input id={`${fid}-date`} type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          </Field>
          <Field id={`${fid}-amount`} label="Montante (€)" error={errors.amount}>
            <input id={`${fid}-amount`} type="number" inputMode="decimal" min="0.01" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
          </Field>
        </div>
        <div className={errors.memberUserId ? 'form__field form__field--error' : 'form__field'}>
          <span className="form__label">Transferido para (membro da tuna)</span>
          {member ? (
            <PersonChip person={member} onRemove={() => setMember(null)} />
          ) : (
            <>
              {transfer && <p className="note">Atual: {transfer.transferTo}</p>}
              <MemberPicker forDebts={false} label="Procurar membro" onPick={setMember} />
            </>
          )}
          {errors.memberUserId && (
            <p className="form__error" role="alert">
              {errors.memberUserId}
            </p>
          )}
        </div>
        <div className="form__row">
          <Field id={`${fid}-from`} label="Transferido por (opcional)" error={errors.transferFrom}>
            <input id={`${fid}-from`} type="text" maxLength={200} value={from} onChange={(e) => setFrom(e.target.value)} />
          </Field>
          <Field id={`${fid}-phone`} label="Telefone (opcional)" error={errors.phone}>
            <input id={`${fid}-phone`} type="tel" maxLength={20} value={phone} onChange={(e) => setPhone(e.target.value)} />
          </Field>
        </div>
        <Field id={`${fid}-desc`} label="Descrição (opcional)" error={errors.description}>
          <textarea id={`${fid}-desc`} rows={3} maxLength={500} value={description} onChange={(e) => setDescription(e.target.value)} />
        </Field>
      </form>
    </Dialog>
  );
}
