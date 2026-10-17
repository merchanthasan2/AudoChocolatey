// Astral-plane symbols (emoji), misc symbols & dingbats, misc technical, stars, !! and !?, and the emoji variation selector.
const EMOJI = /[\u{10000}-\u{10FFFF}\u{2600}-\u{27BF}\u{2300}-\u{23FF}\u{2B50}-\u{2B55}\u{203C}\u{2049}\u{FE0F}]/u;

// Screens describe apps, not package tools (owner decision 2026-10-13). Settings and the
// details panel's <!-- advanced --> block may name them; code identifiers and comments are fine.
const TOOL = /\b(winget|chocolatey|choco|scoop|msstore)\b/i;
const AS_CODE = /'(winget|choco|scoop|msstore)'|"(winget|choco|scoop|msstore)"|\.(winget|choco|scoop|msstore)\b|\b(winget|choco|scoop|msstore)(?=[A-Z:?])/g;
const SCREENS = /^app\/src\/(views|lib\/components)\/.+\.svelte$/;
// UI logic that builds on-screen text. Tests, the IPC layer and the one tool-name map are exempt.
const LOGIC = /^app\/src\/lib\/.+\.ts$/;
const LOGIC_EXEMPT = /(\.test\.ts$|^app\/src\/lib\/ipc\/|^app\/src\/lib\/logic\/sources\.ts$)/;

export function findViolations(text, file) {
  const out = [];
  const isSvelte = file.endsWith('.svelte');
  const screen = (SCREENS.test(file) && !file.endsWith('/Settings.svelte')) || (LOGIC.test(file) && !LOGIC_EXEMPT.test(file));
  let advanced = false;
  text.split(/\r?\n/).forEach((line, i) => {
    if (EMOJI.test(line)) out.push({ file, line: i + 1, rule: 'emoji', excerpt: line.trim().slice(0, 80) });
    if (isSvelte && line.includes('{@html')) out.push({ file, line: i + 1, rule: 'html', excerpt: line.trim().slice(0, 80) });
    if (line.includes('<!-- advanced -->')) advanced = true;
    if (line.includes('<!-- /advanced -->')) { advanced = false; return; }
    const t = line.trim();
    if (!screen || advanced || t.startsWith('//') || t.startsWith('/*') || t.startsWith('*') || t.startsWith('<!--')) return;
    if (TOOL.test(line.replace(AS_CODE, ''))) out.push({ file, line: i + 1, rule: 'tool-name', excerpt: t.slice(0, 80) });
  });
  return out;
}
