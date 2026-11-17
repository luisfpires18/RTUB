import { useEffect, useRef, useState, type KeyboardEvent, type PointerEvent } from 'react';
import { Dialog } from './MusicUi';

const VIEW = 600; // canvas units of the on-screen frame's width

type Frame = { img: HTMLImageElement; zoom: number; x: number; y: number; h: number };

const SQUARE = {
  title: 'Recortar a capa',
  label: 'Pré-visualização da capa quadrada. Arraste ou use as setas para enquadrar.',
  note: 'As capas são sempre quadradas. Arraste a imagem para escolher o enquadramento.',
};

/**
 * Fixed-ratio crop: square (1:1) album covers, 3:2 event images (as the old event cropper). Canvas
 * only: the picked file is read as a data: URL (CSP allows data: images, not blob:), panned by drag or
 * the arrow keys, zoomed with a slider, and exported as WebP (JPEG where the browser cannot encode WebP).
 */
export function Cropper({
  file,
  onDone,
  onCancel,
  aspect = 1,
  outWidth = 800,
  text = SQUARE,
}: {
  file: File;
  onDone: (cover: Blob, preview: string) => void;
  onCancel: () => void;
  aspect?: number;
  outWidth?: number;
  text?: typeof SQUARE;
}) {
  const viewH = Math.round(VIEW / aspect);
  const canvas = useRef<HTMLCanvasElement>(null);
  const drag = useRef<{ x: number; y: number } | null>(null);
  const [frame, setFrame] = useState<Frame | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    const reader = new FileReader();
    reader.onload = () => {
      const img = new Image();
      img.onload = () => setFrame(center({ img, zoom: 1, x: 0, y: 0, h: viewH }));
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
    out.width = outWidth;
    out.height = Math.round(outWidth / aspect);
    const ctx = out.getContext('2d');
    if (!ctx) return;
    draw(ctx, frame, outWidth);
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
      title={text.title}
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
            height={viewH}
            tabIndex={0}
            role="img"
            aria-label={text.label}
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
          <p className="note">{text.note}</p>
        </div>
      )}
    </Dialog>
  );
}

/** The image scale at zoom 1: it just covers the frame. */
const base = (f: Frame) => Math.max(VIEW / f.img.naturalWidth, f.h / f.img.naturalHeight);

function clamp(f: Frame): Frame {
  const s = base(f) * f.zoom;
  const minX = VIEW - f.img.naturalWidth * s;
  const minY = f.h - f.img.naturalHeight * s;
  return { ...f, x: Math.min(0, Math.max(minX, f.x)), y: Math.min(0, Math.max(minY, f.y)) };
}

function center(f: Frame): Frame {
  const s = base(f) * f.zoom;
  return clamp({ ...f, x: (VIEW - f.img.naturalWidth * s) / 2, y: (f.h - f.img.naturalHeight * s) / 2 });
}

/** Zoom around the middle of the frame, keeping what is there in place. */
function zoomTo(f: Frame, zoom: number): Frame {
  const ratio = zoom / f.zoom;
  const midX = VIEW / 2;
  const midY = f.h / 2;
  return clamp({ ...f, zoom, x: midX - (midX - f.x) * ratio, y: midY - (midY - f.y) * ratio });
}

function draw(ctx: CanvasRenderingContext2D, f: Frame, width: number) {
  const k = width / VIEW;
  const s = base(f) * f.zoom * k;
  ctx.fillStyle = '#000';
  ctx.fillRect(0, 0, width, f.h * k);
  ctx.imageSmoothingQuality = 'high';
  ctx.drawImage(f.img, f.x * k, f.y * k, f.img.naturalWidth * s, f.img.naturalHeight * s);
}
