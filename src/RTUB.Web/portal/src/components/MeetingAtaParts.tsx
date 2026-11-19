// Layout pieces of the ata editor (and, for Block and SecretarySelect, the ata view).
import { useId, type ReactNode } from 'react';
import { Icon, type IconName } from './icons';
import { fieldClass, type Errors } from './MeetingUi';
import { FieldError } from './ui/FieldError';
import { FormField } from './ui/FormField';
import { IconAction } from './ui/IconAction';
import type { MeetingAtaEditor, MeetingPersonOption } from '../data/meetingsTypes';
import { numericDateTime } from '../data/meetingsDates';
import type { Point } from '../data/meetingAtaForm';

const MAX_CLOSING = 5000;
const MAX_POINT_TEXT = 5000;
const MAX_VOTES = 100000;

export function Block({ icon, title, children }: { icon: IconName; title: string; children: ReactNode }) {
  const id = useId();
  return (
    <section className="mtg-block" aria-labelledby={id}>
      <h3 id={id} className="mtg-block__title">
        <Icon name={icon} />
        {title}
      </h3>
      <div className="mtg-block__body">{children}</div>
    </section>
  );
}

export function SecretarySelect({
  id,
  label,
  hint,
  value,
  options,
  errors,
  name,
  onChange,
}: {
  id: string;
  label: string;
  hint: string;
  value: string;
  options: MeetingPersonOption[];
  errors: Errors;
  name: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className={fieldClass(errors, name)}>
      <label htmlFor={id}>
        {label} <span className="note">(Opcional)</span>
      </label>
      <span className="control control--select">
        <select id={id} value={value} onChange={(e) => onChange(e.target.value)}>
          <option value="">-- Nenhum secretário selecionado --</option>
          {options.map((o) => (
            <option key={o.id} value={o.id}>
              {o.label}
            </option>
          ))}
        </select>
      </span>
      <p className="form__hint">{hint}</p>
      <FieldError text={errors[name]} />
    </div>
  );
}

export function AtaAttendanceBlock({ present }: { present: string[] }) {
  return (
    <Block icon="calendarCheck" title="Presenças">
      {present.length > 0 ? (
        <>
          <p>
            <strong>Membros presentes ({present.length}):</strong>
          </p>
          <ul className="mtg-chips">
            {present.map((name, i) => (
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
  );
}

/** One agenda point: its discussion, decision, votes and result. */
export function AtaPointCard({
  point,
  index,
  id,
  error,
  removable,
  onRemove,
  onChange,
}: {
  point: Point;
  index: number;
  id: string;
  error: string | undefined;
  removable: boolean;
  onRemove: () => void;
  onChange: (patch: Partial<Point>) => void;
}) {
  const fieldId = (name: string) => `${id}-p${point.key}-${name}`;
  return (
    <li className={error ? 'mtg-point is-invalid' : 'mtg-point'}>
      <div className="mtg-point__head">
        <strong className="mtg-point__title">
          {index + 1}. {point.title}
        </strong>
        {removable && <IconAction icon="trash" label={`Remover o ponto ${index + 1}`} title="Remover ponto" onClick={onRemove} danger />}
      </div>
      <div className="form__field">
        <label htmlFor={fieldId('discussion')}>Discussão/Desenvolvimento:</label>
        <textarea
          id={fieldId('discussion')}
          rows={2}
          maxLength={MAX_POINT_TEXT}
          placeholder="Resumo da discussão sobre este ponto..."
          value={point.discussion}
          onChange={(e) => onChange({ discussion: e.target.value })}
        />
      </div>
      <div className="form__field">
        <label htmlFor={fieldId('decision')}>Deliberação/Decisão:</label>
        <textarea
          id={fieldId('decision')}
          rows={2}
          maxLength={MAX_POINT_TEXT}
          placeholder="Decisão tomada ou proposta submetida..."
          value={point.decision}
          onChange={(e) => onChange({ decision: e.target.value })}
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
            <label htmlFor={fieldId(field)}>{label}</label>
            <input
              id={fieldId(field)}
              type="number"
              inputMode="numeric"
              min={0}
              max={MAX_VOTES}
              step={1}
              value={point[field]}
              onChange={(e) => onChange({ [field]: e.target.value })}
            />
          </div>
        ))}
        <div className="form__field">
          <label htmlFor={fieldId('result')}>Resultado:</label>
          <span className="control control--select">
            <select id={fieldId('result')} value={point.result} onChange={(e) => onChange({ result: e.target.value })}>
              <option value="">--</option>
              <option value="Aprovado">Aprovado</option>
              <option value="Rejeitado">Rejeitado</option>
            </select>
          </span>
        </div>
      </div>
      <FieldError text={error} />
    </li>
  );
}

export function AtaClosingBlock({ id, value, error, onChange }: { id: string; value: string; error: string | undefined; onChange: (text: string) => void }) {
  return (
    <Block icon="journal" title="Encerramento">
      <FormField id={`${id}-closing`} label="Texto de Encerramento" error={error} alert>
        <textarea
          id={`${id}-closing`}
          rows={4}
          maxLength={MAX_CLOSING}
          placeholder="Ex: A reunião foi encerrada às XX horas, tendo sido lavrada a presente ata..."
          value={value}
          onChange={(e) => onChange(e.target.value)}
        />
      </FormField>
    </Block>
  );
}

export function AtaStatusBlock({ editor, published }: { editor: MeetingAtaEditor; published: boolean }) {
  return (
    <Block icon="file" title="Estado">
      <span className={published ? 'pill pill--yes' : 'pill pill--wait'}>{published ? 'Publicada' : 'Rascunho'}</span>
      {editor.generatedAt && (
        <p className="note mtg-ata__generated">
          <Icon name="clock" /> Último PDF gerado: {numericDateTime(editor.generatedAt)}
        </p>
      )}
    </Block>
  );
}
