// Pieces shared by the /profile sections: section frame, form state, saved note, form buttons, fact cell.
import { Spinner } from './ui/Spinner';
import { FieldError } from './ui/FieldError';
import { useState, type ReactNode, type Ref } from 'react';
import { outcomeText, type Outcome } from '../data/api';
import { Icon, type IconName } from './icons';

export const problem = (o: Outcome<unknown>, what: string) =>
  outcomeText(o, what);

export function Section({
  id,
  title,
  icon,
  action,
  sectionRef,
  children,
}: {
  id: string;
  title: string;
  icon: IconName;
  action?: ReactNode;
  sectionRef?: Ref<HTMLElement>;
  children: ReactNode;
}) {
  return (
    <section ref={sectionRef} className="profile-section" id={id} aria-labelledby={`${id}-title`}>
      <header className="profile-section__head">
        <h2 id={`${id}-title`} className="profile-section__title">
          <Icon name={icon} />
          {title}
        </h2>
        {action}
      </header>
      {children}
    </section>
  );
}

export function useFormState() {
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const field = (key: string) => (errors[key] ? 'form__field form__field--error' : 'form__field');
  const error = (key: string) =>
    <FieldError text={errors[key]} />;
  return { errors, setErrors, banner, setBanner, busy, setBusy, field, error };
}

export function SavedNote({ show }: { show: boolean }) {
  return show ? (
    <p className="profile-saved" role="status">
      <Icon name="check" />
      Guardado.
    </p>
  ) : null;
}

// ---------- Pessoal ----------

/** An email that may wrap only after the "@" and before each dot, never mid-word (<wbr> copies as nothing). */
export const breakableEmail = (email: string) =>
  email.split(/(?=\.)|(?<=@)/).map((part, i) => (
    <span key={i}>
      {i > 0 && <wbr />}
      {part}
    </span>
  ));

/** One "Pessoal" value as a small tile: an icon, the label and the value. `wide` takes two columns when there are two. */
export function Fact({ icon, label, accent, wide, children }: { icon: IconName; label: string; accent?: boolean; wide?: boolean; children: ReactNode }) {
  return (
    <div className={`profile-fact${accent ? ' profile-fact--accent' : ''}${wide ? ' profile-fact--wide' : ''}`}>
      <span className="profile-fact__icon" aria-hidden="true">
        <Icon name={icon} />
      </span>
      <dt>{label}</dt>
      <dd>{children ?? <span className="note">Não definido</span>}</dd>
    </div>
  );
}

export function FormButtons({ busy, onCancel, label = 'Guardar' }: { busy: boolean; onCancel?: () => void; label?: string }) {
  return (
    <div className="profile-form__buttons">
      {onCancel && (
        <button type="button" className="btn btn--ghost" onClick={onCancel} disabled={busy}>
          Cancelar
        </button>
      )}
      <button type="submit" className="btn btn--primary" disabled={busy}>
        {busy && <Spinner small />}
        {label}
      </button>
    </div>
  );
}

// ---------- Tuna ----------
