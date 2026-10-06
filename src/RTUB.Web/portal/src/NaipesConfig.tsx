import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Dialog } from './Dialog';
import type { Outcome } from './eventsApi';
import { Icon } from './icons';
import { loginTo } from './musicApi';
import { naipesApi, type NaipeSetting } from './naipesApi';

const MAX_PICTURE = 5 * 1024 * 1024;
const PICTURE_ACCEPT = 'image/jpeg,image/png,image/webp';

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Só um Admin ou o Owner altera os instrumentos dos naipes.'
      : o.kind === 'notfound'
        ? 'Este instrumento já não existe.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

/**
 * /naipes/config (task 033; was the Blazor page). Admin and Owner choose which instruments /naipes shows, their order
 * and picture. Any other member gets a clear refusal; the server decides (NaipeBoardService).
 */
export default function NaipesConfig() {
  const [settings, setSettings] = useState<NaipeSetting[] | 'signin' | 'forbidden' | null>();
  const [editing, setEditing] = useState<NaipeSetting>();

  useEffect(() => {
    document.title = 'Configurar naipes · RTUB';
    naipesApi
      .settings()
      .then((o) => setSettings(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : o.kind === 'forbidden' ? 'forbidden' : null));
  }, []);

  return (
    <section className="page wrap events-page naipes-page" aria-labelledby="naipes-config-title">
      <header className="page__head events-page__head">
        <div>
          <a className="back-link" href={portal.naipes}>
            <Icon name="arrowLeft" />
            Naipes
          </a>
          <h1 id="naipes-config-title" className="page__title">
            Configurar naipes
          </h1>
          <p className="page__lead">Que instrumentos aparecem nos naipes, por que ordem e com que imagem.</p>
        </div>
      </header>

      {settings === undefined ? (
        <Loading label="A carregar os instrumentos…" />
      ) : settings === 'signin' ? (
        <div className="notice" role="status">
          <p>A configuração dos naipes é da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.naipesConfig)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : settings === 'forbidden' ? (
        <div className="notice" role="status">
          <p>Só um Admin ou o Owner configura os naipes. Os vídeos e as imagens continuam todos na página dos naipes.</p>
          <a className="btn btn--ghost btn--sm" href={portal.naipes}>
            <Icon name="arrowLeft" />
            Voltar aos naipes
          </a>
        </div>
      ) : settings === null ? (
        <div className="notice" role="status">
          <p>Não foi possível carregar os instrumentos. Tenta outra vez daqui a pouco.</p>
        </div>
      ) : (
        <>
          <p className="note">Ordem mais baixa primeiro. Um instrumento oculto sai da página dos naipes, mas o conteúdo dele fica guardado.</p>
          <ul className="naipe-settings">
            {settings.map((s) => (
              <li key={s.id}>
                <article className={s.isVisible ? 'naipe-setting' : 'naipe-setting is-hidden'}>
                  <span className="naipe-setting__picture">
                    {s.pictureUrl ? <img src={s.pictureUrl} alt="" width="72" height="72" loading="lazy" /> : <Icon name="music" />}
                  </span>
                  <strong className="naipe-setting__name">{s.label}</strong>
                  <span className="member-badges">
                    <span className="member-badge">Ordem {s.sortOrder}</span>
                    <span className={s.isVisible ? 'member-badge member-badge--position' : 'member-badge'}>{s.isVisible ? 'Visível' : 'Oculto'}</span>
                  </span>
                  <button type="button" className="btn btn--ghost btn--sm" onClick={() => setEditing(s)}>
                    <Icon name="pencil" />
                    Editar<span className="sr-only"> {s.label}</span>
                  </button>
                </article>
              </li>
            ))}
          </ul>
        </>
      )}

      {editing && (
        <SettingDialog
          setting={editing}
          onClose={() => setEditing(undefined)}
          onChanged={(next) => {
            setSettings(next);
            setEditing(next.find((s) => s.id === editing.id));
          }}
        />
      )}
    </section>
  );
}

/** One instrument: picture (upload / remove, saved at once), order 0-999 and visibility (saved with "Guardar"). */
function SettingDialog({ setting, onClose, onChanged }: { setting: NaipeSetting; onClose: () => void; onChanged: (next: NaipeSetting[]) => void }) {
  const [sortOrder, setSortOrder] = useState(String(setting.sortOrder));
  const [visible, setVisible] = useState(setting.isVisible);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();

  const fail = (o: Outcome<unknown>, what: string) => {
    if (o.kind === 'invalid') setErrors(o.errors);
    else {
      setErrors({});
      setBanner(problem(o, what));
    }
  };

  const picture = async (file?: File) => {
    if (!file) return;
    if (file.size > MAX_PICTURE) return setErrors({ picture: 'A imagem não pode exceder 5 MB.' });
    setBusy(true);
    setBanner(undefined);
    const o = await naipesApi.setPicture(setting.id, file);
    setBusy(false);
    if (o.kind === 'ok') {
      setErrors({});
      onChanged(o.data);
    } else fail(o, 'enviar a imagem');
  };

  const removePicture = async () => {
    setBusy(true);
    setBanner(undefined);
    const o = await naipesApi.removePicture(setting.id);
    setBusy(false);
    if (o.kind === 'ok') onChanged(o.data);
    else fail(o, 'remover a imagem');
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setBanner(undefined);
    const o = await naipesApi.saveSetting(setting.id, visible, sortOrder.trim() ? Number(sortOrder) : null);
    setBusy(false);
    if (o.kind === 'ok') {
      onChanged(o.data);
      onClose();
    } else fail(o, 'guardar');
  };

  const field = (key: string) => (errors[key] ? 'form__field form__field--error' : 'form__field');
  const error = (key: string) =>
    errors[key] && (
      <p className="form__error" role="alert">
        {errors[key]}
      </p>
    );

  return (
    <Dialog
      title={`Editar ${setting.label}`}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Guardar
          </button>
        </>
      }
    >
      <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
        {banner && (
          <p className="form__banner" role="alert">
            {banner}
          </p>
        )}
        <div className={field('picture')}>
          <span className="naipe-setting__picture naipe-setting__picture--lg">
            {setting.pictureUrl ? <img src={setting.pictureUrl} alt={setting.label} width="96" height="96" /> : <Icon name="music" />}
          </span>
          <label htmlFor={`${id}-picture`}>Imagem</label>
          <input id={`${id}-picture`} type="file" accept={PICTURE_ACCEPT} onChange={(e) => picture(e.target.files?.[0])} disabled={busy} />
          <p className="form__hint">JPEG, PNG ou WebP, até 5 MB. Fica guardada logo que a escolhes.</p>
          {error('picture')}
          {setting.pictureUrl && (
            <button type="button" className="btn btn--ghost btn--sm" onClick={removePicture} disabled={busy}>
              <Icon name="trash" />
              Remover imagem
            </button>
          )}
        </div>
        <div className={field('sortOrder')}>
          <label htmlFor={`${id}-order`}>Ordem</label>
          <input id={`${id}-order`} type="number" inputMode="numeric" step="1" min="0" max="999" value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} />
          <p className="form__hint">De 0 a 999; os números mais baixos aparecem primeiro.</p>
          {error('sortOrder')}
        </div>
        <label className="form__check">
          <input type="checkbox" checked={visible} onChange={(e) => setVisible(e.target.checked)} disabled={busy} />
          {visible ? 'Aparece na página dos naipes' : 'Escondido da página dos naipes'}
        </label>
      </form>
    </Dialog>
  );
}
