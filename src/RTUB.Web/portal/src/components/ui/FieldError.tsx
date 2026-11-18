// A field's error message under it, announced; renders nothing without text.
export function FieldError({ text }: { text?: string }) {
  return text ? (
    <p className="form__error" role="alert">
      {text}
    </p>
  ) : null;
}
