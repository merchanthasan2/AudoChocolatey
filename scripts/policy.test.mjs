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

test('flags package-tool names in screen text', () => {
  const v = findViolations('<p class="muted">One place for winget and Chocolatey.</p>', 'app/src/views/Marketplace.svelte');
  assert.equal(v.length, 1);
  assert.equal(v[0].rule, 'tool-name');
  assert.equal(findViolations('<span>Needs Chocolatey</span>', 'app/src/lib/components/AppCard.svelte').length, 1);
});

test('allows tool names used as code, comments, Settings and the Advanced block', () => {
  const code = "{@const id = e.best === 'winget' ? e.winget : e.choco}\n// winget ids are case-insensitive";
  assert.deepEqual(findViolations(code, 'app/src/views/Marketplace.svelte'), []);
  assert.deepEqual(findViolations('<span>Chocolatey</span>', 'app/src/views/Settings.svelte'), []);
  const adv = '<!-- advanced -->\n<span>Installs with winget</span>\n<!-- /advanced -->\n<span>winget</span>';
  const v = findViolations(adv, 'app/src/lib/components/AppDetail.svelte');
  assert.equal(v.length, 1);
  assert.equal(v[0].line, 4);
  assert.deepEqual(findViolations("const label = 'Chocolatey';", 'app/src/lib/logic/sources.ts'), []);
});

test('UI logic files are checked too, except tests, IPC and the source-name map', () => {
  assert.equal(findViolations("const t = 'Chocolatey failed';", 'app/src/lib/logic/x.ts').length, 1);
  assert.deepEqual(findViolations("type S = 'winget';\nconst y = x.choco;\ntype I = { msstore?: string };", 'app/src/lib/logic/x.ts'), []);
  assert.deepEqual(findViolations("const t = 'Chocolatey failed';", 'app/src/lib/logic/sources.ts'), []);
  assert.deepEqual(findViolations("const t = 'Chocolatey failed';", 'app/src/lib/logic/x.test.ts'), []);
  assert.deepEqual(findViolations("const t = 'Chocolatey failed';", 'app/src/lib/ipc/commands.ts'), []);
});
