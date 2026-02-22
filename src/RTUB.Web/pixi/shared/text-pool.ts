/**
 * PIXI.Text object pool — avoids creating/destroying Text objects every frame.
 * Shared between Arena and Stage scenes.
 */
import type { Text, TextStyle } from 'pixi.js';

const MAX_POOL_SIZE = 20;

export interface TextPool {
  pool: Text[];
}

/** Get a Text from the pool or create a new one. */
export function getPooledText(
  textPool: TextPool,
  text: string,
  style: Partial<TextStyle>,
): Text | null {
  try {
    let t = textPool.pool.pop();

    if (t) {
      // Re-check if the pooled text was destroyed (e.g., GL context loss)
      if ((t as unknown as { destroyed?: boolean }).destroyed) {
        t = undefined;
      }
    }

    if (t) {
      t.text = text;
      // Apply style properties
      if (t.style) {
        Object.assign(t.style, style);
      }
      t.alpha = 1;
      t.scale.set(1);
      t.visible = true;
    } else {
      t = new PIXI.Text({ text, style: style as TextStyle });
    }
    return t;
  } catch {
    // GL context may be lost — create a fresh one
    try {
      return new PIXI.Text({ text, style: style as TextStyle });
    } catch {
      return null;
    }
  }
}

/** Return a Text to the pool for later reuse. */
export function releaseText(textPool: TextPool, t: Text): void {
  if (!t) return;
  t.visible = false;
  if (t.parent) t.parent.removeChild(t);
  if (textPool.pool.length < MAX_POOL_SIZE) {
    textPool.pool.push(t);
  } else {
    try { t.destroy(); } catch { /* ignore */ }
  }
}

/** Destroy all pooled texts (cleanup). */
export function destroyTextPool(textPool: TextPool): void {
  for (const t of textPool.pool) {
    try { t.destroy(); } catch { /* ignore */ }
  }
  textPool.pool = [];
}
