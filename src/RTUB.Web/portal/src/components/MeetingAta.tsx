import { useId } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { fieldClass } from './MeetingUi';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingCard } from '../data/meetingsTypes';
import { numericDateTime } from '../data/meetingsDates';
import { useAtaEditor, MAX_POINTS } from '../data/useAtaEditor';
import { Block, AtaAttendanceBlock, AtaClosingBlock, AtaPointCard, AtaStatusBlock } from './MeetingAtaParts';
import { AtaBoard } from './MeetingAtaBoard';
import { Loading } from './ui/Loading';
import { Banner } from './ui/Banner';
import { FieldError } from './ui/FieldError';
import { FormField } from './ui/FormField';
import { AsyncConfirmDialog } from './ui/ConfirmDialog';
import { Spinner } from './ui/Spinner';
import { SelectField } from './ui/SearchTools';

// The ata of a meeting: the editor for whoever may write it (a draft stays private to its writers) and
// "Ver Ata" for readers of a published one. The server decides who writes, reads, publishes and confirms.

const MAX_ATA_NUMBER = 50;
const MAX_LOCATION = 200;
const MAX_POINT_TITLE = 500;

/** "Criar Ata" / "Editar Ata": save, generate the PDF, publish (the PDF then goes to Documentação). */
export function AtaEditorDialog({ card, onClose, onChanged }: { card: MeetingCard; onClose: () => void; onChanged: () => void }) {
  const {
    editor, values, newPoint, setNewPoint, errors, banner, notice, busy, confirmPublish, setConfirmPublish,
    set, setPoint, addPoint, onSave, onPdf, publish,
  } = useAtaEditor(card.id, onChanged);
  const id = useId();

  const published = editor?.status === 'published';
  const readOnly = published || busy !== undefined;
  const kind = editor?.kind;

  return (
    <>
      <Dialog
        title={editor?.id ? 'Editar Ata' : 'Criar Ata'}
        size="lg"
        onClose={onClose}
        footer={
          <div className="mtg-ata-foot">
            <span className="mtg-ata-foot__main">
              {editor && !published && (
                <>
                  <button type="button" className="btn btn--primary btn--sm" onClick={onSave} disabled={busy !== undefined}>
                    {busy === 'save' ? <Spinner small /> : <Icon name="check" />}
                    Guardar Ata
                  </button>
                  <button type="button" className="btn btn--ghost btn--sm" onClick={onPdf} disabled={busy !== undefined}>
                    {busy === 'pdf' ? <Spinner small /> : <Icon name="filePdf" />}
                    Gerar PDF
                  </button>
                  {editor.id !== null && (
                    <button type="button" className="btn btn--gold btn--sm" onClick={() => setConfirmPublish(true)} disabled={busy !== undefined} title="Publicar Ata">
                      <Icon name="send" />
                      Publicar
                    </button>
                  )}
                </>
              )}
              {editor && published && (
                <>
                  <a className="btn btn--primary btn--sm" href={meetingsApi.ataPdfUrl(card.id)} download>
                    <Icon name="filePdf" />
                    Download PDF
                  </a>
                  <span className="pill pill--yes">
                    <Icon name="checkCircle" />
                    Ata Publicada
                  </span>
                </>
              )}
            </span>
            <button type="button" className="btn btn--ghost btn--sm" onClick={onClose} disabled={busy !== undefined}>
              Fechar
            </button>
          </div>
        }
      >
        {editor === undefined ? (
          <Loading label="A carregar dados da ata..." />
        ) : editor === null || !values ? (
          <Banner text={banner} />
        ) : (
          <div className="mtg-ata">
            <Banner text={banner} />
            {notice && (
              <p className="mtg-feedback mtg-feedback--ok" role="status">
                {notice}
              </p>
            )}
            <Block icon="journal" title="Informações da Reunião">
              <dl className="mtg-kv">
                <div>
                  <dt>Tipo:</dt>
                  <dd>{editor.meetingTypeLabel}</dd>
                </div>
                <div>
                  <dt>Título:</dt>
                  <dd>{editor.meetingTitle}</dd>
                </div>
                <div>
                  <dt>Data Agendada:</dt>
                  <dd>{numericDateTime(editor.meetingDate)}</dd>
                </div>
              </dl>
            </Block>

            <fieldset className="mtg-fieldset" disabled={readOnly}>
              <legend className="sr-only">Ata</legend>
              <Block icon="file" title="Dados da Ata">
                <div className="form mtg-form">
                  <FormField id={`${id}-number`} label="Número da Ata" error={errors.ataNumber} alert>
                    <input
                      id={`${id}-number`}
                      type="text"
                      maxLength={MAX_ATA_NUMBER}
                      placeholder="Ex: ATA CV 01/2025"
                      value={values.ataNumber}
                      onChange={(e) => set({ ataNumber: e.target.value })}
                    />
                  </FormField>
                  <div className="form__row">
                    <div className={fieldClass(errors, 'actualStartTime')}>
                      <label htmlFor={`${id}-start`}>
                        Hora de Início Efetiva <span className="mtg-required">*</span>
                      </label>
                      <input id={`${id}-start`} type="datetime-local" value={values.actualStartTime} onChange={(e) => set({ actualStartTime: e.target.value })} />
                      <FieldError text={errors.actualStartTime} />
                    </div>
                    <FormField id={`${id}-end`} label="Hora de Término" error={errors.actualEndTime} alert>
                      <input id={`${id}-end`} type="datetime-local" value={values.actualEndTime} onChange={(e) => set({ actualEndTime: e.target.value })} />
                    </FormField>
                  </div>
                  <div className={fieldClass(errors, 'location')}>
                    <label htmlFor={`${id}-location`}>
                      Local <span className="mtg-required">*</span>
                    </label>
                    <input id={`${id}-location`} type="text" maxLength={MAX_LOCATION} value={values.location} onChange={(e) => set({ location: e.target.value })} />
                    <FieldError text={errors.location} />
                  </div>
                  {kind === 'ag' && (
                    <FormField id={`${id}-quorum`} label="Base do Quórum" error={errors.quorumBasis} alert>
                      <SelectField id={`${id}-quorum`} value={values.quorumBasis} onChange={(v) => set({ quorumBasis: v })}>
                        <option value="HoraAgendada">Iniciada à hora agendada</option>
                        <option value="HoraAgendadaMais30Minutos">Iniciada 30 minutos após hora agendada</option>
                      </SelectField>
                    </FormField>
                  )}
                </div>
              </Block>

              <AtaBoard id={id} editor={editor} values={values} errors={errors} set={set} />

              <AtaAttendanceBlock present={editor.present} />

              <Block icon="listOl" title="Ordem de Trabalhos">
                {values.points.length === 0 ? (
                  <p className="note">Nenhum ponto adicionado ainda.</p>
                ) : (
                  <ol className="mtg-points">
                    {values.points.map((p, i) => (
                      <AtaPointCard
                        key={p.key}
                        point={p}
                        index={i}
                        id={id}
                        error={errors[`agendaPoints[${i}]`]}
                        removable={!published}
                        onRemove={() => set({ points: values.points.filter((x) => x.key !== p.key) })}
                        onChange={(patch) => setPoint(p.key, patch)}
                      />
                    ))}
                  </ol>
                )}
                {!published && (
                  <div className="mtg-point-add">
                    <label className="control" htmlFor={`${id}-new-point`}>
                      <span className="sr-only">Título do novo ponto</span>
                      <input
                        id={`${id}-new-point`}
                        type="text"
                        maxLength={MAX_POINT_TITLE}
                        placeholder="Título do novo ponto de ordem de trabalhos..."
                        value={newPoint}
                        onChange={(e) => setNewPoint(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter') {
                            e.preventDefault();
                            addPoint();
                          }
                        }}
                      />
                    </label>
                    <button type="button" className="btn btn--ghost btn--sm" onClick={addPoint} disabled={!newPoint.trim() || values.points.length >= MAX_POINTS}>
                      <Icon name="plus" />
                      Adicionar
                    </button>
                  </div>
                )}
                <FieldError text={errors.agendaPoints} />
              </Block>

              <AtaClosingBlock id={id} value={values.closingText} error={errors.closingText} onChange={(closingText) => set({ closingText })} />
            </fieldset>

            <AtaStatusBlock editor={editor} published={published} />
          </div>
        )}
      </Dialog>
      {confirmPublish && (
        <AsyncConfirmDialog title="Publicar Ata" confirmLabel="Sim, Publicar" confirmIcon="send" onClose={() => setConfirmPublish(false)} onConfirm={publish}>
          <div className="mtg-confirm">
            <p>
              Tem a certeza que deseja publicar esta ata? Após publicação, a ata ficará visível para os utilizadores autorizados e será criada uma pasta na
              Documentação.
            </p>
          </div>
        </AsyncConfirmDialog>
      )}
    </>
  );
}
