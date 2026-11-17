// Task 080 (#261): who sees the startup notification reminder. Pure, so portal/checks/startup.check.mjs runs it.
import type { PushState } from './push';

/** Device states the reminder is for. Loading, unknown and errors are not "off": no reminder, nothing consumed. */
export function reminderFits(state: PushState): boolean {
  switch (state.kind) {
    case 'off':
    case 'stale':
    case 'unregistered':
    case 'denied':
      return true;
    case 'unsupported':
      return !state.installed; // only the install instructions are useful; an installed app with no push has none
    default:
      return false; // on, server disabled / not configured, no worker, error
  }
}

/** Active: every opening while off. Reformado: once per account, across devices, after an acknowledgement. */
export function reminderDue(account: { reformed: boolean; acknowledged: boolean }): boolean {
  return !account.reformed || !account.acknowledged;
}
