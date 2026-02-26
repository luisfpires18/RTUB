/**
 * Survive Mode — Input Manager.
 *
 * Handles keyboard (WASD/arrows), mouse click-to-move, and touch joystick.
 * Produces a normalised movement vector each frame.
 */
import * as PIXI from 'pixi.js';
import type { JoystickState } from './types';

export interface InputState {
  dx: number;
  dy: number;
}

export class InputManager {
  // Keyboard
  private keys: Record<string, boolean> = {};

  // Mouse
  private touchActive = false;
  private touchTarget = { x: 0, y: 0 };

  // Smoothed output (touch devices)
  private _smoothDx = 0;
  private _smoothDy = 0;

  // Joystick
  private joystick: JoystickState | null = null;
  private joystickActive = false;
  private joystickAngle = 0;
  private joystickMagnitude = 0;
  private _joystickPointerId: number | null = null;
  private _isTouchDevice = false;

  // Viewport info (updated by scene)
  private vpWidth: number;
  private vpHeight: number;
  private camX = 0;
  private camY = 0;

  // Event handler refs
  private _onKeyDown: ((e: KeyboardEvent) => void) | null = null;
  private _onKeyUp: ((e: KeyboardEvent) => void) | null = null;
  private _onPointerDown: ((e: PointerEvent) => void) | null = null;
  private _onPointerMove: ((e: PointerEvent) => void) | null = null;
  private _onPointerUp: ((e: PointerEvent) => void) | null = null;
  private _onPointerCancel: ((e: PointerEvent) => void) | null = null;

  private canvas: HTMLCanvasElement | null = null;

  constructor(vpWidth: number, vpHeight: number) {
    this.vpWidth = vpWidth;
    this.vpHeight = vpHeight;
  }

  /** Update camera position (call each frame before getMovement). */
  setCamera(camX: number, camY: number): void {
    this.camX = camX;
    this.camY = camY;
  }

  setViewport(w: number, h: number): void {
    this.vpWidth = w;
    this.vpHeight = h;
  }

  setup(app: PIXI.Application, uiContainer: PIXI.Container): void {
    this._isTouchDevice = ('ontouchstart' in window) || (navigator.maxTouchPoints > 0);
    this.canvas = app.canvas as HTMLCanvasElement;

    this._onKeyDown = (e: KeyboardEvent) => {
      this.keys[e.key.toLowerCase()] = true;
      e.preventDefault();
    };
    this._onKeyUp = (e: KeyboardEvent) => {
      this.keys[e.key.toLowerCase()] = false;
    };
    window.addEventListener('keydown', this._onKeyDown);
    window.addEventListener('keyup', this._onKeyUp);

    if (this.canvas) {
      this.canvas.style.touchAction = 'none';

      this._onPointerDown = (e: PointerEvent) => {
        const rect = this.canvas!.getBoundingClientRect();
        const scaleX = this.vpWidth / rect.width;
        const scaleY = this.vpHeight / rect.height;
        const localX = (e.clientX - rect.left) * scaleX;
        const localY = (e.clientY - rect.top) * scaleY;

        if (this._isTouchDevice) {
          if (localX < this.vpWidth * 0.55 && this._joystickPointerId === null && this.joystick) {
            this._joystickPointerId = e.pointerId;
            this.joystick.x = localX;
            this.joystick.y = localY;
            this.joystick.bg.position.set(localX, localY);
            this.joystick.knob.position.set(localX, localY);
            this.joystick.bg.alpha = 1;
            this.joystick.knob.alpha = 1;
            this.joystickActive = true;
            this.joystickMagnitude = 0;
          }
        } else {
          this.touchActive = true;
          this.touchTarget.x = localX + this.camX;
          this.touchTarget.y = localY + this.camY;
        }
      };

      this._onPointerMove = (e: PointerEvent) => {
        const rect = this.canvas!.getBoundingClientRect();
        const scaleX = this.vpWidth / rect.width;
        const scaleY = this.vpHeight / rect.height;
        const localX = (e.clientX - rect.left) * scaleX;
        const localY = (e.clientY - rect.top) * scaleY;

        if (this._isTouchDevice) {
          if (this.joystickActive && e.pointerId === this._joystickPointerId) {
            this._updateJoystick(localX, localY);
          }
        } else {
          if (!this.touchActive) return;
          this.touchTarget.x = localX + this.camX;
          this.touchTarget.y = localY + this.camY;
        }
      };

      this._onPointerUp = (e: PointerEvent) => {
        if (this._isTouchDevice) {
          if (e.pointerId === this._joystickPointerId) this._resetJoystick();
        } else {
          this.touchActive = false;
        }
      };

      this._onPointerCancel = (e: PointerEvent) => {
        if (e.pointerId === this._joystickPointerId) this._resetJoystick();
      };

      this.canvas.addEventListener('pointerdown', this._onPointerDown);
      this.canvas.addEventListener('pointermove', this._onPointerMove);
      this.canvas.addEventListener('pointerup', this._onPointerUp);
      this.canvas.addEventListener('pointercancel', this._onPointerCancel);
    }

    this._createJoystickVisuals(uiContainer);
  }

