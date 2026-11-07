import { Dialog } from './Dialog';
import { Icon } from './icons';
import type { ReleaseNote } from './releaseNotesApi';

/**
 * "Nova atualização <version>" (task 083, #264): the notes as plain text, one section per release, newest first.
 * One dialog whatever was skipped: the first section is the running release, the others the unread ones below it.
 * The strings come from releases/<version>.json inside the build and are rendered as text, never as HTML.
 */
export function ReleaseNotesDialog({
  notes,
  onClose,
  busy = false,
  error,
  className,
}: {
  notes: ReleaseNote[];
  onClose: () => void;
  busy?: boolean;
  error?: string;
  className?: string;
}) {
  const [first, ...older] = notes;
  return (
    <Dialog
      title={first.title}
      className={className ? `release-notes ${className}` : 'release-notes'}
      onClose={onClose}
      footer={
        <button type="button" className="btn btn--primary" onClick={onClose} disabled={busy}>
          <Icon name="check" />
          Entendido
        </button>
      }
    >
      <ul className="release-notes__list">
        {first.items.map((item, i) => (
          <li key={i}>{item}</li>
        ))}
      </ul>
      {older.length > 0 && (
        <section className="release-notes__older" aria-label="Versões anteriores">
          <p className="note">Desde a última vez que viste as novidades:</p>
          {older.map((note) => (
            <div key={note.version} className="release-notes__release">
              <h3 className="release-notes__version">{note.title}</h3>
              <ul className="release-notes__list">
                {note.items.map((item, i) => (
                  <li key={i}>{item}</li>
                ))}
              </ul>
            </div>
          ))}
        </section>
      )}
      {error && (
        <p className="form__error" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
