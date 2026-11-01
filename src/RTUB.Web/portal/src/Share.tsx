import { useEffect, useState } from 'react';
import { portal } from './content';
import { Icon } from './icons';

// What another app shared with the installed RTUB: the manifest's share_target sends a GET to /share?title&text&url.
// The query stays in the address bar on purpose, so a reload keeps the shared content. Nothing is stored or sent on.
const query = new URLSearchParams(location.search);
const shared = {
  title: query.get('title') ?? '',
  text: query.get('text') ?? '',
  url: query.get('url') ?? '',
};

/**
 * /share (task 053): the Web Share Target. Shows the shared title, text and link read-only, with a copy button.
 */
export default function Share() {
  const [copied, setCopied] = useState(false);
  const empty = !shared.title && !shared.text && !shared.url;

  useEffect(() => {
    document.title = 'Partilhar · RTUB';
  }, []);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText([shared.title, shared.text, shared.url].filter(Boolean).join('\n\n'));
      setCopied(true);
    } catch {
      // The browser refused the clipboard: the fields stay there to select by hand.
    }
  };

  return (
    <section className="page wrap" aria-labelledby="share-title">
      <header className="page__head">
        <p className="eyebrow">Partilhar</p>
        <h1 id="share-title" className="page__title">
          Partilhar conteúdo
        </h1>
      </header>

      <div className="request__form-card account-card">
        {empty ? (
          <div className="account-done" role="status">
            <Icon name="inbox" />
            <p>Nenhum conteúdo para partilhar.</p>
            <a className="btn btn--primary" href={portal.home}>
              <Icon name="arrowLeft" />
              Voltar ao início
            </a>
          </div>
        ) : (
          <div className="form">
            <div className="form__field">
              <label htmlFor="share-field-title">Título</label>
              <input id="share-field-title" type="text" value={shared.title} readOnly />
            </div>
            <div className="form__field">
              <label htmlFor="share-field-text">Texto</label>
              <textarea id="share-field-text" rows={3} value={shared.text} readOnly />
            </div>
            {shared.url && (
              <div className="form__field">
                <label htmlFor="share-field-url">URL</label>
                <input id="share-field-url" type="url" value={shared.url} readOnly />
              </div>
            )}
            <div className="share__actions">
              <button type="button" className="btn btn--primary" onClick={copy}>
                <Icon name="journal" />
                Copiar
              </button>
              <a className="btn btn--ghost" href={portal.home}>
                <Icon name="arrowLeft" />
                Voltar
              </a>
            </div>
            <p className="note" role="status" aria-live="polite">
              {copied ? 'Copiado.' : ''}
            </p>
          </div>
        )}
      </div>
    </section>
  );
}
