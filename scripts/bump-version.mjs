import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { isValidVersion, replaceCargoVersion } from './versions.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const version = process.argv[2];
if (!isValidVersion(version)) {
  console.error('Usage: node scripts/bump-version.mjs <semver>, e.g. 3.0.0-alpha.2');
  process.exit(1);
}
for (const p of ['package.json', 'app/package.json']) {
  const file = join(root, p);
  if (!existsSync(file)) continue;
  const pkg = JSON.parse(readFileSync(file, 'utf8'));
  pkg.version = version;
  writeFileSync(file, JSON.stringify(pkg, null, 2) + '\n');
}
const cargo = join(root, 'src-tauri/Cargo.toml');
writeFileSync(cargo, replaceCargoVersion(readFileSync(cargo, 'utf8'), version));
console.log(`Bumped to ${version}. Run "cargo check" in src-tauri to refresh Cargo.lock.`);
