import { readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const SEMVER = /^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$/;
const PKG_VERSION_LINE = /^version\s*=\s*"([^"]+)"/m;

export function isValidVersion(v) {
  return typeof v === 'string' && SEMVER.test(v);
}

// The [package] table is the first table in Cargo.toml; it ends at the next line starting with "[".
function splitPackageSection(text) {
  const idx = text.search(/\r?\n\[/);
  return idx === -1 ? [text, ''] : [text.slice(0, idx), text.slice(idx)];
}

export function readCargoVersion(text) {
  const [head] = splitPackageSection(text);
  const m = head.match(PKG_VERSION_LINE);
  return m ? m[1] : null;
}

export function replaceCargoVersion(text, version) {
  const [head, tail] = splitPackageSection(text);
  if (!PKG_VERSION_LINE.test(head)) throw new Error('Cargo.toml [package] has no version line');
  return head.replace(PKG_VERSION_LINE, `version = "${version}"`) + tail;
}

export function collectVersions(root) {
  const json = (p) => JSON.parse(readFileSync(join(root, p), 'utf8')).version;
  const out = {
    'package.json': json('package.json'),
    'src-tauri/Cargo.toml': readCargoVersion(readFileSync(join(root, 'src-tauri/Cargo.toml'), 'utf8')),
  };
  if (existsSync(join(root, 'app/package.json'))) out['app/package.json'] = json('app/package.json');
  return out;
}

export function findMismatches(versions, tag) {
  const expected = versions['package.json'];
  const problems = [];
  if (!isValidVersion(expected)) problems.push(`package.json version "${expected}" is not valid semver`);
  for (const [file, v] of Object.entries(versions)) {
    if (v !== expected) problems.push(`${file} has ${v}, expected ${expected}`);
  }
  if (tag !== undefined) {
    const t = tag.replace(/^refs\/tags\//, '').replace(/^v/, '');
    if (t !== expected) problems.push(`tag ${tag} does not match ${expected}`);
  }
  return problems;
}
