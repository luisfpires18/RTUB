// A small action menu drawn in fixed position.
import { useLayoutEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react';
import { Icon } from './icons';

/** A small action menu, drawn in fixed position so the board's horizontal scroll never clips it. */
export function Menu({ label, children }: { label: string; children: (close: () => void) => ReactNode }) {
  const [at, setAt] = useState<CSSProperties>();
  const button = useRef<HTMLButtonElement>(null);
  const list = useRef<HTMLDivElement>(null);

  // Below the button (above it near the bottom of the screen); none once the button scrolls out of sight.
  const place = (): CSSProperties | undefined => {
    const r = button.current?.getBoundingClientRect();
    if (!r || r.bottom < 0 || r.top > window.innerHeight || r.right < 0 || r.left > window.innerWidth) return undefined;
    const width = 220;
    const left = Math.max(8, Math.min(r.right - width, window.innerWidth - width - 8));
    return window.innerHeight - r.bottom > 260 ? { left, top: r.bottom + 4, width } : { left, bottom: window.innerHeight - r.top + 4, width };
  };
  const open = !!at;

  useLayoutEffect(() => {
    if (!open) return;
    const close = (e: Event) => {
      if (e.type === 'keydown' && (e as KeyboardEvent).key !== 'Escape') return;
      if (e.type === 'pointerdown' && (list.current?.contains(e.target as Node) || button.current?.contains(e.target as Node))) return;
      setAt(undefined);
    };
    // The board scrolls sideways (and snaps): the menu follows its button instead of vanishing.
    const follow = () => setAt(place());
    document.addEventListener('pointerdown', close);
    document.addEventListener('keydown', close);
    window.addEventListener('scroll', follow, true);
    window.addEventListener('resize', follow);
    list.current?.querySelector<HTMLElement>('button')?.focus({ preventScroll: true });
    return () => {
      document.removeEventListener('pointerdown', close);
      document.removeEventListener('keydown', close);
      window.removeEventListener('scroll', follow, true);
      window.removeEventListener('resize', follow);
    };
  }, [open]);

  const toggle = () => setAt(at ? undefined : place());

  return (
    <>
      <button
        ref={button}
        type="button"
        className="icon-btn icon-btn--sm lx-menu__button"
        aria-haspopup="menu"
        aria-expanded={!!at}
        onClick={(e) => {
          e.stopPropagation();
          toggle();
        }}
      >
        <Icon name="menu" />
        <span className="sr-only">{label}</span>
      </button>
      {at && (
        <div ref={list} className="lx-menu" role="menu" style={at} onClick={(e) => e.stopPropagation()}>
          {children(() => setAt(undefined))}
        </div>
      )}
    </>
  );
}
