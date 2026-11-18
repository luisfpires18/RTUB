// Card owner and collaborators panel.
import { useId } from 'react';
import { MemberPicker } from './LogisticsFields';
import { Section, type Apply } from './LogisticsPanel';
import { logisticsApi, type CardDetail } from '../data/logisticsApi';
import { DEFAULT_AVATAR } from '../data/membersApi';
import { FieldError } from './ui/FieldError';
import { PersonChip } from './ui/PersonChip';
import type { Draft } from '../data/logisticsCardDraft';

/** Owner (manager picks; saved with the card's fields) and collaborators (managers add / remove, saved at once). */
export function People({
  boardId,
  detail,
  draft,
  set,
  apply,
  errors,
}: {
  boardId: number;
  detail: CardDetail;
  draft: Draft;
  set: (patch: Partial<Draft>) => void;
  apply: Apply;
  errors: Record<string, string>;
}) {
  const id = useId();
  const cardId = detail.card.id;
  const person = (name: string, avatar: string | null, note?: string) => (
    <span className="chip lx-person">
      <img src={avatar ?? DEFAULT_AVATAR} alt="" width="24" height="24" />
      {name}
      {note && <small className="lx-view__lead">{note}</small>}
    </span>
  );

  return (
    <Section id={`${id}-people`} title="Pessoas">
      <div className="form__field">
        <span className="form__label">Responsável</span>
        {detail.canManage ? (
          draft.owner ? (
            <PersonChip name={draft.owner.name} avatar={draft.owner.avatar} onRemove={() => set({ owner: null })} removeLabel="Remover o responsável" />
          ) : (
            <MemberPicker label="Responsável" boardId={boardId} onPick={(m) => set({ owner: { id: m.id, name: m.displayName, avatar: m.avatarUrl } })} />
          )
        ) : detail.assignedTo ? (
          person(detail.assignedTo.displayName, detail.assignedTo.avatarUrl)
        ) : (
          <p className="note">Sem responsável.</p>
        )}
        <FieldError text={errors.assignedToUserId} />
      </div>
      <div className="form__field">
        <span className="form__label">Colaboradores</span>
        {detail.assignments.length > 0 ? (
          <div className="chips">
            {detail.assignments.map((a, i) =>
              detail.canManage && a.userId ? (
                <PersonChip
                  key={a.userId}
                  name={a.displayName}
                  avatar={a.avatarUrl}
                  onRemove={() => apply(logisticsApi.unassign(cardId, a.userId!), 'userId', 'remover o colaborador')}
                  removeLabel={`Remover ${a.displayName}`}
                />
              ) : (
                <span key={i}>{person(a.displayName, a.avatarUrl)}</span>
              ),
            )}
          </div>
        ) : (
          <p className="note">Sem colaboradores.</p>
        )}
        {detail.canManage && (
          <MemberPicker
            label="Adicionar colaborador"
            boardId={boardId}
            exclude={[...detail.assignments.flatMap((a) => (a.userId ? [a.userId] : [])), ...(draft.owner ? [draft.owner.id] : [])]}
            onPick={(m) => apply(logisticsApi.assign(cardId, m.id), 'userId', 'adicionar o colaborador')}
          />
        )}
        <FieldError text={errors.userId} />
      </div>
    </Section>
  );
}
