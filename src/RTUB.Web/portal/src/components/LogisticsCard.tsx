// The card workspace dialog: loads one card, edits its fields and lays out the panels.
import { useEffect, useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { Section } from './LogisticsPanel';
import type { Outcome } from '../data/api';
import { logisticsApi, PRIORITY, problem, STATUS, type Board, type Card, type CardDetail } from '../data/logisticsApi';
import { day } from '../data/format';
import { deadline } from '../data/logisticsDates';
import { CardFacts, CardStatus } from './LogisticsCardFacts';
import { Checklist } from './LogisticsChecklist';
import { Attachments } from './LogisticsAttachments';
import { Discussion } from './LogisticsDiscussion';
import { People } from './LogisticsPeople';
import { Labels } from './LogisticsLabels';
import { LinkedCards } from './LogisticsLinkedCards';
import { Reminders } from './LogisticsReminders';
import { History } from './LogisticsHistory';
import { draftOf, same, type Draft } from '../data/logisticsCardDraft';
import { Loading } from './ui/Loading';
import { FieldError } from './ui/FieldError';
import { FormField } from './ui/FormField';
import { Spinner } from './ui/Spinner';

/**
 * The card workspace (Logística 2/5). One layout for everyone; the controls follow the server's flags:
 * `canWork` (owner, collaborators, managers) edits the card's fields, checklist, status, comments and attachments;
 * `canManage` also changes the owner, collaborators, labels, linked cards and deletes. The card's own fields save
 * together ("Guardar alterações" in the footer, version-checked: a 409 shows a conflict and keeps the draft); the
 * rest saves as you go and shows only what the server answered. Full screen on phones.
 */
export function CardWorkspace({
  board,
  cardId,
  onClose,
  onChanged,
  onDelete,
}: {
  board: Board;
  cardId: number;
  onClose: () => void;
  onChanged: () => void;
  onDelete: (card: Card) => void;
}) {
  const [detail, setDetail] = useState<CardDetail | 'missing' | null>();
  const [draft, setDraft] = useState<Draft>();
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [conflict, setConflict] = useState(false);
  const [busy, setBusy] = useState<string>();
  const fid = useId();

  const load = (keepDraft = false) =>
    logisticsApi.card(cardId).then((o) => {
      if (o.kind !== 'ok') return setDetail(o.kind === 'notfound' ? 'missing' : null);
      setDetail(o.data);
      if (!keepDraft) setDraft(draftOf(o.data));
    });

  useEffect(() => {
    load();
  }, [cardId]);

  if (!detail || detail === 'missing' || !draft) {
    return (
      <Dialog title="Cartão" onClose={onClose} className="lx-card-modal">
        {detail === undefined ? (
          <Loading label="A carregar o cartão…" />
        ) : (
          <div className="notice" role="status">
            <p>{detail === 'missing' ? 'Este cartão já não existe ou deixou de ter acesso a ele.' : 'Não foi possível abrir o cartão.'}</p>
            {detail === null && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => (setDetail(undefined), load())}>
                Tentar novamente
              </button>
            )}
          </div>
        )}
      </Dialog>
    );
  }

  const card = detail.card;
  const dirty = detail.canWork && !same(draft, draftOf(detail));
  const due = deadline(detail.card.dueDate, card.status);

  // Every immediate change answers with the fresh card; the draft of the card's own fields is kept.
  const apply = async (o: Promise<Outcome<CardDetail>>, key: string, what: string) => {
    setErrors({});
    setBusy(key);
    const r = await o;
    setBusy(undefined);
    if (r.kind === 'ok') {
      setDetail(r.data);
      onChanged();
      return true;
    }
    // 404: the card is gone or no longer visible (access removed) - drop what is shown, do not keep stale data.
    if (r.kind === 'notfound') {
      setDetail('missing');
      onChanged();
      return false;
    }
    setErrors({ [key]: problem(r, what) });
    return false;
  };

  const save = async (e?: FormEvent, version = detail.version) => {
    e?.preventDefault();
    setSaving(true);
    setSaved(false);
    setErrors({});
    const o = await logisticsApi.updateCard(card.id, {
      title: draft.title,
      description: draft.description,
      priority: draft.priority,
      startDate: draft.startDate || null,
      dueDate: draft.dueDate || null,
      // Non-managers have no member ids: they send none and the server keeps the owner.
      assignedToUserId: detail.canManage ? draft.owner?.id || null : null,
      version,
    });
    setSaving(false);
    if (o.kind === 'conflict') return setConflict(true);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'guardar o cartão') });
    setConflict(false);
    setDetail(o.data);
    setDraft(draftOf(o.data));
    setSaved(true);
    onChanged();
  };

  // Their version wins: reload, the draft is dropped. Mine wins: save the draft over the newest version.
  const reloadTheirs = () => (setConflict(false), load());
  const keepMine = async () => {
    const o = await logisticsApi.card(card.id);
    if (o.kind === 'ok') save(undefined, o.data.version);
    else setErrors({ form: problem(o, 'guardar o cartão') });
  };

  const close = () => {
    if (dirty && !window.confirm('Tem alterações por guardar neste cartão. Fechar sem guardar?')) return;
    onClose();
  };

  const set = (patch: Partial<Draft>) => (setSaved(false), setDraft({ ...draft, ...patch }));
  const listName = board.lists.find((l) => l.id === detail.listId)?.name;
  const archive = async (archived: boolean) => {
    if (await apply(logisticsApi.archiveCard(card.id, archived), 'form', archived ? 'arquivar o cartão' : 'restaurar o cartão')) setDraft(draftOf(detail));
  };
  const duplicate = async () => {
    setErrors({});
    const o = await logisticsApi.duplicateCard(card.id);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'duplicar o cartão') });
    onChanged();
    setDetail(o.data);
    setDraft(draftOf(o.data));
    setSaved(false);
  };

  return (
    <Dialog
      title={card.title}
      size="xl"
      className="lx-card-modal"
      onClose={close}
      footer={
        <>
          {detail.canManage && !board.archivedAt && (
            <span className="lx-ws__manage">
              <button type="button" className="btn btn--danger lx-ws__delete" onClick={() => onDelete(card)}>
                <Icon name="trash" />
                Eliminar
              </button>
              <button type="button" className="btn btn--ghost" onClick={() => archive(!detail.archivedAt)}>
                <Icon name={detail.archivedAt ? 'restore' : 'folder'} />
                {detail.archivedAt ? 'Restaurar' : 'Arquivar'}
              </button>
              {!detail.archivedAt && (
                <button type="button" className="btn btn--ghost" onClick={duplicate} title="Título, detalhes, etiquetas, prioridade, datas e checklist por fazer. Sem pessoas, comentários, histórico nem anexos.">
                  <Icon name="kanban" />
                  Duplicar
                </button>
              )}
            </span>
          )}
          <span className="lx-ws__state" role="status">
            {saving ? 'A guardar…' : dirty ? 'Alterações por guardar' : saved ? 'Guardado.' : ''}
          </span>
          <button type="button" className="btn btn--ghost" onClick={close}>
            Fechar
          </button>
          {detail.canWork && (
            <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={!dirty || saving || !draft.title.trim()}>
              {saving && <Spinner small />}
              Guardar alterações
            </button>
          )}
        </>
      }
    >
      <div className="lx-ws">
        <div className="lx-ws__strip">
          <span className={`lx-status lx-view__status--${card.status.toLowerCase()}`}>{STATUS[card.status].label}</span>
          {card.priority !== 'Normal' && <span className={`lx-prio lx-prio--${card.priority.toLowerCase()}`}>{PRIORITY[card.priority]}</span>}
          {due && <span className={`lx-due lx-due--${due.tone}`}>{due.text}</span>}
          {listName && <span className="lx-chip">{listName}</span>}
          {card.mine && <span className="lx-chip lx-chip--mine">Atribuído a si</span>}
          {!detail.assignedTo && <span className="lx-chip lx-chip--warn">Sem responsável</span>}
        </div>
        {detail.readOnly && (
          <p className="lx-archived-banner" role="status">
            <Icon name="folder" />
            {detail.archivedAt ? `Cartão arquivado a ${day(detail.archivedAt)}` : 'O quadro está arquivado'}: só leitura.
          </p>
        )}
        {!detail.canWork && !detail.readOnly && (
          <p className="lx-view__role">
            <Icon name="eye" />
            Só leitura: este cartão é trabalhado pelo responsável e pelos colaboradores.
          </p>
        )}
        {conflict && (
          <div className="form__banner lx-ws__conflict" role="alert">
            <p>Outra pessoa guardou este cartão enquanto o editava. As suas alterações ainda não foram guardadas.</p>
            <div className="lx-inline">
              <button type="button" className="btn btn--ghost btn--sm" onClick={reloadTheirs}>
                Ver a versão atual (descartar as minhas)
              </button>
              <button type="button" className="btn btn--primary btn--sm" onClick={keepMine}>
                Guardar as minhas por cima
              </button>
            </div>
          </div>
        )}
        <FieldError text={errors.form} />

        <form id={`${fid}-form`} className="lx-ws__grid" onSubmit={save} noValidate>
          <div className="lx-ws__main">
            {detail.canWork ? (
              <>
                <FormField id={`${fid}-title`} label="Título" error={errors.title} alert>
                  <input id={`${fid}-title`} type="text" maxLength={200} value={draft.title} onChange={(e) => set({ title: e.target.value })} />
                </FormField>
                <FormField id={`${fid}-desc`} label="Detalhes" error={errors.description} alert>
                  <textarea id={`${fid}-desc`} rows={5} maxLength={2000} value={draft.description} onChange={(e) => set({ description: e.target.value })} />
                  <small className="note">{draft.description.length}/2000</small>
                </FormField>
              </>
            ) : (
              card.description && (
                <Section id={`${fid}-desc`} title="Detalhes">
                  <p className="lx-view__text">{card.description}</p>
                </Section>
              )
            )}

            <Checklist detail={detail} busy={busy} errors={errors} apply={apply} />
            <Attachments detail={detail} board={board} apply={apply} errors={errors} onUploaded={(d) => (setDetail(d), onChanged())} />
            <Discussion boardId={board.id} detail={detail} apply={apply} busy={busy} errors={errors} />
          </div>

          <aside className="lx-ws__side" aria-label="Estado e pessoas">
            <CardStatus fid={fid} detail={detail} board={board} busy={busy} errors={errors} apply={apply} />


            <CardFacts fid={fid} detail={detail} draft={draft} errors={errors} set={set} />

            <People boardId={board.id} detail={detail} draft={draft} set={set} apply={apply} errors={errors} />
            <Reminders boardId={board.id} detail={detail} apply={apply} busy={busy} errors={errors} />
            <Labels detail={detail} apply={apply} errors={errors} />
            <LinkedCards detail={detail} board={board} apply={apply} errors={errors} />
            {card.eventName && <p className="note">Atuação: {card.eventName}</p>}
            <History activity={detail.activity} />
          </aside>
        </form>
      </div>
    </Dialog>
  );
}
