// Member admin tool menu and its dialog switch for the /members page.
import { Icon } from './icons';
import { ConfirmAction, NicknameDialog } from './MemberActionDialogs';
import { MemberFormDialog } from './MemberFormDialog';
import { memberAdminApi, type ActiveMember, type Directory, type MemberDetail } from '../data/membersApi';

/** An Admin/Owner tool open over the page (React track 018). */
export type Tool =
  | { kind: 'create' }
  | { kind: 'edit'; id: string }
  | { kind: 'nickname' | 'expel' | 'reactivate' | 'delete'; member: MemberDetail }
  | { kind: 'reminder'; member: ActiveMember; reload: () => void };

export function MemberTools({ m, canDelete, onPick }: { m: MemberDetail; canDelete: boolean; onPick: (t: Tool) => void }) {
  return (
    <>
      <button type="button" className="btn btn--ghost btn--sm" onClick={() => onPick({ kind: 'edit', id: m.id })}>
        <Icon name="pencil" />
        Editar
      </button>
      {m.leitao && (
        <>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => onPick({ kind: 'nickname', member: m })}>
            <Icon name="star" />
            Definir alcunha
          </button>
          {m.expelled ? (
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => onPick({ kind: 'reactivate', member: m })}>
              <Icon name="restore" />
              Reativar
            </button>
          ) : (
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => onPick({ kind: 'expel', member: m })}>
              <Icon name="ban" />
              Expulsar
            </button>
          )}
        </>
      )}
      {canDelete && (
        <button type="button" className="btn btn--danger btn--sm" onClick={() => onPick({ kind: 'delete', member: m })}>
          <Icon name="trash" />
          Eliminar
        </button>
      )}
    </>
  );
}

export function ToolDialog({
  tool,
  instruments,
  onClose,
  onChanged,
}: {
  tool: Tool;
  instruments: Directory['instruments'];
  onClose: () => void;
  onChanged: (deleted: boolean) => void;
}) {
  const changed = () => onChanged(false);
  switch (tool.kind) {
    case 'create':
      return <MemberFormDialog instrumentOptions={instruments} onClose={onClose} onSaved={changed} />;
    case 'edit':
      return <MemberFormDialog memberId={tool.id} instrumentOptions={instruments} onClose={onClose} onSaved={changed} />;
    case 'nickname':
      return <NicknameDialog member={tool.member} onClose={onClose} onDone={changed} />;
    case 'expel':
      return (
        <ConfirmAction title="Expulsar membro" confirmLabel="Expulsar" danger run={() => memberAdminApi.expel(tool.member.id)} onClose={onClose} onDone={changed}>
          <p>
            Expulsar <strong>{tool.member.displayName}</strong>? Deixa de conseguir entrar na área de membros. Pode ser reativado mais tarde.
          </p>
        </ConfirmAction>
      );
    case 'reactivate':
      return (
        <ConfirmAction title="Reativar membro" confirmLabel="Reativar" run={() => memberAdminApi.reactivate(tool.member.id)} onClose={onClose} onDone={changed}>
          <p>
            Reativar <strong>{tool.member.displayName}</strong>? Volta a conseguir entrar na área de membros.
          </p>
        </ConfirmAction>
      );
    case 'delete':
      return (
        <ConfirmAction title="Eliminar membro" confirmLabel="Eliminar" danger run={() => memberAdminApi.remove(tool.member.id)} onClose={onClose} onDone={() => onChanged(true)}>
          <p>
            Eliminar <strong>{tool.member.displayName}</strong>?
          </p>
          <p className="warning">
            <Icon name="warning" />
            Apaga a conta e tudo o que lhe está ligado. Não dá para desfazer.
          </p>
        </ConfirmAction>
      );
    case 'reminder':
      return (
        <ConfirmAction title="Enviar lembrete push" confirmLabel="Enviar" run={() => memberAdminApi.reminder(tool.member.id)} onClose={onClose} onDone={tool.reload}>
          <p>
            Enviar um lembrete push a <strong>{tool.member.displayName}</strong> para participar numa atividade este mês?
          </p>
        </ConfirmAction>
      );
  }
}
