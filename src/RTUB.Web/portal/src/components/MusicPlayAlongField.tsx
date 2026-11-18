// Song form field to upload or remove the song's play-along chart.
import { useEffect, useState } from 'react';
import { CheckField } from './ui/CheckField';
import { Icon } from './icons';
import { musicApi, type PlayAlongChart } from '../data/musicApi';
import { failedMessage } from '../data/musicFormHelpers';

/**
 * The song's play-along chart: a .json file made offline with tools/playalong. Sending and
 * removing act at once, apart from "Guardar"; the server validates the chart.
 */
export function PlayAlongField({ songId, has, onChange }: { songId: number; has: boolean; onChange: (has: boolean) => void }) {
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ text: string; error: boolean } | null>(null);
  // The stored chart, so ticking "dedilhado" can send it back with the flag changed.
  const [chart, setChart] = useState<PlayAlongChart | null>(null);
  const [picking, setPicking] = useState(false);

  useEffect(() => {
    if (!has) {
      setChart(null);
      return;
    }
    let live = true;
    musicApi.playAlong(songId).then((r) => {
      if (!live || r.kind !== 'ok') return;
      setChart(r.data.chart);
      setPicking(r.data.chart.picking);
    });
    return () => {
      live = false;
    };
  }, [songId, has]);

  const togglePicking = async (on: boolean) => {
    if (!chart) return;
    setBusy(true);
    setMessage(null);
    const next = { ...chart, picking: on };
    const r = await musicApi.savePlayAlong(songId, next);
    if (r.kind === 'ok') {
      setChart(next);
      setPicking(on);
    }
    finish(r, true, on ? 'Dedilhado incluído.' : 'Dedilhado retirado.');
  };

  const finish = (r: Awaited<ReturnType<typeof musicApi.savePlayAlong>>, nowHas: boolean, done: string) => {
    setBusy(false);
    if (r.kind === 'ok') {
      onChange(nowHas);
      setMessage({ text: done, error: false });
    } else if (r.kind === 'invalid') {
      setMessage({ text: r.errors.chart ?? 'A pauta não é válida.', error: true });
    } else {
      setMessage({ text: failedMessage(r.kind === 'failed' ? r.title : undefined), error: true });
    }
  };

  const send = async (file: File | undefined) => {
    if (!file) return;
    let parsed: unknown;
    try {
      parsed = JSON.parse(await file.text());
    } catch {
      setMessage({ text: 'O ficheiro não é JSON.', error: true });
      return;
    }
    setBusy(true);
    setMessage(null);
    // A new file keeps the choice already made here.
    const next = { ...(parsed as PlayAlongChart), picking };
    const r = await musicApi.savePlayAlong(songId, next);
    if (r.kind === 'ok') setChart(next);
    finish(r, true, 'Pauta guardada.');
  };

  const remove = async () => {
    setBusy(true);
    setMessage(null);
    finish(await musicApi.deletePlayAlong(songId), false, 'Pauta removida.');
  };

  return (
    <fieldset className="urls playalong-upload">
      <legend>Aprender</legend>
      <p className="form__hint">
        {has ? 'Esta música tem pauta para aprender.' : 'Esta música ainda não tem pauta para aprender.'} A pauta é um
        ficheiro .json feito a partir da cifra e da gravação.
      </p>
      <div className="playalong-upload__actions">
        <label className="btn btn--ghost btn--sm">
          <Icon name="upload" />
          {has ? 'Substituir pauta' : 'Enviar pauta'}
          <input
            type="file"
            accept="application/json,.json"
            className="sr-only"
            disabled={busy}
            onChange={(e) => {
              void send(e.target.files?.[0]);
              e.target.value = '';
            }}
          />
        </label>
        {has && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={remove} disabled={busy}>
            <Icon name="trash" />
            Remover pauta
          </button>
        )}
      </div>
      {has && (
        <CheckField checked={picking} disabled={busy || !chart} onChange={(c) => void togglePicking(c)}>
          Incluir dedilhado (guitarra)
        </CheckField>
      )}
      {message && (
        <p className={message.error ? 'form__error' : 'form__hint'} role={message.error ? 'alert' : 'status'}>
          {message.text}
        </p>
      )}
    </fieldset>
  );
}
