import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { collectVersions, findMismatches } from './versions.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const versions = collectVersions(root);
const problems = findMismatches(versions, process.argv[2]);
if (problems.length) {
  console.error('Version check failed:\n  ' + problems.join('\n  '));
  process.exit(1);
}
console.log(`Versions OK: ${versions['package.json']}`);
