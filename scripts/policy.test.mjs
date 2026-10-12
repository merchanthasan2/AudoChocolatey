import { test } from 'node:test';
import assert from 'node:assert/strict';
import { findViolations } from './policy.mjs';

test('flags emoji with file and line', () => {
  const v = findViolations('ok\nconst s = "done \u{1F680}";\n', 'app/src/a.ts');
  assert.equal(v.length, 1);
  assert.equal(v[0].line, 2);
  assert.equal(v[0].rule, 'emoji');
});

test('flags dingbat check marks', () => {
  assert.equal(findViolations('<span>\u2713</span>', 'app/src/A.svelte').length, 1);
});

test('allows typographic characters', () => {
  assert.deepEqual(findViolations('1.94 \u2192 1.95 \u2026 \u00B7 \u00A9 2026 \u2013 ok', 'app/src/A.svelte'), []);
});

test('flags {@html} in svelte files', () => {
  const v = findViolations('<p>{@html desc}</p>', 'app/src/views/AppDetail.svelte');
  assert.equal(v.length, 1);
  assert.equal(v[0].rule, 'html');
});

test('ignores {@html} text outside svelte files', () => {
  assert.deepEqual(findViolations('// never use {@html}', 'app/src/lib/x.ts'), []);
});
