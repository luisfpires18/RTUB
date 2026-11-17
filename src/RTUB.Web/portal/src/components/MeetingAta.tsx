import { useEffect, useId, useState } from 'react';
import { Loading } from './AppShared';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { Banner, ConfirmDialog, FieldError, fieldClass, problem, type Errors } from './MeetingUi';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingAtaEditor, MeetingCard } from '../data/meetingsTypes';
import { numericDateTime } from '../data/meetingsDates';
import { type Point, type Values, toPoint, fromEditor, toInput, download, editorProblem } from '../data/meetingAtaForm';
import { Block, SecretarySelect } from './MeetingAtaParts';

// The ata of a meeting (task 034): the editor for whoever may write it (a draft is only theirs, decision A1) and
// "Ver Ata" for readers of a published one. The server decides who writes, reads, publishes and confirms.

const MAX_ATA_NUMBER = 50;
const MAX_LOCATION = 200;
const MAX_CLOSING = 5000;
const MAX_POINT_TITLE = 500;
const MAX_POINT_TEXT = 5000;
const MAX_POINTS = 100;
const MAX_VOTES = 100000;

/** "Criar Ata" / "Editar Ata": save, generate the PDF, publish (the PDF then goes to Documentação). */
export function AtaEditorDialog({ card, onClose, onChanged }: { card: MeetingCard; onClose: () => void; onChanged: () => void }) {
  const [editor, setEditor] = useState<MeetingAtaEditor | null>();
  const [values, setValues] = useState<Values>();
  const [newPoint, setNewPoint] = useState('');
  const [errors, setErrors] = useState<Errors>({});
  const [banner, setBanner] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [busy, setBusy] = useState<'save' | 'pdf' | 'publish'>();
  const [confirmPublish, setConfirmPublish] = useState(false);
  const id = useId();

  useEffect(() => {
    meetingsApi.ataEditor(card.id).then((o) => {
      if (o.kind === 'ok') {
        setEditor(o.data);
        setValues(fromEditor(o.data));
      } else {
        setEditor(null);
        setBanner(editorProblem(o.kind, problem(o, 'abrir a ata')));
      }
    });
  }, [card.id]);

  const set = (patch: Partial<Values>) => setValues((v) => v && { ...v, ...patch });
  const setPoint = (key: number, patch: Partial<Point>) =>
    setValues((v) => v && { ...v, points: v.points.map((p) => (p.key === key ? { ...p, ...patch } : p)) });

  const addPoint = () => {
    const title = newPoint.trim();
    if (!title || !values || values.points.length >= MAX_POINTS) return;
    set({ points: [...values.points, toPoint({ title })] });
    setNewPoint('');
  };

  /** Saves the form; the saved editor, or null (errors shown). */
  const save = async (): Promise<MeetingAtaEditor | null> => {
    if (!editor || !values) return null;
    setBanner(undefined);
    setNotice(undefined);
    const o = await meetingsApi.saveAta(card.id, toInput(values, editor.kind));
    if (o.kind === 'ok') {
      setErrors({});
      setEditor(o.data);
      setValues(fromEditor(o.data));
      onChanged();
      return o.data;
    }
    if (o.kind === 'invalid') {
      setErrors(o.errors);
      setBanner(o.errors.agendaPoints ?? 'Há campos por corrigir.');
    } else setBanner(editorProblem(o.kind, problem(o, 'guardar a ata')));
    return null;
  };

  const onSave = async () => {
    setBusy('save');
    const saved = await save();
    setBusy(undefined);
    if (saved) setNotice('Ata guardada.');
  };

  const onPdf = async () => {
    setBusy('pdf');
    const saved = await save();
    setBusy(undefined);
    if (saved) download(meetingsApi.ataPdfUrl(card.id));
  };

  const publish = async (): Promise<string | void> => {
    setBusy('publish');
    const saved = await save();
    if (!saved) {
      setBusy(undefined);
      return 'A ata não foi guardada: veja os campos por corrigir.';
    }
    const o = await meetingsApi.publishAta(card.id);
    setBusy(undefined);
    if (o.kind !== 'ok') return o.kind === 'closed' ? 'Só uma ata guardada e ainda por publicar pode ser publicada.' : problem(o, 'publicar a ata');
    setEditor(o.data);
    setValues(fromEditor(o.data));
    setNotice('Ata publicada.');
    onChanged();
  };

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
                    {busy === 'save' ? <span className="spinner spinner--small" aria-hidden="true" /> : <Icon name="check" />}
                    Guardar Ata
                  </button>
                  <button type="button" className="btn btn--ghost btn--sm" onClick={onPdf} disabled={busy !== undefined}>
                    {busy === 'pdf' ? <span className="spinner spinner--small" aria-hidden="true" /> : <Icon name="filePdf" />}
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
                  <div className={fieldClass(errors, 'ataNumber')}>
                    <label htmlFor={`${id}-number`}>Número da Ata</label>
                    <input
                      id={`${id}-number`}
                      type="text"
                      maxLength={MAX_ATA_NUMBER}
                      placeholder="Ex: ATA CV 01/2025"
                      value={values.ataNumber}
                      onChange={(e) => set({ ataNumber: e.target.value })}
                    />
                    <FieldError errors={errors} name="ataNumber" />
                  </div>
                  <div className="form__row">
                    <div className={fieldClass(errors, 'actualStartTime')}>
                      <label htmlFor={`${id}-start`}>
                        Hora de Início Efetiva <span className="mtg-required">*</span>
                      </label>
                      <input id={`${id}-start`} type="datetime-local" value={values.actualStartTime} onChange={(e) => set({ actualStartTime: e.target.value })} />
                      <FieldError errors={errors} name="actualStartTime" />
                    </div>
                    <div className={fieldClass(errors, 'actualEndTime')}>
                      <label htmlFor={`${id}-end`}>Hora de Término</label>
                      <input id={`${id}-end`} type="datetime-local" value={values.actualEndTime} onChange={(e) => set({ actualEndTime: e.target.value })} />
                      <FieldError errors={errors} name="actualEndTime" />
                    </div>
                  </div>
                  <div className={fieldClass(errors, 'location')}>
                    <label htmlFor={`${id}-location`}>
                      Local <span className="mtg-required">*</span>
                    </label>
                    <input id={`${id}-location`} type="text" maxLength={MAX_LOCATION} value={values.location} onChange={(e) => set({ location: e.target.value })} />
                    <FieldError errors={errors} name="location" />
                  </div>
                  {kind === 'ag' && (
                    <div className={fieldClass(errors, 'quorumBasis')}>
                      <label htmlFor={`${id}-quorum`}>Base do Quórum</label>
                      <span className="control control--select">
                        <select id={`${id}-quorum`} value={values.quorumBasis} onChange={(e) => set({ quorumBasis: e.target.value })}>
                          <option value="HoraAgendada">Iniciada à hora agendada</option>
                          <option value="HoraAgendadaMais30Minutos">Iniciada 30 minutos após hora agendada</option>
                        </select>
                      </span>
                      <FieldError errors={errors} name="quorumBasis" />
                    </div>
                  )}
                </div>
              </Block>

              {kind === 'cv' ? (
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
              ) : (
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
              )}

              <Block icon="calendarCheck" title="Presenças">
                {editor.present.length > 0 ? (
                  <>
                    <p>
                      <strong>Membros presentes ({editor.present.length}):</strong>
                    </p>
                    <ul className="mtg-chips">
                      {editor.present.map((name, i) => (
                        <li key={`${name}-${i}`} className="mtg-chip mtg-chip--yes">
                          {name}
                        </li>
                      ))}
                    </ul>
                  </>
                ) : (
                  <p className="note">Nenhum membro inscrito para esta reunião.</p>
                )}
              </Block>

              <Block icon="listOl" title="Ordem de Trabalhos">
                {values.points.length === 0 ? (
                  <p className="note">Nenhum ponto adicionado ainda.</p>
                ) : (
                  <ol className="mtg-points">
                    {values.points.map((p, i) => (
                      <li key={p.key} className={errors[`agendaPoints[${i}]`] ? 'mtg-point is-invalid' : 'mtg-point'}>
                        <div className="mtg-point__head">
                          <strong className="mtg-point__title">
                            {i + 1}. {p.title}
                          </strong>
                          {!published && (
                            <button
                              type="button"
                              className="icon-btn icon-btn--sm icon-btn--danger"
                              onClick={() => set({ points: values.points.filter((x) => x.key !== p.key) })}
                              title="Remover ponto"
                            >
                              <Icon name="trash" />
                              <span className="sr-only">Remover o ponto {i + 1}</span>
                            </button>
                          )}
                        </div>
                        <div className="form__field">
                          <label htmlFor={`${id}-p${p.key}-discussion`}>Discussão/Desenvolvimento:</label>
                          <textarea
                            id={`${id}-p${p.key}-discussion`}
                            rows={2}
                            maxLength={MAX_POINT_TEXT}
                            placeholder="Resumo da discussão sobre este ponto..."
                            value={p.discussion}
                            onChange={(e) => setPoint(p.key, { discussion: e.target.value })}
                          />
                        </div>
                        <div className="form__field">
                          <label htmlFor={`${id}-p${p.key}-decision`}>Deliberação/Decisão:</label>
                          <textarea
                            id={`${id}-p${p.key}-decision`}
                            rows={2}
                            maxLength={MAX_POINT_TEXT}
                            placeholder="Decisão tomada ou proposta submetida..."
                            value={p.decision}
                            onChange={(e) => setPoint(p.key, { decision: e.target.value })}
                          />
                        </div>
                        <div className="mtg-votes">
                          {(
                            [
                              ['votesFor', 'A favor:'],
                              ['votesAgainst', 'Contra:'],
                              ['votesAbstain', 'Abstenções:'],
                            ] as const
                          ).map(([field, label]) => (
                            <div key={field} className="form__field">
                              <label htmlFor={`${id}-p${p.key}-${field}`}>{label}</label>
                              <input
                                id={`${id}-p${p.key}-${field}`}
                                type="number"
                                inputMode="numeric"
                                min={0}
                                max={MAX_VOTES}
                                step={1}
                                value={p[field]}
                                onChange={(e) => setPoint(p.key, { [field]: e.target.value })}
                              />
                            </div>
                          ))}
                          <div className="form__field">
                            <label htmlFor={`${id}-p${p.key}-result`}>Resultado:</label>
                            <span className="control control--select">
                              <select id={`${id}-p${p.key}-result`} value={p.result} onChange={(e) => setPoint(p.key, { result: e.target.value })}>
                                <option value="">--</option>
                                <option value="Aprovado">Aprovado</option>
                                <option value="Rejeitado">Rejeitado</option>
                              </select>
                            </span>
                          </div>
                        </div>
                        <FieldError errors={errors} name={`agendaPoints[${i}]`} />
                      </li>
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
                <FieldError errors={errors} name="agendaPoints" />
              </Block>

              <Block icon="journal" title="Encerramento">
                <div className={fieldClass(errors, 'closingText')}>
                  <label htmlFor={`${id}-closing`}>Texto de Encerramento</label>
                  <textarea
                    id={`${id}-closing`}
                    rows={4}
                    maxLength={MAX_CLOSING}
                    placeholder="Ex: A reunião foi encerrada às XX horas, tendo sido lavrada a presente ata..."
                    value={values.closingText}
                    onChange={(e) => set({ closingText: e.target.value })}
                  />
                  <FieldError errors={errors} name="closingText" />
                </div>
              </Block>
            </fieldset>

            <Block icon="file" title="Estado">
              <span className={published ? 'pill pill--yes' : 'pill pill--wait'}>{published ? 'Publicada' : 'Rascunho'}</span>
              {editor.generatedAt && (
                <p className="note mtg-ata__generated">
                  <Icon name="clock" /> Último PDF gerado: {numericDateTime(editor.generatedAt)}
                </p>
              )}
            </Block>
          </div>
        )}
      </Dialog>
      {confirmPublish && (
        <ConfirmDialog title="Publicar Ata" confirmLabel="Sim, Publicar" confirmIcon="send" onClose={() => setConfirmPublish(false)} onConfirm={publish}>
          <p>
            Tem a certeza que deseja publicar esta ata? Após publicação, a ata ficará visível para os utilizadores autorizados e será criada uma pasta na
            Documentação.
          </p>
        </ConfirmDialog>
      )}
    </>
  );
}
