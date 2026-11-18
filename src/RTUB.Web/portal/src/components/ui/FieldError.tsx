// A field's error message under it; renders nothing without text. Announced (role="alert") unless `alert={false}`.
export function FieldError({ text, alert = true }: { text?: string | null; alert?: boolean }) {
  return text ? (
    <p className="form__error" role={alert ? 'alert' : undefined}>
      {text}
    </p>
  ) : null;
}