  /** Get smoothed movement direction (frame-rate independent). */
  getMovement(dt: number): InputState {
    let dx = 0;
    let dy = 0;

    if (this.keys['w'] || this.keys['arrowup']) dy -= 1;
    if (this.keys['s'] || this.keys['arrowdown']) dy += 1;
    if (this.keys['a'] || this.keys['arrowleft']) dx -= 1;
    if (this.keys['d'] || this.keys['arrowright']) dx += 1;

    if (this.joystickActive && this.joystickMagnitude > 0.1) {
      // Quadratic curve — finer control at low thumb magnitudes
      const curved = this.joystickMagnitude * this.joystickMagnitude;
      dx = Math.cos(this.joystickAngle) * curved;
      dy = Math.sin(this.joystickAngle) * curved;
    }

    // Clamp to unit length but preserve partial magnitude from joystick
    const mag = Math.sqrt(dx * dx + dy * dy);
    if (mag > 1) { dx /= mag; dy /= mag; }

    // On touch devices, smooth the output for fluid direction changes & deceleration
    if (this._isTouchDevice) {
      const hasInput = dx !== 0 || dy !== 0;
      const speed = hasInput ? 14 : 8; // accelerate faster than decelerate
      const f = 1 - Math.exp(-speed * dt);
      this._smoothDx += (dx - this._smoothDx) * f;
      this._smoothDy += (dy - this._smoothDy) * f;
      // Snap to zero when idle and negligible
      if (!hasInput && Math.abs(this._smoothDx) < 0.01 && Math.abs(this._smoothDy) < 0.01) {
        this._smoothDx = 0;
        this._smoothDy = 0;
      }
      return { dx: this._smoothDx, dy: this._smoothDy };
    }

    return { dx, dy };
  }

  cleanup(): void {
    if (this._onKeyDown) window.removeEventListener('keydown', this._onKeyDown);
    if (this._onKeyUp) window.removeEventListener('keyup', this._onKeyUp);
    if (this.canvas) {
      if (this._onPointerDown) this.canvas.removeEventListener('pointerdown', this._onPointerDown);
      if (this._onPointerMove) this.canvas.removeEventListener('pointermove', this._onPointerMove);
      if (this._onPointerUp) this.canvas.removeEventListener('pointerup', this._onPointerUp);
      if (this._onPointerCancel) this.canvas.removeEventListener('pointercancel', this._onPointerCancel);
    }
    this.keys = {};
    this._smoothDx = 0;
    this._smoothDy = 0;
  }

  // ─── Joystick Internals ─────────────────────────────────────

  private _createJoystickVisuals(uiContainer: PIXI.Container): void {
    const joyRadius = 64;
    const knobRadius = 26;
    const defaultX = 100;
    const defaultY = this.vpHeight - 100;

    const bg = new PIXI.Graphics();
    bg.circle(0, 0, joyRadius);
    bg.fill({ color: 0xffffff, alpha: 0.15 });
    bg.setStrokeStyle({ width: 2, color: 0xffffff, alpha: 0.3 });
    bg.stroke();
    bg.position.set(defaultX, defaultY);
    bg.alpha = 0;
    uiContainer.addChild(bg);

    const knob = new PIXI.Graphics();
    knob.circle(0, 0, knobRadius);
    knob.fill({ color: 0xffffff, alpha: 0.5 });
    knob.position.set(defaultX, defaultY);
    knob.alpha = 0;
    uiContainer.addChild(knob);

    this.joystick = { bg, knob, x: defaultX, y: defaultY, radius: joyRadius, defaultX, defaultY };
    this._joystickPointerId = null;
  }

  private _updateJoystick(localX: number, localY: number): void {
    if (!this.joystick) return;
    const dx = localX - this.joystick.x;
    const dy = localY - this.joystick.y;
    const d = Math.sqrt(dx * dx + dy * dy);
    const maxD = this.joystick.radius;
    const clamped = Math.min(d, maxD);

    this.joystickAngle = Math.atan2(dy, dx);
    this.joystickMagnitude = clamped / maxD;

    this.joystick.knob.position.set(
      this.joystick.x + Math.cos(this.joystickAngle) * clamped,
      this.joystick.y + Math.sin(this.joystickAngle) * clamped,
    );
  }

  private _resetJoystick(): void {
    if (!this.joystick) return;
    this.joystickActive = false;
    this.joystickMagnitude = 0;
    this._joystickPointerId = null;
    this.joystick.bg.alpha = 0;
    this.joystick.knob.alpha = 0;
    this.joystick.x = this.joystick.defaultX;
    this.joystick.y = this.joystick.defaultY;
    this.joystick.bg.position.set(this.joystick.x, this.joystick.y);
    this.joystick.knob.position.set(this.joystick.x, this.joystick.y);
  }
}
