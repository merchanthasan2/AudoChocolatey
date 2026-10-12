import { test } from 'node:test';
import assert from 'node:assert/strict';
import { isValidVersion, readCargoVersion, replaceCargoVersion, findMismatches } from './versions.mjs';

const CARGO = '[package]\nname = "quantum_updater"\nversion = "2.0.0"\n\n[dependencies]\nfoo = { version = "1.2.3" }\n';

test('accepts release and prerelease semver, rejects junk', () => {
  assert.ok(isValidVersion('3.0.0'));
  assert.ok(isValidVersion('3.0.0-alpha.1'));
  assert.ok(!isValidVersion('3.0'));
  assert.ok(!isValidVersion('v3.0.0'));
});

test('reads only the [package] version', () => {
  assert.equal(readCargoVersion(CARGO), '2.0.0');
});

test('reads version from CRLF Cargo.toml', () => {
  assert.equal(readCargoVersion(CARGO.replace(/\n/g, '\r\n')), '2.0.0');
});

test('replaces package version but not dependency versions', () => {
  const out = replaceCargoVersion(CARGO, '3.0.0-alpha.1');
  assert.equal(readCargoVersion(out), '3.0.0-alpha.1');
  assert.match(out, /foo = \{ version = "1\.2\.3" \}/);
});

test('preserves CRLF when replacing', () => {
  const out = replaceCargoVersion(CARGO.replace(/\n/g, '\r\n'), '3.0.0');
  assert.ok(out.includes('version = "3.0.0"\r\n'));
});

test('no mismatches when all files agree', () => {
  assert.deepEqual(findMismatches({ 'package.json': '3.0.0', 'src-tauri/Cargo.toml': '3.0.0' }), []);
});

test('reports a file that disagrees', () => {
  const p = findMismatches({ 'package.json': '3.0.0', 'src-tauri/Cargo.toml': '2.0.0' });
  assert.equal(p.length, 1);
  assert.match(p[0], /Cargo\.toml has 2\.0\.0/);
});

test('accepts matching prerelease tag with v prefix and refs/tags', () => {
  assert.deepEqual(findMismatches({ 'package.json': '3.0.0-alpha.1' }, 'refs/tags/v3.0.0-alpha.1'), []);
});

test('reports tag mismatch', () => {
  assert.equal(findMismatches({ 'package.json': '3.0.0' }, 'v3.0.1').length, 1);
});
