import { useEffect, useRef, useState, type KeyboardEvent, type PointerEvent } from 'react';
import { Dialog } from './MusicUi';

const VIEW = 600; // canvas units of the on-screen square
const OUT = 800; // pixels of the saved cover

type Frame = { img: HTMLImageElement; zoom: number; x: number; y: number };

/**
 * Square (1:1) crop for album covers, the only way a new cover reaches the form. Canvas only: the
 * picked file is read as a data: URL (CSP allows data: images, not blob:), panned by drag or the
 * arrow keys, zoomed with a slider, and exported as WebP (JPEG where the browser cannot encode WebP).
 */
export function Cropper({ file, onDone, onCancel }: { file: File; onDone: (cover: Blob, preview: string) => void; onCancel: () => void }) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const drag = useRef<{ x: number; y: number } | null>(null);
  const [frame, setFrame] = useState<Frame | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    const reader = new FileReader();
    reader.onload = () => {
      const img = new Image();
      img.onload = () => setFrame(center({ img, zoom: 1, x: 0, y: 0 }));
      img.onerror = () => setFailed(true);
      img.src = String(reader.result);
    };
    reader.onerror = () => setFailed(true);
    reader.readAsDataURL(file);
    return () => reader.abort();
  }, [file]);

  useEffect(() => {
    const ctx = canvas.current?.getContext('2d');
    if (ctx && frame) draw(ctx, frame, VIEW);
  }, [frame]);

  const move = (dx: number, dy: number) => setFrame((f) => (f ? clamp({ ...f, x: f.x + dx, y: f.y + dy }) : f));

  const onPointerDown = (e: PointerEvent<HTMLCanvasElement>) => {
    e.currentTarget.setPointerCapture(e.pointerId);
    drag.current = { x: e.clientX, y: e.clientY };
  };
  const onPointerMove = (e: PointerEvent<HTMLCanvasElement>) => {
    if (!drag.current) return;
    const ratio = VIEW / e.currentTarget.getBoundingClientRect().width;
    move((e.clientX - drag.current.x) * ratio, (e.clientY - drag.current.y) * ratio);
    drag.current = { x: e.clientX, y: e.clientY };
  };
  const onKeyDown = (e: KeyboardEvent<HTMLCanvasElement>) => {
    const step = e.shiftKey ? 40 : 10;
    const keys: Record<string, [number, number]> = { ArrowLeft: [step, 0], ArrowRight: [-step, 0], ArrowUp: [0, step], ArrowDown: [0, -step] };
    if (keys[e.key]) {
      e.preventDefault();
      move(...keys[e.key]);
    }
  };

  const save = () => {
    if (!frame) return;
    const out = document.createElement('canvas');
    out.width = OUT;
    out.height = OUT;
    const ctx = out.getContext('2d');
    if (!ctx) return;
    draw(ctx, frame, OUT);
    const finish = (blob: Blob | null) => {
      if (blob) onDone(blob, out.toDataURL(blob.type));
      else setFailed(true);
    };
    out.toBlob((webp) => {
      if (webp?.type === 'image/webp') finish(webp);
      else out.toBlob(finish, 'image/jpeg', 0.9);
    }, 'image/webp', 0.85);
  };

  return (
    <Dialog
      title="Recortar a capa"
      onClose={onCancel}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onCancel}>
            Cancelar
          </button>
          <button type="button" className="btn btn--primary" onClick={save} disabled={!frame}>
            Usar este recorte
          </button>
        </>
      }
    >
      {failed ? (
        <p className="form__banner" role="alert">
          Não foi possível ler esta imagem. Escolha outro ficheiro.
        </p>
      ) : (
        <div className="cropper">
          <canvas
            ref={canvas}
            className="cropper__canvas"
            width={VIEW}
            height={VIEW}
            tabIndex={0}
            role="img"
            aria-label="Pré-visualização da capa quadrada. Arraste ou use as setas para enquadrar."
            onPointerDown={onPointerDown}
            onPointerMove={onPointerMove}
            onPointerUp={() => (drag.current = null)}
            onPointerCancel={() => (drag.current = null)}
            onKeyDown={onKeyDown}
          />
          <label className="cropper__zoom">
            Zoom
            <input
              type="range"
              min={1}
              max={3}
              step={0.01}
              value={frame?.zoom ?? 1}
              disabled={!frame}
              onChange={(e) => setFrame((f) => (f ? zoomTo(f, Number(e.target.value)) : f))}
            />
          </label>
          <p className="note">As capas são sempre quadradas. Arraste a imagem para escolher o enquadramento.</p>
        </div>
      )}
    </Dialog>
  );
}

/** The image scale at zoom 1: it just covers the square. */
const base = (img: HTMLImageElement) => Math.max(VIEW / img.naturalWidth, VIEW / img.naturalHeight);

function clamp(f: Frame): Frame {
  const s = base(f.img) * f.zoom;
  const minX = VIEW - f.img.naturalWidth * s;
  const minY = VIEW - f.img.naturalHeight * s;
  return { ...f, x: Math.min(0, Math.max(minX, f.x)), y: Math.min(0, Math.max(minY, f.y)) };
}

function center(f: Frame): Frame {
  const s = base(f.img) * f.zoom;
  return clamp({ ...f, x: (VIEW - f.img.naturalWidth * s) / 2, y: (VIEW - f.img.naturalHeight * s) / 2 });
}

/** Zoom around the middle of the square, keeping what is there in place. */
function zoomTo(f: Frame, zoom: number): Frame {
  const ratio = zoom / f.zoom;
  const mid = VIEW / 2;
  return clamp({ ...f, zoom, x: mid - (mid - f.x) * ratio, y: mid - (mid - f.y) * ratio });
}

function draw(ctx: CanvasRenderingContext2D, f: Frame, size: number) {
  const k = size / VIEW;
  const s = base(f.img) * f.zoom * k;
  ctx.fillStyle = '#000';
  ctx.fillRect(0, 0, size, size);
  ctx.imageSmoothingQuality = 'high';
  ctx.drawImage(f.img, f.x * k, f.y * k, f.img.naturalWidth * s, f.img.naturalHeight * s);
}
