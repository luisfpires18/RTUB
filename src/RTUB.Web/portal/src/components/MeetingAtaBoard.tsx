import { Block, SecretarySelect } from './MeetingAtaParts';
import type { Errors } from './MeetingUi';
import type { MeetingAtaEditor } from '../data/meetingsTypes';
import type { Values } from '../data/meetingAtaForm';

/** Who chairs and secretaries the meeting: the CV has one optional secretary, every other meeting two. */
export function AtaBoard({
  id,
  editor,
  values,
  errors,
  set,
}: {
  id: string;
  editor: MeetingAtaEditor;
  values: Values;
  errors: Errors;
  set: (patch: Partial<Values>) => void;
}) {
  const kind = editor.kind;
  if (kind === 'cv') {
    return (
      <Block icon="people" title="Constituição e Secretariado (CV)">
        <div className="form mtg-form">
          <div className="form__field">
            <label htmlFor={`${id}-president`}>
              Presidente do CV <span className="mtg-required">*</span>
            </label>
            <input id={`${id}-president`} type="text" value={editor.presidentName ?? 'Não encontrado'} readOnly disabled />
            <p className="form__hint">Membro com o cargo de Presidente do Conselho de Veteranos</p>
          </div>
          {editor.firstSecretaryOptions.length > 0 ? (
            <SecretarySelect
              id={`${id}-first`}
              label="Secretário"
              hint="Membro inscrito escolhido pelo Presidente do CV (opcional)"
              value={values.firstSecretaryId}
              options={editor.firstSecretaryOptions}
              errors={errors}
              name="firstSecretaryId"
              onChange={(v) => set({ firstSecretaryId: v })}
            />
          ) : (
            <div className="form__field">
              <label htmlFor={`${id}-first`}>
                Secretário <span className="note">(Opcional)</span>
              </label>
              <input id={`${id}-first`} type="text" placeholder="Nenhum membro inscrito na reunião" readOnly disabled />
              <p className="form__hint">Não existem membros inscritos para selecionar</p>
            </div>
          )}
        </div>
      </Block>
    );
  }

  return (
    <Block icon="people" title={kind === 'ag' ? 'Mesa da Assembleia Geral' : 'Constituição e Secretariado'}>
      <div className="form mtg-form">
        <div className="form__field">
          <label htmlFor={`${id}-president`}>
            {kind === 'ag' ? 'Presidente da Mesa' : 'Presidente'} <span className="mtg-required">*</span>
          </label>
          <input id={`${id}-president`} type="text" value={editor.presidentName ?? 'Não encontrado'} readOnly disabled />
          {kind === 'ag' && <p className="form__hint">Membro com o cargo de Presidente da Mesa da Assembleia Geral</p>}
        </div>
        <SecretarySelect
          id={`${id}-first`}
          label="1.º Secretário"
          hint={kind === 'ag' ? 'Usa o membro com cargo de 1.º Secretário da Mesa (opcional)' : 'Opcional'}
          value={values.firstSecretaryId}
          options={editor.firstSecretaryOptions}
          errors={errors}
          name="firstSecretaryId"
          onChange={(v) => set({ firstSecretaryId: v })}
        />
        <SecretarySelect
          id={`${id}-second`}
          label="2.º Secretário"
          hint={kind === 'ag' ? 'Usa o membro com cargo de 2.º Secretário da Mesa (opcional)' : 'Opcional'}
          value={values.secondSecretaryId}
          options={editor.secondSecretaryOptions}
          errors={errors}
          name="secondSecretaryId"
          onChange={(v) => set({ secondSecretaryId: v })}
        />
      </div>
    </Block>
  );
}
