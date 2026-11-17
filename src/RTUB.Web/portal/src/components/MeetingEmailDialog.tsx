// The email-the-members dialog of /meetings (draft, sandboxed preview, send).
import { useEffect, useId, useState } from 'react';
import { Loading } from './AppShared';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingCard, MeetingEmailDraft, MeetingNoticeResult } from '../data/meetingsTypes';
import { MAX_SUBJECT, MAX_EMAIL_BODY, problem, type Errors, fieldClass, FieldError, Banner, Spinner, MeetingSummary, PeopleList } from './MeetingUi';

/**
 * The preview document for the sandboxed frame (sandbox="", srcdoc: no script runs and nothing reaches this page). The
 * frame inherits the page's Content-Security-Policy, which refuses inline CSS and other origins' images, so those are
 * dropped first (a missed one is only refused, never applied) and the browser shows the email's text without a refusal
 * in the console; blocks the template pre-wraps keep their line breaks. Parsing happens in an inert DOMParser document.
 */
function previewDocument(html: string): string {
  const cleaned = html
    .replace(/<style\b[\s\S]*?<\/style\s*>/gi, '')
    .replace(/\sstyle\s*=\s*("[^"]*"|'[^']*')/gi, (_match, value: string) => (/white-space\s*:\s*pre/i.test(value) ? ' data-mtg-pre=""' : ''));
  const doc = new DOMParser().parseFromString(cleaned, 'text/html');
  doc.querySelectorAll('script, link, base, meta[http-equiv], iframe, object, embed').forEach((el) => el.remove());
  doc.querySelectorAll('img').forEach((img) => {
    let sameOrigin = false;
    try {
      sameOrigin = new URL(img.getAttribute('src') ?? '', location.href).origin === location.origin;
    } catch {
      sameOrigin = false;
    }
    if (!sameOrigin) img.replaceWith(doc.createTextNode(img.getAttribute('alt') ?? ''));
  });
  doc.querySelectorAll('[data-mtg-pre]').forEach((el) => {
    const walker = doc.createTreeWalker(el, NodeFilter.SHOW_TEXT);
    const texts: Text[] = [];
    while (walker.nextNode()) texts.push(walker.currentNode as Text);
    for (const text of texts) {
      const lines = text.data.split('\n');
      if (lines.length < 2) continue;
      const fragment = doc.createDocumentFragment();
      lines.forEach((line, i) => {
        if (i > 0) fragment.append(doc.createElement('br'));
        fragment.append(line);
      });
      text.replaceWith(fragment);
    }
  });
  return `<!doctype html>${doc.documentElement.outerHTML}`;
}

/**
 * The meeting email: subject, the statement as the body, a preview of the rendered email and who receives it. The
 * preview is the server's HTML in a sandboxed frame (no scripts, no same-origin access): nothing of it reaches this
 * page's DOM.
 */
export function EmailDialog({ card, onClose, onSent }: { card: MeetingCard; onClose: () => void; onSent: (r: MeetingNoticeResult) => void }) {
  const [draft, setDraft] = useState<MeetingEmailDraft | null>();
  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');
  const [tunoId, setTunoId] = useState('');
  const [showPreview, setShowPreview] = useState(false);
  const [preview, setPreview] = useState<{ html?: string; error?: string }>();
  const [errors, setErrors] = useState<Errors>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();

  useEffect(() => {
    meetingsApi.emailDraft(card.id).then((o) => {
      if (o.kind !== 'ok') {
        setDraft(null);
        setBanner(problem(o, 'preparar o email'));
        return;
      }
      setDraft(o.data);
      setSubject(o.data.subject);
      setBody(o.data.body);
    });
  }, [card.id]);

  useEffect(() => {
    if (!showPreview) return;
    let live = true;
    const timer = setTimeout(() => {
      meetingsApi.emailPreview(card.id, body).then((o) => {
        if (live) setPreview(o.kind === 'ok' ? { html: previewDocument(o.data.html) } : { error: problem(o, 'gerar a pré-visualização') });
      });
    }, 400);
    return () => {
      live = false;
      clearTimeout(timer);
    };
  }, [card.id, body, showPreview]);

  const send = async () => {
    setBusy(true);
    setBanner(undefined);
    setErrors({});
    const o = await meetingsApi.sendEmail(card.id, subject, body, tunoId || null);
    setBusy(false);
    if (o.kind === 'ok') onSent(o.data);
    else if (o.kind === 'invalid') {
      setErrors(o.errors);
      if (o.errors.notice) setBanner(o.errors.notice);
    } else setBanner(problem(o, 'enviar o email'));
  };

  const recipients = draft?.recipients ?? [];
  const canSend = Boolean(draft) && recipients.length > 0 && subject.trim() !== '' && body.trim() !== '';

  return (
    <Dialog
      title="Enviar notificação da reunião"
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--primary" onClick={send} disabled={busy || !canSend}>
            <Spinner on={busy} />
            {busy ? 'A enviar emails…' : 'Enviar Notificação'}
          </button>
        </>
      }
    >
      <MeetingSummary card={card} />
      <Banner text={banner} />
      {draft === undefined ? (
        <Loading label="A carregar subscritores..." />
      ) : draft === null ? null : (
        <div className="form mtg-form">
          <div className={fieldClass(errors, 'subject')}>
            <label htmlFor={`${id}-subject`}>
              Assunto <span className="mtg-required">*</span>
            </label>
            <input id={`${id}-subject`} type="text" maxLength={MAX_SUBJECT} value={subject} onChange={(e) => setSubject(e.target.value)} />
            <FieldError errors={errors} name="subject" />
          </div>
          <div className={fieldClass(errors, 'body')}>
            <label htmlFor={`${id}-body`}>
              Corpo do Email <span className="mtg-required">*</span>
            </label>
            <textarea id={`${id}-body`} rows={12} maxLength={MAX_EMAIL_BODY} value={body} onChange={(e) => setBody(e.target.value)} />
            <p className="form__hint">Escreva a mensagem da reunião em texto simples. Será formatada automaticamente no email.</p>
            <FieldError errors={errors} name="body" />
          </div>
          <div className="mtg-preview">
            <button type="button" className="btn btn--ghost btn--sm" aria-expanded={showPreview} onClick={() => setShowPreview((s) => !s)}>
              <Icon name="eye" />
              {showPreview ? 'Ocultar' : 'Ver'} Pré-visualização
            </button>
            {showPreview && (
              <div className="mtg-preview__box">
                <p className="mtg-preview__label">Pré-visualização do Email:</p>
                {preview?.error ? (
                  <p className="form__error">{preview.error}</p>
                ) : preview?.html === undefined ? (
                  <Loading label="A gerar a pré-visualização…" />
                ) : (
                  <iframe className="mtg-preview__frame" title="Pré-visualização do email" sandbox="" referrerPolicy="no-referrer" srcDoc={preview.html} />
                )}
              </div>
            )}
          </div>
          {draft.tunoOptions.length > 0 && (
            <div className={fieldClass(errors, 'tunoId')}>
              <label htmlFor={`${id}-tuno`}>Adicionar um Representante de Tunos</label>
              <span className="control control--select">
                <select id={`${id}-tuno`} value={tunoId} onChange={(e) => setTunoId(e.target.value)}>
                  <option value="">Nenhum representante selecionado</option>
                  {draft.tunoOptions.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.label}
                    </option>
                  ))}
                </select>
              </span>
              <p className="form__hint">Opcionalmente adicione um representante de Tunos à lista de destinatários para reuniões CV.</p>
              <FieldError errors={errors} name="tunoId" />
            </div>
          )}
          <p className="mtg-info">
            <Icon name="envelope" />
            {recipients.length} membros irão receber esta mensagem.
          </p>
          <PeopleList title="Destinatários:" people={recipients} empty="Nenhum destinatário encontrado." />
          <PeopleList title="Quem não irá receber:" people={draft.notReceiving} />
        </div>
      )}
    </Dialog>
  );
}
