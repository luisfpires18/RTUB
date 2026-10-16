import { lazy, Suspense, type ComponentProps, type ComponentType, type ReactNode } from 'react';
import { Loading } from './App';
import { Dialog } from './Dialog';

/**
 * The Admin / Owner event management UI (`EventManage`, about half of the old Events chunk) loads only when one of its
 * dialogs is opened (#226 F13): visitors and members never download it. Same components, same props; while the chunk
 * arrives the dialog opens with a spinner. The server still decides every permission; this only defers the code.
 */
type Module = typeof import('./EventManage');
const load = () => import('./EventManage');

function deferred<K extends keyof Module>(name: K, fallback: (props: ComponentProps<Module[K]>) => ReactNode) {
  type Props = ComponentProps<Module[K]>;
  // The props are typed at the boundary (Props); inside, the picked export is any of the module's components.
  const Lazy = lazy(() => load().then((m) => ({ default: m[name] as ComponentType<object> })));
  return (props: Props) => (
    <Suspense fallback={fallback(props)}>
      <Lazy {...(props as object)} />
    </Suspense>
  );
}

const opening = ({ onClose }: { onClose: () => void }) => (
  <Dialog title="A abrir…" onClose={onClose}>
    <Loading label="A abrir…" />
  </Dialog>
);

export const EventFormDialog = deferred('EventFormDialog', opening);
export const DeleteEventDialog = deferred('DeleteEventDialog', opening);
export const CancelEventDialog = deferred('CancelEventDialog', opening);
export const ReactivateEventDialog = deferred('ReactivateEventDialog', opening);
export const NoticeDialog = deferred('NoticeDialog', opening);
export const ParticipantsManagerDialog = deferred('ParticipantsManagerDialog', opening);
export const RepertoireManagerDialog = deferred('RepertoireManagerDialog', opening);
export const VideoManagerDialog = deferred('VideoManagerDialog', opening);
export const PrizeManager = deferred('PrizeManager', () => <Loading label="A abrir a gestão de prémios…" />);
