// One labelled fact in a `<dl>` (label above, value below); "Não definido" when there is no value.
import type { ReactNode } from 'react';

/** Goes inside a `<dl className="member-fields">`-style list. `null` / `undefined` children show the fallback. */
export function FactRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="member-field">
      <dt>{label}</dt>
      <dd>{children ?? <span className="note">Não definido</span>}</dd>
    </div>
  );
}
