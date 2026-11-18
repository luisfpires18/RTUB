// Responsible member, visibility and access list fields of a board.
import { useId } from 'react';
import type { Assignee, Visibility } from '../data/logisticsApi';
import { MemberPicker } from './LogisticsFields';
import { PersonChip } from './ui/PersonChip';

export type Person = { id: string; name: string; avatar: string | null };
export const person = (a: Assignee): Person => ({ id: a.userId ?? '', name: a.displayName, avatar: a.avatarUrl });

/** The access fields shared by the board form and the template dialog: responsible member, visibility and access list. */
export function AccessFields({
  owner,
  setOwner,
  visibility,
  setVisibility,
  members,
  setMembers,
  errors,
}: {
  owner: Person | null;
  setOwner: (p: Person | null) => void;
  visibility: Visibility;
  setVisibility: (v: Visibility) => void;
  members: Person[];
  setMembers: (m: Person[]) => void;
  errors: Record<string, string>;
}) {
  const fid = useId();
  const err = (key: string) =>
    errors[key] && (
      <p className="form__error" role="alert">
        {errors[key]}
      </p>
    );
  return (
    <>
      <fieldset className="form__field lx-visibility">
        <legend>Quem vê o quadro</legend>
        <label className="lx-choice">
          <input type="radio" name={`${fid}-vis`} checked={visibility === 'Members'} onChange={() => setVisibility('Members')} />
          <span>
            <strong>Todos os membros</strong>
            <small>Incluindo Leitões.</small>
          </span>
        </label>
        <label className="lx-choice">
          <input type="radio" name={`${fid}-vis`} checked={visibility === 'Private'} onChange={() => setVisibility('Private')} />
          <span>
            <strong>Privado</strong>
            <small>Só a lista de acesso abaixo. Atribuir um cartão não dá acesso.</small>
          </span>
        </label>
        {err('visibility')}
      </fieldset>
      {visibility === 'Private' && (
        <div className={errors.memberIds ? 'form__field form__field--error' : 'form__field'}>
          <span className="form__label">Lista de acesso</span>
          {members.length > 0 ? (
            <div className="chips">
              {members.map((m) => (
                <PersonChip
                  key={m.id}
                  name={m.name}
                  avatar={m.avatar}
                  onRemove={() => {
                    setMembers(members.filter((x) => x.id !== m.id));
                    if (owner?.id === m.id) setOwner(null);
                  }}
                  removeLabel={`Retirar o acesso a ${m.name}`}
                />
              ))}
            </div>
          ) : (
            <p className="note">Ainda ninguém na lista de acesso.</p>
          )}
          <MemberPicker label="Dar acesso a" exclude={members.map((m) => m.id)} onPick={(m) => setMembers([...members, { id: m.id, name: m.displayName, avatar: m.avatarUrl }])} />
          {err('memberIds')}
        </div>
      )}
      <div className={errors.ownerUserId ? 'form__field form__field--error' : 'form__field'}>
        <span className="form__label">Responsável pelo quadro (opcional)</span>
        {owner ? (
          <PersonChip name={owner.name} avatar={owner.avatar} onRemove={() => setOwner(null)} removeLabel="Remover o responsável" />
        ) : (
          <MemberPicker
            label="Responsável pelo quadro"
            onPick={(m) => {
              const p = { id: m.id, name: m.displayName, avatar: m.avatarUrl };
              setOwner(p);
              if (visibility === 'Private' && !members.some((x) => x.id === m.id)) setMembers([...members, p]);
            }}
          />
        )}
        <small className="note">Responde pelo quadro; não ganha permissões de gestão.</small>
        {err('ownerUserId')}
      </div>
    </>
  );
}
