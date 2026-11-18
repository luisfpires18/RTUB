// Card files and links panel.
import { useId, useRef, useState, type FormEvent, type KeyboardEvent } from 'react';
import { Icon } from './icons';
import { size } from '../data/logisticsFiles';
import { Section, type Apply } from './LogisticsPanel';
import { logisticsApi, problem, uploadAttachment, type Board, type CardAttachment, type CardDetail } from '../data/logisticsApi';
import { FILE_TYPES, MAX_FILE } from '../data/logisticsFiles';
import { FieldError } from './ui/FieldError';
import { EmptyNote } from './ui/EmptyNote';
import { IconAction } from './ui/IconAction';

type Upload = { name: string; share: number; file: File; error?: string };

/** The card's own files and links (not the board's files, not links to other cards). */
export function Attachments({
  detail,
  board,
  apply,
  errors,
  onUploaded,
}: {
  detail: CardDetail;
  board: Board;
  apply: Apply;
  errors: Record<string, string>;
  onUploaded: (d: CardDetail) => void;
}) {
  const [uploads, setUploads] = useState<Upload[]>([]);
  const [error, setError] = useState<string>();
  const [link, setLink] = useState<{ url: string; name: string }>();
  const [confirm, setConfirm] = useState<number>();
  const fileInput = useRef<HTMLInputElement>(null);
  const id = useId();
  const cardId = detail.card.id;

  const send = async (file: File) => {
    const name = file.name;
    if (file.size > MAX_FILE) return setUploads((u) => [...u.filter((x) => x.name !== name), { name, file, share: 0, error: 'Ficheiro grande demais: o limite são 10 MB.' }]);
    if (!FILE_TYPES.includes(file.type))
      return setUploads((u) => [...u.filter((x) => x.name !== name), { name, file, share: 0, error: 'Só PDF, Word, Excel, TXT, PNG ou JPG.' }]);
    setUploads((u) => [...u.filter((x) => x.name !== name), { name, file, share: 0 }]);
    const o = await uploadAttachment(cardId, file, (share) => setUploads((u) => u.map((x) => (x.name === name ? { ...x, share } : x))));
    if (o.kind === 'ok') {
      setUploads((u) => u.filter((x) => x.name !== name));
      onUploaded(o.data);
    } else {
      setUploads((u) => u.map((x) => (x.name === name ? { ...x, error: problem(o, 'carregar o ficheiro') } : x)));
    }
  };

  const open = async (a: CardAttachment) => {
    setError(undefined);
    const o = await logisticsApi.attachment(cardId, a.id);
    if (o.kind === 'ok') window.open(o.data.url, '_blank', 'noopener,noreferrer');
    else setError(problem(o, 'abrir o anexo'));
  };

  const boardFile = async (name: string) => {
    setError(undefined);
    const o = await logisticsApi.file(board.id, name);
    if (o.kind === 'ok') window.open(o.data.url, '_blank', 'noopener');
    else setError(problem(o, 'abrir o ficheiro'));
  };

  const addLink = async (e: FormEvent | KeyboardEvent) => {
    e.preventDefault();
    if (!link) return;
    setError(undefined);
    if (await apply(logisticsApi.addLink(cardId, link.url.trim(), link.name.trim()), 'url', 'adicionar a ligação')) setLink(undefined);
  };

  return (
    <Section id={`${id}-att`} title="Anexos" extra={detail.attachments.length > 0 && <small>{detail.attachments.length}</small>}>
      {detail.attachments.length === 0 && uploads.length === 0 && <EmptyNote>Sem anexos neste cartão.</EmptyNote>}
      {(detail.attachments.length > 0 || uploads.length > 0) && (
        <ul className="lx-files">
          {detail.attachments.map((a) => (
            <li key={a.id}>
              <Icon name={a.kind === 'Link' ? 'external' : 'file'} />
              <button type="button" className="lx-files__name lx-files__open" onClick={() => open(a)}>
                {a.name}
                <small>
                  {a.kind === 'Link' ? a.host : `${(a.extension ?? '').replace('.', '').toUpperCase()} · ${size(a.sizeBytes)}`}
                  {a.addedBy && ` · ${a.addedBy.displayName}`}
                </small>
              </button>
              <IconAction icon={a.kind === 'Link' ? 'external' : 'download'} label={a.kind === 'Link' ? `Abrir ${a.name}` : `Descarregar ${a.name}`} onClick={() => open(a)} />
              {a.canDelete &&
                (confirm === a.id ? (
                  <button
                    type="button"
                    className="btn btn--danger btn--sm"
                    onClick={async () => (setConfirm(undefined), await apply(logisticsApi.removeAttachment(cardId, a.id), 'attachments', 'remover o anexo'))}
                  >
                    Remover?
                  </button>
                ) : (
                  <IconAction icon="trash" label={`Remover ${a.name}`} onClick={() => setConfirm(a.id)} danger />
                ))}
            </li>
          ))}
          {uploads.map((u) => (
            <li key={`up-${u.name}`} className={u.error ? 'lx-upload-row lx-upload-row--error' : 'lx-upload-row'}>
              <Icon name={u.error ? 'warning' : 'upload'} />
              <span className="lx-files__name">
                {u.name}
                {u.error ? (
                  <small role="alert">{u.error}</small>
                ) : (
                  <span className="lx-progress__bar" role="progressbar" aria-label={`A carregar ${u.name}`} aria-valuenow={Math.round(u.share * 100)}>
                    <span style={{ width: `${Math.round(u.share * 100)}%` }} />
                  </span>
                )}
              </span>
              {u.error && FILE_TYPES.includes(u.file.type) && u.file.size <= MAX_FILE && (
                <button type="button" className="btn btn--ghost btn--sm" onClick={() => send(u.file)}>
                  Tentar novamente
                </button>
              )}
              {u.error && (
                <IconAction icon="close" label="Dispensar" onClick={() => setUploads((x) => x.filter((y) => y.name !== u.name))} />
              )}
            </li>
          ))}
        </ul>
      )}
      {detail.canWork && (
        <div className="lx-inline">
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => fileInput.current?.click()}>
            <Icon name="upload" />
            Carregar ficheiro
          </button>
          <input
            ref={fileInput}
            className="sr-only"
            type="file"
            tabIndex={-1}
            aria-hidden="true"
            accept=".pdf,.docx,.doc,.xlsx,.xls,.txt,.png,.jpg,.jpeg"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) send(file);
              e.target.value = '';
            }}
          />
          {!link && (
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setLink({ url: '', name: '' })}>
              <Icon name="link" />
              Adicionar ligação
            </button>
          )}
          <small className="note">PDF, Word, Excel, TXT, PNG ou JPG, até 10 MB.</small>
        </div>
      )}
      {link && (
        <div className="lx-link-form">
          <input
            type="url"
            inputMode="url"
            aria-label="Endereço (https://…)"
            placeholder="https://…"
            autoFocus
            value={link.url}
            onChange={(e) => setLink({ ...link, url: e.target.value })}
            onKeyDown={(e) => e.key === 'Enter' && addLink(e)}
          />
          <input
            type="text"
            aria-label="Nome (opcional)"
            placeholder="Nome (opcional)"
            maxLength={200}
            value={link.name}
            onChange={(e) => setLink({ ...link, name: e.target.value })}
            onKeyDown={(e) => e.key === 'Enter' && addLink(e)}
          />
          <div className="lx-inline">
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setLink(undefined)}>
              Cancelar
            </button>
            <button type="button" className="btn btn--primary btn--sm" disabled={!link.url.trim()} onClick={addLink}>
              Adicionar
            </button>
          </div>
        </div>
      )}
      <FieldError text={error ?? errors.attachments ?? errors.url ?? errors.name} />
      {board.files.length > 0 && (
        <details className="lx-ws__board-files">
          <summary>Ficheiros do quadro ({board.files.length}): partilhados por todos os cartões</summary>
          <ul className="lx-files">
            {board.files.map((f) => (
              <li key={f.name}>
                <Icon name="folder" />
                <span className="lx-files__name">
                  {f.name}
                  <small>
                    {f.extension.replace('.', '').toUpperCase()} · {size(f.sizeBytes)}
                  </small>
                </span>
                <IconAction icon="download" label={`Descarregar ${f.name}`} onClick={() => boardFile(f.name)} />
              </li>
            ))}
          </ul>
        </details>
      )}
    </Section>
  );
}
