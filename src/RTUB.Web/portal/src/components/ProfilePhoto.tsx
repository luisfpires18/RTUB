// The /profile photo picker with crop and upload.
import { Avatar } from './ui/Avatar';
import { Spinner } from './ui/Spinner';
import { useId, useRef, useState } from 'react';
import { getCurrentUser } from '../data/api';
import { Cropper } from './Cropper';
import { Icon } from './icons';
import { memberAreaApi, type MyProfile } from '../data/memberAreaApi';
import { problem } from './ProfileShared';

const MAX_PHOTO = 10 * 1024 * 1024;

const PHOTO_TYPES = ['image/webp', 'image/jpeg', 'image/png'];

const ROUND = {
  title: 'Recortar a foto',
  label: 'Pré-visualização da foto de perfil. Arraste ou use as setas para enquadrar.',
  note: 'A foto aparece num círculo: deixa a cara ao centro do quadrado.',
};

/** The avatar of the header with its change-photo button (pick, square crop and upload). */
export function ProfilePhoto({ profile, onChange }: { profile: MyProfile; onChange: (avatarUrl: string) => void }) {
  const id = useId();
  const input = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);

  const pick = (picked: File | undefined) => {
    setSaved(false);
    if (input.current) input.current.value = '';
    if (!picked) return;
    if (!PHOTO_TYPES.includes(picked.type)) return setError('Escolhe uma imagem WebP, JPEG ou PNG.');
    if (picked.size > MAX_PHOTO) return setError('A imagem não pode passar de 10 MB.');
    setError(undefined);
    setFile(picked);
  };

  const upload = async (photo: Blob) => {
    setFile(undefined);
    setBusy(true);
    const o = await memberAreaApi.photo(photo);
    setBusy(false);
    if (o.kind === 'ok') {
      onChange(o.data.avatarUrl);
      setSaved(true);
      getCurrentUser(true).catch(() => undefined); // the menu's avatar, on the next page
    } else setError(problem(o, 'guardar a foto'));
  };

  return (
    <div className="profile-avatar">
      <Avatar className="profile-avatar__img" src={profile.member.avatarUrl} size={112} eager alt="A tua foto de perfil" />
      <input
        ref={input}
        id={id}
        className="sr-only"
        type="file"
        accept={PHOTO_TYPES.join(',')}
        aria-describedby={`${id}-hint`}
        onChange={(e) => pick(e.target.files?.[0])}
        disabled={busy}
      />
      <label htmlFor={id} className={busy ? 'profile-avatar__edit is-disabled' : 'profile-avatar__edit'} title="Mudar foto">
        {busy ? <Spinner small /> : <Icon name="pencil" />}
        <span className="sr-only">{busy ? 'A guardar a foto…' : 'Mudar foto de perfil'}</span>
      </label>
      <span id={`${id}-hint`} className="sr-only">
        WebP, JPEG ou PNG até 10 MB. Recortas em quadrado antes de guardar; a foto anterior é substituída.
      </span>
      {error && (
        <p className="form__error profile-avatar__msg" role="alert">
          {error}
        </p>
      )}
      {saved && (
        <p className="profile-saved profile-avatar__msg" role="status">
          <Icon name="check" />
          Foto guardada.
        </p>
      )}
      {file && <Cropper file={file} aspect={1} outWidth={600} text={ROUND} onCancel={() => setFile(undefined)} onDone={(blob) => upload(blob)} />}
    </div>
  );
}

// ---------- Notificações por email ----------
