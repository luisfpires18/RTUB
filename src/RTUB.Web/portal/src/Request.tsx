import { useEffect, useRef, useState, type ChangeEvent, type FormEvent, type ReactNode } from 'react';
import { submitRequest, type FieldErrors, type RequestForm } from './api';
import { contactEmail, portal } from './content';
import { Icon } from './icons';

// Suggestions only: the event type stays free text, as on the Blazor form.
const examples = ['Serenata', 'Casamento', 'Batizado', 'Aniversário', 'Arraial', 'Arruada', 'Convívio', 'Missa', 'Festival'];

const empty: RequestForm = {
  name: '',
  email: '',
  phone: '',
  eventType: '',
  preferredDate: '',
  isDateRange: false,
  preferredEndDate: '',
  location: '',
  message: '',
  website: '',
};

const fieldOrder: (keyof RequestForm)[] = ['name', 'email', 'phone', 'eventType', 'preferredDate', 'preferredEndDate', 'location', 'message'];

const hints: FieldErrors = {
  eventType: 'Por exemplo: serenata, casamento, aniversário.',
  message: 'Horário, duração, número de pessoas ou o que ajudar a preparar a atuação.',
};

type Banner = 'invalid' | 'expired' | 'throttled' | 'failed' | null;

const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

/**
 * Mirrors the server rules (Request entity annotations + RequestValidationService) so most mistakes
 * are caught before sending. The server re-validates everything; its answer wins.
 */
function validate(f: RequestForm): FieldErrors {
  const e: FieldErrors = {};
  const need = (key: keyof RequestForm, value: string, message: string, max: number, tooLong: string) => {
    if (!value.trim()) e[key] = message;
    else if (value.length > max) e[key] = tooLong;
  };
  need('name', f.name, 'O nome é obrigatório', 200, 'O nome não pode exceder 200 caracteres');
  need('email', f.email, 'O email é obrigatório', 200, 'O email não pode exceder 200 caracteres');
  const at = f.email.indexOf('@');
  if (!e.email && (at <= 0 || at !== f.email.lastIndexOf('@') || at === f.email.length - 1)) e.email = 'Endereço de email inválido';
  need('phone', f.phone, 'O telefone é obrigatório', 20, 'O telefone não pode exceder 20 caracteres');
  need('eventType', f.eventType, 'O tipo de evento é obrigatório', 100, 'O tipo de evento não pode exceder 100 caracteres');
  need('location', f.location, 'A localização é obrigatória', 200, 'A localização não pode exceder 200 caracteres');
  if (f.message.length > 2000) e.message = 'A mensagem não pode exceder 2000 caracteres';

  const now = today();
  if (!f.preferredDate) e.preferredDate = 'A data preferida é obrigatória';
  else if (f.preferredDate < now) e.preferredDate = 'A data não pode ser no passado.';
  if (f.isDateRange) {
    if (!f.preferredEndDate) e.preferredEndDate = 'A data de fim é obrigatória para intervalo de datas.';
    else if (f.preferredEndDate < now) e.preferredEndDate = 'O fim do intervalo já passou.';
    else if (f.preferredDate && f.preferredEndDate < f.preferredDate)
      e.preferredEndDate = 'A data de fim deve ser posterior ou igual à data de início.';
  }
  return e;
}

