// Task 080: self-check of the startup notification reminder rules (portal/src/startupRules.ts). Run: npm run check:startup
import assert from 'node:assert/strict';
import { build } from 'esbuild';

const out = await build({ entryPoints: [new URL('../src/startupRules.ts', import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1')], bundle: false, write: false, format: 'esm' });
const { reminderFits, reminderDue } = await import(`data:text/javascript,${encodeURIComponent(out.outputFiles[0].text)}`);
const status = { enabled: true, configured: true, reason: null, vapidPublicKey: 'k', optedOut: false, subscriptions: 1 };

// device states: off-ish ones remind; on, unknown and server-side unavailability never do
for (const kind of ['off', 'stale', 'unregistered']) assert.equal(reminderFits({ kind, status, others: 0 }), true, kind);
assert.equal(reminderFits({ kind: 'off', status, others: 2 }), true, 'another device subscribed is not ON here');
assert.equal(reminderFits({ kind: 'denied', status, registered: false, others: 0 }), true, 'denied: settings instructions');
assert.equal(reminderFits({ kind: 'unsupported', installed: false }), true, 'iOS not installed: install instructions');
assert.equal(reminderFits({ kind: 'unsupported', installed: true }), false);
assert.equal(reminderFits({ kind: 'on', status, others: 0 }), false);
for (const kind of ['disabled', 'not-configured', 'no-worker']) assert.equal(reminderFits({ kind, status }), false, kind);
assert.equal(reminderFits({ kind: 'error' }), false, 'unknown is not OFF');

// account: active every opening, whatever was acknowledged; Reformado once
assert.equal(reminderDue({ reformed: false, acknowledged: false }), true);
assert.equal(reminderDue({ reformed: false, acknowledged: true }), true, 'Reformado back to active: active rule');
assert.equal(reminderDue({ reformed: true, acknowledged: false }), true);
assert.equal(reminderDue({ reformed: true, acknowledged: true }), false);
console.log('startup reminder rules: ok');
