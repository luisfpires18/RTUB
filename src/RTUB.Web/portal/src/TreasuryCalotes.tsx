import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { day, euros, isoDay, matches, problem, treasuryApi, type Calotes, type DebtGroup, type Person } from './treasuryApi';
import { Confirm, DEFAULT_AVATAR, Field, MemberPicker, PersonChip, Refusal, SaveFooter, TreasuryTabs } from './TreasuryUi';

type Debt = DebtGroup['debts'][number];

/** Managers get member ids; everyone else sees one group (their own) or reads only, so the position is enough. */
const key = (g: DebtGroup, i: number) => g.member.id ?? `#${i}`;

/**
 * /treasury/calotes - Calotes (React track 024; was the Blazor /calotes, which visitors could open). Treasury members see
 * every member's debts of a fiscal year; Caloiros and Leitões only their own. Mod, Admin and Owner add, edit, remove and
 * set the commitment date ("Compromisso de pagamento") that pauses the daily reminder.
 */
export default function TreasuryCalotes() {
  const [fy, setFy] = useState(() => new URLSearchParams(location.search).get('fy') ?? '');
  const [data, setData] = useState<Calotes | 'signin' | 'failed'>();
  const [search, setSearch] = useState('');
  const [viewing, setViewing] = useState<string>(); // a group's key
  const [form, setForm] = useState<{ member?: Person; debt?: Debt }>();
  const [deleting, setDeleting] = useState<{ debt: Debt; who: string }>();
  const ids = { search: useId(), year: useId() };

  const load = (year = fy) =>
    treasuryApi.calotes(year).then((o) => setData(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : 'failed'));

  useEffect(() => {
    load();
  }, []);

  const c = data && typeof data === 'object' ? data : null;
  const own = c?.scope === 'own';

  useEffect(() => {
    document.title = `${own ? 'Os meus calotes' : 'Calotes'} · Tesouraria · RTUB`;
  }, [own]);

  const changeYear = (year: string) => {
    setFy(year);
    setSearch('');
    history.replaceState(null, '', `${portal.treasuryCalotes}?fy=${encodeURIComponent(year)}`);
    load(year);
  };

  const saved = (next: Calotes) => setData(next);
  const groups = c?.groups.filter((g) => matches(search, g.member.displayName, g.member.fullName)) ?? [];
  const group = c?.groups.find((g, i) => key(g, i) === viewing);

  return (
    <section className="page wrap events-page tr-page" aria-labelledby="tr-title">
      {!own && (
        <a className="back-link" href={portal.treasury}>
          <Icon name="arrow" />
          Tesouraria
        </a>
      )}
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="tr-title" className="page__title">
            {own ? 'Os meus calotes' : 'Calotes'}
          </h1>
          <p className="page__lead">{own ? 'O que deves à tuna, por ano letivo.' : 'Registo das dívidas dos membros à tuna.'}</p>
        </div>
        {c?.canManage && c.fiscalYear && (
          <div className="events-page__actions">
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setForm({})}>
              <Icon name="plus" />
              Adicionar calote
            </button>
          </div>
        )}
      </header>

      {typeof data === 'string' ? (
        <Refusal state={data} path={portal.treasuryCalotes} onRetry={() => load()} />
      ) : !c ? (
        <Loading label="A carregar os calotes…" />
      ) : (
        <>
          {!own && <TreasuryTabs current="calotes" fiscalYear={c.fiscalYear ?? undefined} />}
          {c.fiscalYears.length === 0 ? (
            <p className="note">Ainda não há anos letivos.</p>
          ) : (
            <>
              <div className="events-tools" role="search">
                {!own && (
                  <label className="control" htmlFor={ids.search}>
                    <span className="sr-only">Procurar membro</span>
                    <Icon name="search" />
                    <input id={ids.search} type="search" placeholder="Procurar membro" value={search} onChange={(e) => setSearch(e.target.value)} />
                  </label>
                )}
                <label className="control control--select" htmlFor={ids.year}>
                  <span className="sr-only">Ano letivo</span>
                  <Icon name="calendar" />
                  <select id={ids.year} value={c.fiscalYear ?? ''} onChange={(e) => changeYear(e.target.value)}>
                    {c.fiscalYears.map((y) => (
                      <option key={y} value={y}>
                        {y}
                      </option>
                    ))}
                  </select>
                </label>
              </div>

              <dl className="tr-tiles tr-tiles--one">
                <div className="tr-tile">
                  <dt>{own ? 'Total em dívida' : 'Total de calotes'}</dt>
                  <dd className="tr-out">{euros(c.total)}</dd>
                </div>
              </dl>

              {c.groups.length === 0 ? (
                <p className="note">{own ? 'Não tens calotes neste ano letivo.' : 'Não há dívidas registadas neste ano letivo.'}</p>
              ) : groups.length === 0 ? (
                <p className="note">Nenhum membro corresponde à pesquisa.</p>
              ) : (
                <ul className="tr-debtors">
                  {groups.map((g) => (
                    <li key={key(g, c.groups.indexOf(g))} className="tr-debtor">
                      <img src={g.member.avatarUrl ?? DEFAULT_AVATAR} alt="" width="64" height="64" loading="lazy" />
                      <strong>{g.member.displayName}</strong>
                      {g.member.fullName && g.member.fullName !== g.member.displayName && <span className="tr-muted">{g.member.fullName}</span>}
                      <span className="pill pill--no">{euros(g.total)}</span>
                      {g.compromisedUntil && (
                        <span className="badge">
                          <Icon name="calendar" />
                          Compromisso até {day(g.compromisedUntil)}
                        </span>
                      )}
                      <button type="button" className="btn btn--ghost btn--sm" onClick={() => setViewing(key(g, c.groups.indexOf(g)))}>
                        Ver detalhes
                        <span className="sr-only"> de {g.member.displayName}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}

          {group && (
            <DebtsDialog
              group={group}
              calotes={c}
              onClose={() => setViewing(undefined)}
              onSaved={saved}
              onEdit={(debt) => setForm({ member: group.member, debt })}
              onDelete={(debt) => setDeleting({ debt, who: group.member.displayName })}
              onAdd={() => setForm({ member: group.member })}
            />
          )}
          {form && c.fiscalYear && (
            <DebtDialog
              fiscalYear={c.fiscalYear}
              member={form.member}
              debt={form.debt}
              onClose={() => setForm(undefined)}
              onSaved={saved}
            />
          )}
          {deleting && (
            <Confirm
              title="Remover calote"
              action="Remover"
              onClose={() => setDeleting(undefined)}
              onConfirm={async () => {
                const o = await treasuryApi.removeDebt(deleting.debt.id);
                if (o.kind !== 'ok') return problem(o, 'remover o calote');
                saved(o.data);
              }}
            >
              <p>
                Remover o calote de <strong>{deleting.who}</strong> ({euros(deleting.debt.amount)})?
              </p>
            </Confirm>
          )}
        </>
      )}
    </section>
  );
}

/** One member's debts of the year, with the commitment date (managers change it) and, for managers, edit / remove. */
function DebtsDialog({
  group,
  calotes,
  onClose,
  onSaved,
  onEdit,
  onDelete,
  onAdd,
}: {
  group: DebtGroup;
  calotes: Calotes;
  onClose: () => void;
  onSaved: (c: Calotes) => void;
  onEdit: (d: Debt) => void;
  onDelete: (d: Debt) => void;
  onAdd: () => void;
}) {
  const [until, setUntil] = useState(isoDay(group.compromisedUntil));
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string>();
  const fid = useId();

  const saveCommitment = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = await treasuryApi.setCommitment(calotes.fiscalYear!, group.member.id!, until || null);
    setBusy(false);
    if (o.kind !== 'ok') return setMessage(problem(o, 'guardar o compromisso'));
    setMessage('Compromisso guardado.');
    onSaved(o.data);
  };

  return (
    <Dialog title={`Calotes · ${group.member.displayName}`} size="lg" onClose={onClose}>
      <div className="tr-debts">
        <p className="tr-debts__total">
          Total em dívida <span className="pill pill--no">{euros(group.total)}</span>
        </p>

        <section aria-labelledby={`${fid}-commit`}>
          <h3 id={`${fid}-commit`} className="tr-subtitle">
            Compromisso de pagamento
          </h3>
          {calotes.canManage ? (
            <form className="tr-inline-form" onSubmit={saveCommitment} noValidate>
              <Field id={`${fid}-until`} label="Até (opcional)">
                <input id={`${fid}-until`} type="date" value={until} onChange={(e) => setUntil(e.target.value)} />
              </Field>
              <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
                {busy && <span className="spinner spinner--small" aria-hidden="true" />}
                Guardar
              </button>
              <p className="form__hint">Enquanto esta data estiver no futuro, não são enviados lembretes de calote a este membro neste ano letivo.</p>
            </form>
          ) : (
            <p className="tr-muted">{group.compromisedUntil ? `Até ${day(group.compromisedUntil)}.` : 'Nenhum compromisso de pagamento definido.'}</p>
          )}
          {message && <p className="note" role="status">{message}</p>}
        </section>

        <section aria-labelledby={`${fid}-list`}>
          <div className="tr-section-head">
            <h3 id={`${fid}-list`} className="tr-subtitle">
              Dívidas ({group.debts.length})
            </h3>
            {calotes.canManage && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={onAdd}>
                <Icon name="plus" />
                Adicionar
              </button>
            )}
          </div>
          <ul className="tr-transactions">
            {group.debts.map((d) => (
              <li key={d.id} className="tr-transaction">
                <span className="tr-transaction__main">{d.description ? <strong>{d.description}</strong> : <span className="note">Sem descrição</span>}</span>
                <span className="tr-out tr-transaction__amount">{euros(d.amount)}</span>
                {calotes.canManage && (
                  <span className="tr-transaction__tools">
                    <button type="button" className="icon-btn icon-btn--sm" onClick={() => onEdit(d)}>
                      <Icon name="pencil" />
                      <span className="sr-only">Editar calote</span>
                    </button>
                    <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={() => onDelete(d)}>
                      <Icon name="trash" />
                      <span className="sr-only">Remover calote</span>
                    </button>
                  </span>
                )}
              </li>
            ))}
          </ul>
        </section>
      </div>
    </Dialog>
  );
}

/** "Adicionar calote" / "Editar calote": member (not a Leitão), amount, description. */
function DebtDialog({
  fiscalYear,
  member: initial,
  debt,
  onClose,
  onSaved,
}: {
  fiscalYear: string;
  member?: Person;
  debt?: Debt;
  onClose: () => void;
  onSaved: (c: Calotes) => void;
}) {
  const [member, setMember] = useState<Person | undefined>(initial);
  const [amount, setAmount] = useState(debt ? String(debt.amount) : '');
  const [description, setDescription] = useState(debt?.description ?? '');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const value = amount.trim() === '' ? null : Number(amount.replace(',', '.'));
    setBusy(true);
    const o = debt
      ? await treasuryApi.updateDebt(debt.id, { amount: value, description })
      : await treasuryApi.addDebt({ fiscalYear, userId: member?.id ?? '', amount: value, description });
    setBusy(false);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'guardar o calote') });
    onSaved(o.data);
    onClose();
  };

  return (
    <Dialog
      title={debt ? 'Editar calote' : `Adicionar calote · ${fiscalYear}`}
      onClose={onClose}
      footer={<SaveFooter formId={`${fid}-form`} busy={busy} disabled={!member || !amount} onClose={onClose} label={debt ? 'Guardar' : 'Adicionar'} />}
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        {errors.form && (
          <p className="form__banner" role="alert">
            {errors.form}
          </p>
        )}
        <div className={errors.userId ? 'form__field form__field--error' : 'form__field'}>
          <span className="form__label">Membro</span>
          {member ? (
            <PersonChip person={member} onRemove={debt || initial ? undefined : () => setMember(undefined)} />
          ) : (
            <MemberPicker forDebts label="Procurar membro" onPick={setMember} />
          )}
          {errors.userId && (
            <p className="form__error" role="alert">
              {errors.userId}
            </p>
          )}
        </div>
        <Field id={`${fid}-amount`} label="Montante (€)" error={errors.amount}>
          <input id={`${fid}-amount`} type="number" inputMode="decimal" min="0.01" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} />
        </Field>
        <Field id={`${fid}-desc`} label="Descrição (opcional)" error={errors.description}>
          <input id={`${fid}-desc`} type="text" maxLength={500} value={description} onChange={(e) => setDescription(e.target.value)} />
        </Field>
      </form>
    </Dialog>
  );
}