/** /request - the public performance request, submitted to POST /api/public/requests. */
export default function RequestPage() {
  const [form, setForm] = useState<RequestForm>(empty);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [banner, setBanner] = useState<Banner>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const successHeading = useRef<HTMLHeadingElement>(null);
  const bannerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    document.title = 'Pedir uma atuação · RTUB';
  }, []);

  useEffect(() => {
    if (submitted) successHeading.current?.focus();
  }, [submitted]);

  const set =
    (key: keyof RequestForm) => (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
      const value = event.target.type === 'checkbox' ? (event.target as HTMLInputElement).checked : event.target.value;
      setForm((f) => ({ ...f, [key]: value }));
      if (errors[key]) setErrors((e) => ({ ...e, [key]: undefined }));
    };

  const showErrors = (found: FieldErrors) => {
    setErrors(found);
    setBanner('invalid');
    const first = fieldOrder.find((k) => found[k]);
    if (first) document.getElementById(`request-${first}`)?.focus();
  };

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (submitting) return;

    const found = validate(form);
    if (Object.keys(found).length > 0) return showErrors(found);

    setSubmitting(true);
    setBanner(null);
    const outcome = await submitRequest(form);
    setSubmitting(false);

    if (outcome.kind === 'submitted') {
      setSubmitted(true);
    } else if (outcome.kind === 'invalid') {
      showErrors(outcome.errors);
    } else {
      setBanner(outcome.kind);
      requestAnimationFrame(() => bannerRef.current?.focus());
    }
  };

  const reset = () => {
    setForm(empty);
    setErrors({});
    setBanner(null);
    setSubmitted(false);
  };

  const field = (key: keyof RequestForm) => ({
    id: `request-${key}`,
    name: key,
    'aria-invalid': errors[key] ? true : undefined,
    'aria-describedby': errors[key] ? `request-${key}-error` : hints[key] ? `request-${key}-hint` : undefined,
  });

  return (
    <section className="page wrap" aria-labelledby="request-page-title">
      <header className="page__head">
        <p className="eyebrow">Pedidos</p>
        <h1 id="request-page-title" className="page__title">
          Pedir uma atuação
        </h1>
        <p className="page__lead">
          Uma serenata à janela, a entrada dos noivos, a festa da aldeia: diga-nos o que imagina e combinamos os
          detalhes consigo.
        </p>
      </header>

      <div className="request">
        <div className="request__form-card">
          {submitted ? (
            <div className="request__done">
              <Icon name="check" className="request__done-icon" />
              <h2 ref={successHeading} tabIndex={-1} className="request__done-title">
                Pedido enviado
              </h2>
              <p>Obrigado. A RTUB vai ver a disponibilidade e responder pelo contacto que indicou.</p>
              <div className="account__actions">
                <button type="button" className="btn btn--ghost" onClick={reset}>
                  Fazer outro pedido
                </button>
                <a className="btn btn--ghost" href={portal.home}>
                  Voltar ao portal
                </a>
              </div>
            </div>
          ) : (
            <form className="form" noValidate onSubmit={onSubmit} aria-describedby="request-required-note">
              {banner && (
                <div ref={bannerRef} tabIndex={-1} className={`form__banner form__banner--${banner}`} role="alert">
                  {banner === 'invalid' && 'Há campos por corrigir. Veja as indicações abaixo.'}
                  {banner === 'expired' && 'O formulário expirou. Recarregue a página e tente de novo.'}
                  {banner === 'throttled' &&
                    `Recebemos vários pedidos seguidos deste dispositivo. Tente mais tarde ou escreva para ${contactEmail}.`}
                  {banner === 'failed' &&
                    `Não foi possível enviar o pedido agora. Tente de novo ou escreva para ${contactEmail}.`}
                </div>
              )}

              <p id="request-required-note" className="note">
                Os campos com * são obrigatórios.
              </p>

              <div className="form__row">
                <Field id="name" label="Nome *" error={errors.name}>
                  <input {...field('name')} type="text" autoComplete="name" maxLength={200} value={form.name} onChange={set('name')} />
                </Field>
                <Field id="email" label="Email *" error={errors.email}>
                  <input {...field('email')} type="email" autoComplete="email" maxLength={200} value={form.email} onChange={set('email')} />
                </Field>
              </div>

              <div className="form__row">
                <Field id="phone" label="Telefone *" error={errors.phone}>
                  <input {...field('phone')} type="tel" autoComplete="tel" maxLength={20} value={form.phone} onChange={set('phone')} />
                </Field>
                <Field id="eventType" label="Tipo de evento *" error={errors.eventType}>
                  <input {...field('eventType')} type="text" list="request-event-types" maxLength={100} value={form.eventType} onChange={set('eventType')} />
                  <datalist id="request-event-types">
                    {examples.map((e) => (
                      <option key={e} value={e} />
                    ))}
                  </datalist>
                </Field>
              </div>

              <label className="form__check">
                <input type="checkbox" checked={form.isDateRange} onChange={set('isDateRange')} />
                Definir intervalo de datas
              </label>

              <div className="form__row">
                <Field id="preferredDate" label={form.isDateRange ? 'Data de início *' : 'Data *'} error={errors.preferredDate}>
                  <input {...field('preferredDate')} type="date" min={today()} value={form.preferredDate} onChange={set('preferredDate')} />
                </Field>
                {form.isDateRange && (
                  <Field id="preferredEndDate" label="Data de fim *" error={errors.preferredEndDate}>
                    <input
                      {...field('preferredEndDate')}
                      type="date"
                      min={form.preferredDate || today()}
                      value={form.preferredEndDate}
                      onChange={set('preferredEndDate')}
                    />
                  </Field>
                )}
              </div>

              <Field id="location" label="Local *" error={errors.location}>
                <input {...field('location')} type="text" autoComplete="address-level2" maxLength={200} value={form.location} onChange={set('location')} />
              </Field>

              <Field id="message" label="Mais informações" error={errors.message}>
                <textarea {...field('message')} rows={5} maxLength={2000} value={form.message} onChange={set('message')} />
              </Field>

              {/* Honeypot: hidden from people and assistive tech; bots tend to fill it. */}
              <div className="hp" aria-hidden="true">
                <label htmlFor="request-website">Não preencher</label>
                <input id="request-website" name="website" type="text" tabIndex={-1} autoComplete="off" value={form.website} onChange={set('website')} />
              </div>

              <button type="submit" className="btn btn--primary btn--lg form__submit" disabled={submitting} aria-busy={submitting}>
                {submitting ? (
                  <>
                    <span className="spinner spinner--small" aria-hidden="true" />A enviar…
                  </>
                ) : (
                  <>
                    <Icon name="send" />
                    Enviar pedido
                  </>
                )}
              </button>
            </form>
          )}
        </div>

        <aside className="request__go" aria-labelledby="request-go-title">
          <h2 id="request-go-title" className="request__title">
            Depois de enviar
          </h2>
          <p>
            Cada pedido chega diretamente à RTUB, que confirma a disponibilidade e responde pelo contacto indicado.
          </p>
          <p className="request__alt">Prefere escrever?</p>
          <a className="door__mail" href={`mailto:${contactEmail}`}>
            <Icon name="envelope" />
            {contactEmail}
          </a>
          <p className="note">
            Saiba como são tratados os dados na <a href={portal.privacy}>Política de Privacidade</a>.
          </p>
        </aside>
      </div>
    </section>
  );
}

function Field({ id, label, error, children }: { id: keyof RequestForm; label: string; error?: string; children: ReactNode }) {
  const hint = hints[id];
  return (
    <div className={error ? 'form__field form__field--error' : 'form__field'}>
      <label htmlFor={`request-${id}`}>{label}</label>
      {children}
      {hint && !error && (
        <p id={`request-${id}-hint`} className="form__hint">
          {hint}
        </p>
      )}
      {error && (
        <p id={`request-${id}-error`} className="form__error">
          {error}
        </p>
      )}
    </div>
  );
}
