import { useEffect, useId, useState, type FormEvent } from 'react';
import { ExternalLink, Loading } from './App';
import { Dialog } from './Dialog';
import { call, type Outcome } from './eventsApi';
import { Icon } from './icons';

// Órgãos Sociais for members (React track 016; was the Blazor /member/roles). The RGI for Member, Mod, Admin and
// Owner; fiscal years and position assignments for Mod, Admin and Owner. The server decides and validates everything
// (GovernanceManagementService); hiding a button here is only a convenience.

export type Holder = { assignmentId: number; displayName: string; fullName: string | null; avatarUrl: string | null };
export type ManagePosition = { position: string; title: string; holders: Holder[] };
export type ManageBody = { name: string; positions: ManagePosition[] };
export type Manage = { fiscalYears: string[]; fiscalYear: string | null; availableStartYears: number[]; bodies: ManageBody[] };
type Candidate = { id: string; displayName: string; fullName: string | null; avatarUrl: string | null };

const DEFAULT_AVATAR = '/images/default-avatar.webp';

export const governanceApi = {
  manage: (fiscalYear?: string) => call<Manage>('GET', `/api/governance/manage${fiscalYear ? `?fiscalYear=${encodeURIComponent(fiscalYear)}` : ''}`),
  members: (q: string) => call<Candidate[]>('GET', `/api/governance/members?q=${encodeURIComponent(q)}`),
  createYear: (startYear: number) => call<Manage>('POST', '/api/governance/years', { startYear }),
  assign: (fiscalYear: string, position: string, userId: string) => call<Manage>('POST', '/api/governance/assignments', { fiscalYear, position, userId }),
  remove: (assignmentId: number) => call<Manage>('DELETE', `/api/governance/assignments/${assignmentId}`),
  rgi: () => call<{ url: string | null }>('GET', '/api/governance/rgi'),
};

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Só Mod, Admin ou Owner podem gerir os órgãos sociais.'
      : o.kind === 'notfound'
        ? 'Este cargo já não está atribuído.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

/** Regulamentos Gerais Internos: the PDF in R2 through a short-lived link, as on the old page. */
export function RgiDialog({ onClose }: { onClose: () => void }) {
  const [state, setState] = useState<{ url: string | null } | { error: string }>();

  useEffect(() => {
    governanceApi.rgi().then((o) =>
      setState(o.kind === 'ok' ? o.data : { error: o.kind === 'forbidden' ? 'O RGI é reservado a membros da RTUB.' : problem(o, 'abrir o RGI') }),
    );
  }, []);

  return (
    <Dialog title="Regulamentos Gerais Internos (RGI)" size="lg" onClose={onClose}>
      {!state ? (
        <Loading label="A carregar o RGI…" />
      ) : 'error' in state ? (
        <p className="form__banner" role="alert">
          {state.error}
        </p>
      ) : !state.url ? (
        <p className="form__banner" role="status">
          O documento RGI não está disponível de momento.
        </p>
      ) : (
        <div className="lyrics">
          <iframe className="lyrics__pdf" src={`${state.url}#toolbar=0&navpanes=0`} title="Regulamentos Gerais Internos" />
          <ExternalLink href={state.url} className="more">
            Abrir o PDF
          </ExternalLink>
        </div>
      )}
    </Dialog>
  );
}

export function CreateYearDialog({ years, onDone, onClose }: { years: number[]; onDone: (m: Manage) => void; onClose: () => void }) {
  const id = useId();
  const [startYear, setStartYear] = useState(years[0]);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = await governanceApi.createYear(startYear);
    setBusy(false);
    if (o.kind === 'ok') onDone(o.data);
    else setError(problem(o, 'criar o ano letivo'));
  };

  return (
    <Dialog
      title="Adicionar ano letivo"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          {years.length > 0 && (
            <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy}>
              Criar
            </button>
          )}
        </>
      }
    >
      {years.length === 0 ? (
        <p className="note">Não falta criar nenhum ano letivo: estão todos registados, de 1991 até hoje.</p>
      ) : (
        <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
          <div className={error ? 'form__field form__field--error' : 'form__field'}>
            <label htmlFor={id}>Ano de início</label>
            <span className="control control--select">
              <select id={id} value={startYear} onChange={(e) => setStartYear(Number(e.target.value))}>
                {years.map((y) => (
                  <option key={y} value={y}>
                    {y}-{y + 1}
                  </option>
                ))}
              </select>
            </span>
            {error && <p className="form__error">{error}</p>}
          </div>
        </form>
      )}
    </Dialog>
  );
}

export function AssignDialog({
  fiscalYear,
  seat,
  onDone,
  onClose,
}: {
  fiscalYear: string;
  seat: ManagePosition & { body: string };
  onDone: (m: Manage) => void;
  onClose: () => void;
}) {
  const id = useId();
  const [query, setQuery] = useState('');
  const [found, setFound] = useState<Candidate[]>();
  const [picked, setPicked] = useState<Candidate>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!query.trim()) return setFound(undefined);
    const timer = setTimeout(() => {
      governanceApi.members(query.trim()).then((o) => setFound(o.kind === 'ok' ? o.data : []));
    }, 250);
    return () => clearTimeout(timer);
  }, [query]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!picked) return;
    setBusy(true);
    const o = await governanceApi.assign(fiscalYear, seat.position, picked.id);
    setBusy(false);
    if (o.kind === 'ok') onDone(o.data);
    else setError(problem(o, 'atribuir o cargo'));
  };

  return (
    <Dialog
      title={`Atribuir cargo · ${seat.title}`}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy || !picked}>
            Atribuir
          </button>
        </>
      }
    >
      <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
        <p className="note">
          {seat.body} · Mandato {fiscalYear}
        </p>
        <div className="form__field">
          <label htmlFor={id}>Membro</label>
          <span className="control control--sm">
            <Icon name="search" />
            <input id={id} type="search" placeholder="Procurar por alcunha, nome ou email" value={query} onChange={(e) => setQuery(e.target.value)} />
          </span>
          {found && found.length === 0 && <p className="note">Ninguém encontrado.</p>}
          {found && found.length > 0 && (
            <ul className="talk__picks">
              {found.map((m) => (
                <li key={m.id}>
                  <button type="button" aria-pressed={picked?.id === m.id} onClick={() => setPicked(m)}>
                    <img src={m.avatarUrl ?? DEFAULT_AVATAR} alt="" width="28" height="28" loading="lazy" />
                    <span>
                      {m.displayName}
                      {m.fullName && <small>{m.fullName}</small>}
                    </span>
                    {picked?.id === m.id && <Icon name="check" />}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
        {picked && <p className="note">Escolhido: {picked.displayName}</p>}
        {error && (
          <p className="form__error" role="alert">
            {error}
          </p>
        )}
      </form>
    </Dialog>
  );
}

export function RemoveDialog({
  holder,
  title,
  onDone,
  onClose,
}: {
  holder: Holder;
  title: string;
  onDone: (m: Manage) => void;
  onClose: () => void;
}) {
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  const remove = async () => {
    setBusy(true);
    const o = await governanceApi.remove(holder.assignmentId);
    setBusy(false);
    if (o.kind === 'ok') onDone(o.data);
    else setError(problem(o, 'remover o cargo'));
  };

  return (
    <Dialog
      title="Remover cargo"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={remove} disabled={busy}>
            Remover
          </button>
        </>
      }
    >
      <p>
        Remover {holder.displayName} do cargo de {title}?
      </p>
      <p className="note">Se for do ano letivo atual, o perfil do membro também é atualizado.</p>
      {error && (
        <p className="form__error" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
