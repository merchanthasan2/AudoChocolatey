// Checks every id in src-tauri/catalog/apps.json against the live sources:
//   winget: `winget show --id <id> --exact --source winget` must find it
//   choco:  community feed FindPackagesById must return it
// Run manually (needs winget + internet): node scripts/validate-catalog.mjs
import { readFileSync } from 'node:fs';
import { execFile } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const catalog = JSON.parse(readFileSync(join(root, 'src-tauri/catalog/apps.json'), 'utf8'));
const winget = join(process.env.LOCALAPPDATA ?? '', 'Microsoft', 'WindowsApps', 'winget.exe');

const wingetOk = (id) =>
  new Promise((res) =>
    execFile(winget, ['show', '--id', id, '--exact', '--source', 'winget', '--accept-source-agreements', '--disable-interactivity'],
      { timeout: 60000, windowsHide: true },
      (err, stdout) => res(!err && /^Found /m.test(stdout))),
  );

const chocoOk = async (id) => {
  const filter = encodeURIComponent(`tolower(Id) eq '${id.toLowerCase()}' and IsLatestVersion`).replace(/'/g, '%27');
  const url = `https://community.chocolatey.org/api/v2/Packages()?$filter=${filter}&$top=1`;
  try {
    const xml = await (await fetch(url)).text();
    return /<entry>/.test(xml);
  } catch {
    return false;
  }
};

async function pool(items, n, fn) {
  const out = [];
  let i = 0;
  await Promise.all(Array.from({ length: n }, async () => { while (i < items.length) { const k = i++; out[k] = await fn(items[k]); } }));
  return out;
}

const results = await pool(catalog.apps, 4, async (a) => ({
  key: a.key,
  // winget can briefly fail under parallel use (source lock); retry once before reporting.
  winget: a.winget ? (await wingetOk(a.winget)) || (await wingetOk(a.winget)) : null,
  choco: a.choco ? await chocoOk(a.choco) : null,
  a,
}));
let bad = 0;
for (const r of results) {
  const problems = [];
  if (r.winget === false) problems.push(`winget id ${r.a.winget} not found`);
  if (r.choco === false) problems.push(`choco id ${r.a.choco} not found`);
  if (problems.length) { bad++; console.log(`${r.key}: ${problems.join('; ')}`); }
}
console.log(`${catalog.apps.length} apps checked, ${bad} with problems.`);
process.exit(bad ? 1 : 0);
