// A form-level error line (red banner), announced; renders nothing without text.
export function Banner({ text }: { text?: string | null }) {
  return text ? (
    <p className="form__banner" role="alert">
      {text}
    </p>
  ) : null;
}
