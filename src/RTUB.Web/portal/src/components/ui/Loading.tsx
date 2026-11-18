// "A carregar…" block: a big spinner and a line, announced to screen readers.
import { Spinner } from './Spinner';

export function Loading({ label }: { label: string }) {
  return (
    <div className="state" role="status">
      <Spinner />
      <p>{label}</p>
    </div>
  );
}
