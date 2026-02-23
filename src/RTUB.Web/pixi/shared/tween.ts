/**
 * RAF-based tween engine and PIXI.Text object pool.
 * Shared across Arena and Stage scenes.
 */
import type { Container } from 'pixi.js';

type TickerRef = { _rafIds: number[]; battleSpeed?: number };

/**
 * Animate properties of a DisplayObject from current to target over `duration` ms.
 * Uses requestAnimationFrame for smooth animation, independent of the PIXI ticker.
 * Automatically adjusts duration by owner.battleSpeed (like the original JS).
 */
export function animateTo(
  owner: TickerRef,
  target: Container,
  properties: Record<string, number>,
  duration: number,
  onComplete?: () => void,
): void {
  if (!target || (target as unknown as { destroyed?: boolean }).destroyed) {
    onComplete?.();
    return;
  }

  // Adjust animation duration based on battle speed, matching original JS behaviour
  const speed = owner.battleSpeed ?? 1;
  const adjustedDuration = speed > 0 ? duration / speed : duration;

  const startTime = Date.now();
  const container = target as unknown as Record<string, unknown>;

  // Snapshot start values
  const startValues: Record<string, number> = {};
  for (const key of Object.keys(properties)) {
    if (key === 'scale') {
      startValues[key] = target.scale?.x ?? 1;
    } else {
      startValues[key] = (container[key] as number) ?? 0;
    }
  }

  const animate = (): void => {
    const elapsed = Date.now() - startTime;
    const t = Math.min(elapsed / adjustedDuration, 1);

    try {
      for (const key of Object.keys(properties)) {
        const from = startValues[key];
        const to = properties[key];
        const value = from + (to - from) * t;
        if (key === 'scale') {
          target.scale?.set(value);
        } else if (key === 'width' || key === 'height') {
          (container[key] as number) = value;
        } else {
          (container[key] as number) = value;
        }
      }
    } catch {
      onComplete?.();
      return;
    }

    if (t < 1) {
      const id = requestAnimationFrame(animate);
      owner._rafIds.push(id);
    } else {
      onComplete?.();
    }
  };

  const id = requestAnimationFrame(animate);
  owner._rafIds.push(id);
}

/**
 * Convenience: fade a target to alpha=0.
 */
export function fadeOut(
  owner: TickerRef,
  target: Container,
  duration: number,
  onComplete?: () => void,
): void {
  animateTo(owner, target, { alpha: 0 }, duration, onComplete);
}
