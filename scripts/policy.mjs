// Astral-plane symbols (emoji), misc symbols & dingbats, misc technical, stars, !! and !?, and the emoji variation selector.
const EMOJI = /[\u{10000}-\u{10FFFF}\u{2600}-\u{27BF}\u{2300}-\u{23FF}\u{2B50}-\u{2B55}\u{203C}\u{2049}\u{FE0F}]/u;

export function findViolations(text, file) {
  const out = [];
  const isSvelte = file.endsWith('.svelte');
  text.split(/\r?\n/).forEach((line, i) => {
    if (EMOJI.test(line)) out.push({ file, line: i + 1, rule: 'emoji', excerpt: line.trim().slice(0, 80) });
    if (isSvelte && line.includes('{@html')) out.push({ file, line: i + 1, rule: 'html', excerpt: line.trim().slice(0, 80) });
  });
  return out;
}
