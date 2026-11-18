// Self-check of the pickers `useLoad` keeps an answer with (portal/src/data/apiCall.ts). Run: npm run check:load
import assert from 'node:assert/strict';
import { build } from 'esbuild';

const out = await build({ entryPoints: [new URL('../src/data/apiCall.ts', import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1')], bundle: false, write: false, format: 'esm' });
const { orNull, orAccess } = await import(`data:text/javascript,${encodeURIComponent(out.outputFiles[0].text)}`);

const data = { a: 1 };
const failures = [{ kind: 'invalid', errors: {} }, { kind: 'signin' }, { kind: 'closed' }, { kind: 'notfound' }, { kind: 'forbidden' }, { kind: 'inuse' }, { kind: 'conflict' }, { kind: 'failed' }];

assert.equal(orNull({ kind: 'ok', data }), data);
assert.equal(orAccess({ kind: 'ok', data }), data);
for (const o of failures) {
  assert.equal(orNull(o), null, o.kind);
  assert.equal(orAccess(o), o.kind === 'signin' ? 'signin' : o.kind === 'forbidden' ? 'forbidden' : null, o.kind);
}
console.log('load pickers: ok');
