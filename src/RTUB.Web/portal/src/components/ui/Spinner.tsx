// The turning busy mark; `small` sits inside a button.
export function Spinner({ small }: { small?: boolean }) {
  return <span className={small ? 'spinner spinner--small' : 'spinner'} aria-hidden="true" />;
}
