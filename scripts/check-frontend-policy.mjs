import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, relative, extname } from 'node:path';
import { findViolations } from './policy.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const EXTS = new Set(['.svelte', '.ts', '.js', '.css', '.html', '.rs', '.json']);
const TARGETS = ['app/src', 'app/index.html', 'src-tauri/src'];

function* walk(p) {
  if (!existsSync(p)) return;
  if (statSync(p).isFile()) { yield p; return; }
  for (const name of readdirSync(p)) yield* walk(join(p, name));
}

const violations = [];
for (const t of TARGETS) {
  for (const file of walk(join(root, t))) {
    if (!EXTS.has(extname(file))) continue;
    const rel = relative(root, file).replace(/\\/g, '/');
    violations.push(...findViolations(readFileSync(file, 'utf8'), rel));
  }
}
for (const v of violations) console.error(`${v.file}:${v.line} [${v.rule}] ${v.excerpt}`);
if (violations.length) { console.error(`${violations.length} policy violation(s).`); process.exit(1); }
console.log('Frontend policy OK: no emoji, no {@html}, no package-tool names on screens.');
