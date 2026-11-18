// Layout pieces shared by the ata editor and the ata view: titled Block and SecretarySelect.
import { useId, type ReactNode } from 'react';
import { Icon, type IconName } from './icons';
import { fieldClass, type Errors } from './MeetingUi';
import { FieldError } from './ui/FieldError';
import type { MeetingPersonOption } from '../data/meetingsTypes';

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
