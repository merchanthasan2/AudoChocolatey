/*
 * Quantum Updater hardware illustration library (ADR 009).
 *
 * Plain script, no build step. Works in a browser (<script src="parts.js">) and in Node (require).
 * Everything is inline SVG, 56 x 56 viewBox, drawn at 2 px strokes with rounded caps.
 * No brand logos are copied: vendor identity comes from the shape of the package or card
 * plus a short text label drawn on the one accent-filled plate.
 *
 * COLOURS (set these on any ancestor; each has a currentColor fallback)
 *   --art-line    outline colour (and the dimmed detail lines, drawn at 55% opacity)
 *   --art-accent  highlights: gold contacts, rings, LEDs, corner marks
 *   --art-fill    the one solid accent fill (die, plate, chips)
 *   --art-ink     colour of text drawn ON --art-fill. Optional, defaults to #073A4B.
 *                 It must keep at least 4.5:1 against --art-fill (mint #06D7A0 + #073A4B is 6.55:1).
 *
 * EXPORTS (window.* in the browser, module.exports in Node)
 *   HARDWARE_ART   { key: '<svg ...>' }  one default drawing per key (sample text where a slot exists)
 *   HARDWARE_META  { key: { kind, label } }  human label for tooltips and the gallery
 *   HARDWARE_KINDS ordered list of kinds
 *   HARDWARE_FALLBACK { kind: genericKey }
 *   pickArt(kind, facts)    -> key (always a key that exists in HARDWARE_ART)
 *   renderArt(kind, facts)  -> { key, label, svg }  svg carries the REAL family/generation text for processors
 *   makeCpuArt({vendor, family, generation, ai, shape}) -> svg string
 *   parseCpuName(name)      -> { vendor, family, generation, key, ai }
 *   parseGpuName(name)      -> { key }
 *
 * makeCpuArt TEMPLATE
 *   vendor      'intel' | 'amd' | 'qualcomm' | 'arm' | 'apple' | 'generic'
 *   family      text on the first line of the die,  e.g. 'Core i7', 'Ryzen 7', 'Ultra 7', 'X Elite', 'M2'
 *   generation  text on the second line, optional,   e.g. '13th Gen', '4000', 'Series 2', 'Pro'
 *   ai          true draws the small sparkle mark (Ryzen AI). Detected from family when omitted.
 *   shape       optional override: intel | intel-ultra | amd | amd-tr | athlon | qualcomm | arm | apple | generic
 *   The package drawing is chosen from vendor + family (Core Ultra gets a split two-tile die, Threadripper gets
 *   a wide package, Athlon gets fewer pins). Text is fitted to the die, never smaller than 8 px at the 56 px size.
 */
(function (root) {
  'use strict';

  var FONT = "'Segoe UI Variable','Segoe UI',Tahoma,Arial,sans-serif";
  var ST = 'stroke-width:2;stroke-linecap:round;stroke-linejoin:round';
  var STY = {
    l: 'fill:none;stroke:var(--art-line,currentColor);' + ST,
    d: 'fill:none;stroke:var(--art-line,currentColor);stroke-opacity:.55;' + ST,
    m: 'fill:none;stroke:var(--art-line,currentColor);stroke-opacity:.55;stroke-dasharray:2.5 3;' + ST,
    a: 'fill:none;stroke:var(--art-accent,currentColor);' + ST,
    f: 'fill:var(--art-fill,currentColor);stroke:var(--art-fill,currentColor);stroke-width:2;stroke-linejoin:round',
    k: 'fill:var(--art-accent,currentColor);stroke:none',
    ink: 'fill:var(--art-ink,#073A4B);stroke:none'
  };

  function esc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;'); }
  function n(v) { return Math.round(v * 100) / 100; }
  function P(d, r) { return '<path d="' + d + '" style="' + STY[r] + '"/>'; }
  function R(x, y, w, h, rx, r) { return '<rect x="' + x + '" y="' + y + '" width="' + w + '" height="' + h + '" rx="' + (rx || 0) + '" style="' + STY[r] + '"/>'; }
  function C(cx, cy, rad, r) { return '<circle cx="' + cx + '" cy="' + cy + '" r="' + rad + '" style="' + STY[r] + '"/>'; }
  function dot(x, y, r) { return P('M' + x + ' ' + y + 'h.01', r); }
  function svg(inner, label) {
    return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 56 56" width="56" height="56" role="img" aria-label="' + esc(label || '') + '" focusable="false">' + inner + '</svg>';
  }

  /* ---------- text fitted onto the accent fill ---------- */
  function fit(str, maxW, maxSize, minSize) {
    var nat = str.length * 0.6;                       // conservative em width per character, bold
    var size = Math.min(maxSize, maxW / nat), tl = null;
    if (size < minSize) { size = minSize; if (nat * size > maxW) tl = maxW; }
    return { size: size, tl: tl };
  }
  function T(str, cx, y, maxW, maxSize, minSize) {
    var f = fit(str, maxW, maxSize, minSize || 8);
    return '<text x="' + cx + '" y="' + n(y) + '" text-anchor="middle"' +
      (f.tl ? ' textLength="' + n(f.tl) + '" lengthAdjust="spacingAndGlyphs"' : '') +
      ' style="font:700 ' + n(f.size) + 'px ' + FONT + ';fill:var(--art-ink,#073A4B)">' + esc(str) + '</text>';
  }
  function plate(x, y, w, h, label, max) {
    var f = fit(label, w - 4, max || 11, 8);
    return R(x, y, w, h, 2.5, 'f') + T(label, x + w / 2, y + h / 2 + f.size * 0.35, w - 4, max || 11, 8);
  }

  /* ---------- processors ---------- */
  function cpuShape(vendor, fam) {
    if (vendor === 'intel') return /ultra/i.test(fam) ? 'intel-ultra' : 'intel';
    if (vendor === 'amd') return /thread|\bTR\b/i.test(fam) ? 'amd-tr' : (/athlon/i.test(fam) ? 'athlon' : 'amd');
    if (vendor === 'qualcomm') return 'qualcomm';
    if (vendor === 'arm') return 'arm';
    if (vendor === 'apple') return 'apple';
    return 'generic';
  }
  function pinsTB(xs, yTop, yBot, edgeTop, edgeBot) {
    return xs.map(function (x) { return 'M' + x + ' ' + yTop + 'V' + edgeTop + 'M' + x + ' ' + edgeBot + 'V' + yBot; }).join('');
  }
  function pinsLR(ys, xL, xR, edgeL, edgeR) {
    return ys.map(function (y) { return 'M' + xL + ' ' + y + 'H' + edgeL + 'M' + edgeR + ' ' + y + 'H' + xR; }).join('');
  }
  function makeCpuArt(o) {
    o = o || {};
    var vendor = String(o.vendor || 'generic').toLowerCase();
    var fam = o.family == null ? '' : String(o.family);
    var gen = o.generation == null ? '' : String(o.generation);
    var shape = o.shape || cpuShape(vendor, fam);
    var ai = o.ai != null ? !!o.ai : (vendor === 'amd' && /\bAI\b/.test(fam));
    var body = '', D = { x: 10, y: 10, w: 36, h: 36 }, split = false;

    if (shape === 'intel') {
      body = P('M3 15L15 3H50Q53 3 53 6V50Q53 53 50 53H6Q3 53 3 50Z', 'l') + P('M6.5 17L17 6.5H47Q49.5 6.5 49.5 9V47Q49.5 49.5 47 49.5H9Q6.5 49.5 6.5 47Z', 'd');
      D = { x: 9, y: 11, w: 38, h: 34 };
    } else if (shape === 'intel-ultra') {
      body = P('M3 15L15 3H50Q53 3 53 6V41L41 53H6Q3 53 3 50Z', 'l');
      D = { x: 9, y: 9, w: 38, h: 38 }; split = true;
    } else if (shape === 'amd' || shape === 'athlon') {
      var xs = shape === 'athlon' ? [20, 28, 36] : [18, 25, 32, 39];
      body = P('M7 14V45Q7 49 11 49H45Q49 49 49 45V11Q49 7 45 7H14', 'l') + P('M14 7L7 14', 'a') +
        P(pinsTB(xs, 3.5, 52.5, 7, 49) + pinsLR(xs, 3.5, 52.5, 7, 49), 'd');
      if (ai) body += P('M51 2V8M48 5H54', 'a');
    } else if (shape === 'amd-tr') {
      body = P('M3 17V43Q3 46 6 46H50Q53 46 53 43V13Q53 10 50 10H10', 'l') + P('M10 10L3 17', 'a') +
        P(pinsTB([14, 21, 28, 35, 42], 6, 50, 10, 46), 'd');
      D = { x: 7, y: 14, w: 42, h: 28 };
    } else if (shape === 'qualcomm') {
      body = P('M12 6H44', 'd') + R(4, 10, 48, 42, 10, 'l') + dot(22, 49, 'a') + dot(28, 49, 'a') + dot(34, 49, 'a');
      D = { x: 9, y: 15, w: 38, h: 30 };
    } else if (shape === 'arm') {
      body = R(5, 5, 46, 46, 6, 'l');
      [14, 21, 28, 35, 42].forEach(function (v) { body += dot(8.5, v, 'd') + dot(47.5, v, 'd') + dot(v, 8.5, 'd') + dot(v, 47.5, 'd'); });
      D = { x: 12, y: 12, w: 32, h: 32 };
    } else if (shape === 'apple') {
      body = R(10, 8, 36, 40, 9, 'l') + R(2, 21, 6, 14, 2, 'd') + R(48, 21, 6, 14, 2, 'd');
      D = { x: 13, y: 12, w: 30, h: 32 };
    } else {
      body = R(8, 8, 40, 40, 5, 'l') + P(pinsTB([18, 28, 38], 4, 52, 8, 48) + pinsLR([18, 28, 38], 4, 52, 8, 48), 'd');
      D = { x: 13, y: 13, w: 30, h: 30 };
    }

    var die = R(D.x, D.y, D.w, D.h, 3, 'f');
    var cx = D.x + D.w / 2, cy = D.y + D.h / 2, tw = D.w - 4, text = '';
    if (split) {
      var mid = D.y + D.h / 2;
      die += '<path d="M' + (D.x + 2) + ' ' + mid + 'H' + (D.x + D.w - 2) + '" style="fill:none;stroke:var(--art-ink,#073A4B);stroke-width:1.5;stroke-linecap:round"/>';
      var f1 = fit(fam, tw, 11, 8), f2 = fit(gen || ' ', tw, 9, 8);
      text = T(fam, cx, D.y + D.h * 0.25 + f1.size * 0.35, tw, 11, 8) + (gen ? T(gen, cx, D.y + D.h * 0.75 + f2.size * 0.35, tw, 9, 8) : '');
    } else if (fam && gen) {
      var s1 = fit(fam, tw, 11, 8).size, s2 = fit(gen, tw, 9, 8).size;
      var top = cy - (0.72 * s1 + 4 + 0.72 * s2) / 2;
      var b1 = top + 0.72 * s1, b2 = b1 + 4 + 0.72 * s2;
      text = T(fam, cx, b1, tw, 11, 8) + T(gen, cx, b2, tw, 9, 8);
    } else if (fam || gen) {
      var one = fam || gen, s = fit(one, tw, 14, 8).size;
      text = T(one, cx, cy + s * 0.36, tw, 14, 8);
    }
    var label = [fam, gen].filter(Boolean).join(' ') + ' processor';
    return svg(body + die + text, label);
  }

  var ORD = function (v) { v = +v; var s = ['th', 'st', 'nd', 'rd'], m = v % 100; return v + (s[(m - 20) % 10] || s[m] || s[0]); };
  var clean = function (s) { return String(s || '').replace(/\(R\)|\(TM\)|\(tm\)|\u00AE|\u2122/gi, ' ').replace(/\s+/g, ' ').trim(); };

  function parseCpuName(raw) {
    var s = clean(raw), m, r = { vendor: 'generic', family: 'CPU', generation: '', key: 'cpu-generic', ai: false };
    if (!s) return r;
    // Apple silicon
    if ((/apple/i.test(s) || /^M[1-9]\b/i.test(s)) && !/intel|amd|ryzen/i.test(s)) {
      m = s.match(/\bM([1-9])\b/i); var vr = (s.match(/\b(Pro|Max|Ultra)\b/i) || [])[1];
      r.vendor = 'apple';
      if (m) {
        r.family = 'M' + m[1]; r.generation = vr ? vr[0].toUpperCase() + vr.slice(1).toLowerCase() : '';
        r.key = vr ? 'cpu-apple-' + vr.toLowerCase() : (+m[1] <= 4 ? 'cpu-apple-m' + m[1] : 'cpu-apple-silicon');
      } else { r.family = 'Apple'; r.generation = 'SoC'; r.key = 'cpu-apple-silicon'; }
      return r;
    }
    // Qualcomm Snapdragon
    if (/snapdragon|qualcomm|oryon|\bX1[EP]\b/i.test(s)) {
      r.vendor = 'qualcomm'; r.key = 'cpu-snapdragon'; r.family = 'Qualcomm';
      var g = (s.match(/Gen\s*(\d)/i) || [])[1];
      if (/X\s*Elite|X1E/i.test(s)) { r.key = 'cpu-snapdragon-x-elite'; r.family = 'X Elite'; r.generation = (s.match(/X1E\w*/i) || ['X1E'])[0].slice(0, 8); }
      else if (/X\s*Plus|X1P/i.test(s)) { r.key = 'cpu-snapdragon-x-plus'; r.family = 'X Plus'; r.generation = (s.match(/X1P\w*/i) || ['X1P'])[0].slice(0, 8); }
      else if (/8cx/i.test(s)) { r.key = 'cpu-snapdragon-8cx'; r.family = '8cx'; r.generation = g ? 'Gen ' + g : ''; }
      else if (/\b7c/i.test(s)) { r.key = 'cpu-snapdragon-7c'; r.family = '7c'; r.generation = g ? 'Gen ' + g : ''; }
      else { var num = (s.match(/\b(\d{3,4})\b/) || [])[1]; r.generation = num || ''; }
      return r;
    }
    // Intel
    if (/intel|\bcore\b|celeron|pentium|xeon/i.test(s) && !/amd|ryzen/i.test(s)) {
      r.vendor = 'intel'; r.key = 'cpu-intel'; r.family = 'Intel';
      var ordGen = (s.match(/(\d{1,2})(?:st|nd|rd|th)\s*Gen/i) || [])[1];
      if ((m = s.match(/Core\s*Ultra\s*([3579])\s*(?:Processor\s*)?(\d{3})?/i))) {
        var t = m[1]; r.family = 'Ultra ' + t; r.key = 'cpu-intel-ultra-' + (t === '3' ? '5' : t);
        var d3 = m[2] ? m[2][0] : ''; r.generation = d3 ? 'Series ' + d3 : '';
      } else if ((m = s.match(/Core\s*i([3579])[- ]?(\d{3,5})?/i))) {
        r.family = 'Core i' + m[1]; r.key = 'cpu-intel-core-i' + m[1];
        var gn = ordGen || (m[2] ? (m[2].length === 3 ? '1' : m[2].length === 4 ? m[2].slice(0, 1) : m[2].slice(0, 2)) : '');
        r.generation = gn ? ORD(gn) + ' Gen' : '';
      } else if (/celeron/i.test(s)) {
        r.family = 'Celeron'; r.key = 'cpu-intel-celeron'; r.generation = ((s.match(/Celeron\s*(?:CPU\s*)?([A-Z]?\d{3,5}\w*)/i) || [])[1] || '').slice(0, 8);
      } else if (/pentium/i.test(s)) {
        r.family = 'Pentium'; r.key = 'cpu-intel-pentium';
        r.generation = (s.match(/Pentium\s*(Gold|Silver)/i) || [])[1] || ((s.match(/Pentium\s*(?:CPU\s*)?([A-Z]?\d{3,5}\w*)/i) || [])[1] || '').slice(0, 8);
      } else if (/xeon/i.test(s)) { r.family = 'Xeon'; r.generation = ((s.match(/Xeon\s*(?:\w+\s*)?([EWD]-?\d{3,5}\w*)/i) || [])[1] || '').slice(0, 8); }
      return r;
    }
    // AMD
    if (/amd|ryzen|athlon|threadripper|epyc/i.test(s)) {
      r.vendor = 'amd'; r.key = 'cpu-amd'; r.family = 'AMD';
      if ((m = s.match(/Ryzen\s*AI\s*(?:Max\+?\s*)?([3579])?\s*(?:HX|PRO|MAX\+?)?\s*(\d{3})/i))) {
        r.key = 'cpu-amd-ryzen-ai'; r.family = 'Ryzen AI'; r.ai = true; r.generation = m[2] ? m[2][0] + '00' : '';
      } else if (/threadripper/i.test(s)) {
        m = s.match(/Threadripper\s*(PRO\s*)?(\d)(\d{3})/i);
        r.key = 'cpu-amd-threadripper'; r.family = 'Ryzen TR'; r.generation = m ? (m[1] ? 'PRO ' : '') + m[2] + '000' : '';
      } else if ((m = s.match(/Ryzen\s*([3579])\s*(?:PRO\s*)?(\d)(\d{3})/i))) {
        r.key = 'cpu-amd-ryzen-' + m[1]; r.family = 'Ryzen ' + m[1]; r.generation = m[2] + '000';
      } else if ((m = s.match(/Ryzen\s*(Z\d)/i))) { r.family = 'Ryzen ' + m[1].toUpperCase(); }
      else if (/athlon/i.test(s)) {
        m = s.match(/Athlon\s*(?:(Gold|Silver|PRO)\s*)?([A-Z]?(\d)\d{3}\w*)?/i);
        r.key = 'cpu-amd-athlon'; r.family = 'Athlon'; r.generation = m && m[3] ? m[3] + '000' : (m && m[1]) || '';
      } else if (/epyc/i.test(s)) { r.family = 'EPYC'; }
      return r;
    }
    if (/\barm|aarch64|cortex|neoverse|mediatek|exynos|rockchip|broadcom/i.test(s)) { r.vendor = 'arm'; r.family = 'ARM'; r.generation = '64-bit'; r.key = 'cpu-arm'; }
    return r;
  }

  /* ---------- graphics ---------- */
  function fan(cx, cy, rad) {
    var b = '';
    [0, 120, 240].forEach(function (a) { b += '<path d="M' + cx + ' ' + (cy - 3.5) + 'V' + (cy - rad + 2) + '" transform="rotate(' + a + ' ' + cx + ' ' + cy + ')" style="' + STY.l + '"/>'; });
    return C(cx, cy, rad, 'l') + b + C(cx, cy, 2.4, 'f');
  }
  function gpuCard(kind, label) {
    var body = R(3, 11, 50, 29, 3, 'l') + P('M9 40V46H29V40', 'l') + P('M34 46H48', 'd');
    if (kind === 'generic') return body + fan(19, 25.5, 9) + fan(38, 25.5, 9);
    var s = body + fan(17, 25.5, 9) + plate(32, 17, 18, 10, label, 10) + P('M32 33H50', 'd');
    if (kind === 'nvidia') s += P('M30.5 12L27.5 39', 'a');
    if (kind === 'amd') s += P('M32 13.5H50', 'a');
    if (kind === 'arc') s = s.replace(P('M32 33H50', 'd'), P('M32 35Q41 30.5 50 35', 'a'));
    return s;
  }
  function gpuIntegrated() {
    return R(8, 8, 40, 40, 4, 'l') + R(13, 13, 30, 30, 2, 'd') +
      R(16, 16, 7, 7, 1, 'l') + R(26, 16, 7, 7, 1, 'l') + R(16, 26, 7, 7, 1, 'l') +
      plate(27, 28, 17, 13, 'GPU', 10) +
      P('M3 20H8M3 28H8M3 36H8M48 20H53', 'd');
  }

  /* ---------- devices ---------- */
  function screenPane() { return R(15, 13, 14, 9, 1.5, 'f'); }
  function laptopBase() { return P('M4 38H52L49 45Q48.5 46 47 46H9Q7.5 46 7 45Z', 'l') + P('M23 38Q23 41 26 41H30Q33 41 33 38', 'd'); }
  var DEV = {
    laptop: function () { return R(9, 8, 38, 26, 3, 'l') + screenPane() + P('M15 28H33', 'd') + laptopBase(); },
    gaming: function () {
      return P('M11 8H45Q47 8 47 10V34H9V10Q9 8 11 8Z', 'l') +
        '<path d="M30 12L22 24H28L26 31L35 19H29Z" style="' + STY.f + '"/>' +
        P('M3 38H53L50 47H6Z', 'l') + P('M10 42H14M10 44H14', 'd') + P('M20 43H46', 'a');
    },
    tower: function () {
      return R(14, 4, 28, 48, 4, 'l') + P('M19 11H37M19 16H37', 'd') + C(28, 31, 7, 'd') + C(28, 31, 3, 'f') + dot(28, 44.5, 'a') + P('M17 52H21M35 52H39', 'l');
    },
    aio: function () {
      return R(5, 6, 46, 32, 3, 'l') + P('M5 31H51', 'd') + R(12, 12, 14, 10, 1.5, 'f') + P('M31 14H44M31 19H40', 'd') + P('M28 38V46M20 48H36', 'l') + dot(28, 34.5, 'a');
    },
    tablet: function () {
      return R(12, 4, 32, 48, 5, 'l') + R(18, 14, 20, 14, 2, 'f') + P('M18 35H38M18 40H30', 'd') + dot(28, 8.5, 'a') + P('M24 48H32', 'd');
    },
    twoinone: function () {
      return R(5, 14, 30, 20, 3, 'l') + R(10, 19, 10, 6, 1, 'f') + P('M2 38H38L36 44H4Z', 'l') +
        P('M41 9A11 11 0 0 1 52 20', 'a') + P('M52 14V20H46', 'a');
    },
    mac: function () {
      return R(8, 8, 40, 27, 4, 'l') + R(23, 9, 10, 3.5, 1.5, 'd') + '<circle cx="28" cy="24" r="5.5" style="' + STY.f + '"/>' +
        P('M3 38H53Q53 45 48 45H8Q3 45 3 38Z', 'l') + P('M23 38Q28 42.5 33 38', 'd');
    },
    pc: function () {
      return R(4, 8, 32, 26, 3, 'l') + R(10, 14, 12, 8, 1.5, 'f') + P('M20 34V40M12 42H28', 'l') + R(40, 14, 13, 34, 2.5, 'l') + P('M43 20H50M43 24H50', 'd') + dot(46.5, 42, 'a');
    },
    mini: function () {
      return R(8, 20, 40, 18, 5, 'l') + R(14, 27, 12, 4, 2, 'f') + C(40, 29, 2.5, 'a') + P('M12 44H44', 'd') + P('M30 27H33M30 31H33', 'd');
    },
    handheld: function () {
      return R(3, 15, 50, 26, 10, 'l') + R(18, 22, 20, 12, 1.5, 'f') + P('M9 28H13M11 26V30', 'd') + dot(46, 25, 'a') + dot(46, 32, 'a') + dot(43, 28.5, 'a') + dot(49, 28.5, 'a');
    }
  };

  /* ---------- memory ---------- */
  function contacts(x0, x1, step, gapAt) {
    var s = '';
    for (var x = x0; x <= x1; x += step) { if (gapAt && Math.abs(x - gapAt) < 3.2) continue; s += '<rect x="' + n(x) + '" y="38.5" width="2.4" height="6" rx=".6" style="' + STY.k + '"/>'; }
    return s;
  }
  function ramStick(type, gapAt, soDimm) {
    var x = soDimm ? 8 : 3, w = soDimm ? 40 : 50;
    var s = R(x, 14, w, 24, 3, 'l') + contacts(x + 3, x + w - 5, 3.6, gapAt);
    if (type) {
      s += plate(x + 4, 19, soDimm ? 20 : 22, 12, type, 10);
      var cx0 = x + (soDimm ? 29 : 31);
      s += R(cx0, 19, 5, 12, 1, 'd') + R(cx0 + 7, 19, 5, 12, 1, 'd');
      if (!soDimm) s += R(cx0 + 14, 19, 5, 12, 1, 'd');
    } else {
      [8, 17, 26, 35].forEach(function (cx) { s += '<rect x="' + cx + '" y="20" width="7" height="10" rx="1.5" style="' + STY.f + '"/>'; });
      s += R(45, 22, 4, 6, 1, 'd');
    }
    if (soDimm) s += P('M8 21H10.5M8 31H10.5M48 21H45.5M48 31H45.5', 'd');
    return s;
  }
  function ramSoldered(label) {
    var s = R(3, 10, 50, 36, 4, 'm') + plate(11, 18, 34, 20, label, 12);
    [15, 22, 29, 36, 43].forEach(function (x) { s += dot(x, 13.5, 'd') + dot(x, 42.5, 'd'); });
    return s + P('M3 28H7M49 28H53', 'a');
  }

  /* ---------- storage ---------- */
  var STO = {
    nvme: function () {
      var s = R(2, 18, 50, 20, 2, 'l') + plate(11, 22, 23, 12, 'NVMe', 10) + R(37, 22, 7, 12, 1, 'd');
      for (var i = 0; i < 5; i++) s += '<rect x="2.5" y="' + (21 + i * 3.2) + '" width="4.5" height="1.8" rx=".5" style="' + STY.k + '"/>';
      return s + C(48, 28, 2, 'd');
    },
    sata: function () {
      return R(8, 6, 40, 38, 4, 'l') + plate(14, 14, 28, 14, 'SSD', 12) + P('M14 36H30', 'd') + dot(41, 36, 'a') + P('M16 44V50H36V44', 'l') + dot(20, 47, 'a') + dot(25, 47, 'a');
    },
    hdd: function () {
      return R(6, 6, 44, 44, 5, 'l') + C(24, 26, 14, 'l') + C(24, 26, 3.5, 'f') + P('M44 12L30 24', 'l') + C(44, 12, 2, 'a') + P('M33 44H46', 'd');
    },
    external: function () {
      return R(4, 14, 34, 28, 5, 'l') + plate(10, 22, 20, 12, 'USB', 10) + dot(33, 37, 'a') + R(38, 24, 9, 8, 1.5, 'l') + P('M47 28H53', 'd') + '<rect x="41" y="26" width="2" height="1.6" style="' + STY.k + '"/><rect x="41" y="29" width="2" height="1.6" style="' + STY.k + '"/>';
    },
    usb: function () {
      return R(18, 20, 26, 16, 3, 'l') + R(5, 24, 13, 8, 1.5, 'l') + '<rect x="8" y="26" width="2.4" height="1.8" style="' + STY.k + '"/><rect x="8" y="29" width="2.4" height="1.8" style="' + STY.k + '"/>' + R(23, 25, 9, 6, 1.5, 'f') + C(39, 28, 2, 'd');
    },
    generic: function () {
      var c = 2 * Math.PI * 8;
      return R(8, 6, 40, 38, 5, 'l') + C(28, 22, 8, 'd') +
        '<circle cx="28" cy="22" r="8" transform="rotate(-90 28 22)" style="' + STY.a + ';stroke-dasharray:' + n(c * 0.86) + ' ' + n(c) + '"/>' +
        P('M14 37H27', 'd') + C(40, 37, 2, 'f') + P('M16 44V50M22 44V50M28 44V50M34 44V50', 'l');
    }
  };

  /* ---------- battery ---------- */
  function battery(state) {
    var filled = { full: 4, mid: 2, low: 1, charging: 2, unknown: 0 }[state] || 0;
    if (state === 'none') return R(4, 16, 42, 24, 5, 'm') + R(46, 24, 5, 8, 1.5, 'm') + P('M10 46L46 10', 'a');
    var s = R(4, 16, 42, 24, 5, 'l') + R(46, 24, 5, 8, 1.5, state === 'unknown' ? 'f' : 'l');
    for (var i = 0; i < 4; i++) {
      var x = 9 + i * 9.5;
      s += i < filled ? '<rect x="' + x + '" y="21" width="7" height="14" rx="2" style="' + STY.f + '"/>' : R(x, 21, 7, 14, 2, 'm');
    }
    if (state === 'charging') s += '<circle cx="45" cy="15" r="8.5" style="' + STY.k + '"/><path d="M46.5 8.5L40.5 16H44.5L43.5 21.5L49.5 14H45.5Z" style="' + STY.ink + '"/>';
    return s;
  }

  /* ---------- display, camera, OS ---------- */
  var DISP = {
    panel: function () { return R(6, 7, 44, 31, 3, 'l') + R(13, 14, 16, 10, 2, 'f') + P('M13 31H36', 'd') + dot(28, 10.5, 'a') + P('M19 44H37', 'l'); },
    monitor: function () { return R(4, 6, 48, 32, 3, 'l') + R(11, 13, 16, 10, 2, 'f') + P('M11 30H34', 'd') + dot(46, 33, 'a') + P('M28 38V46M18 48H38', 'l'); },
    multi: function () {
      return R(17, 10, 22, 18, 2.5, 'l') + R(21, 14, 9, 6, 1.5, 'f') + P('M28 28V34M22 36H34', 'l') +
        R(2, 15, 12, 11, 2, 'd') + P('M8 26V30M4 31H12', 'd') + R(42, 15, 12, 11, 2, 'd') + P('M48 26V30M44 31H52', 'd');
    }
  };
  var CAM = {
    webcam: function () { return C(28, 22, 17, 'l') + C(28, 22, 10, 'd') + C(28, 22, 5.5, 'f') + dot(31, 19, 'a') + P('M28 39V46M17 49H39', 'l'); },
    builtin: function () {
      return P('M8 32V18Q8 14 12 14H44Q48 14 48 18V32', 'l') + P('M12 32H44', 'd') + C(28, 21, 4.5, 'a') + C(28, 21, 2, 'f') + dot(18, 21, 'd') + dot(38, 21, 'd') + P('M20 40H36', 'm');
    },
    none: function () { return C(28, 24, 17, 'm') + C(28, 24, 8, 'm') + P('M12 40L44 8', 'a') + P('M28 41V47M19 50H37', 'd'); }
  };
  function osWindow(label, round) {
    var rx = round ? 5 : 1.5, pr = round ? 2 : 0.5;
    var s = R(4, 8, 48, 40, rx, 'l') + P('M4 17H52', 'l') + dot(10, 12.5, 'a') + dot(15.5, 12.5, 'a') + dot(21, 12.5, 'a');
    s += R(9, 22, 17, 10, pr, 'f');
    if (label) s += T(label, 17.5, 27 + 3.3, 14, 9.5, 8);
    return s + R(30, 22, 17, 10, pr, 'd') + R(9, 36, 17, 7, pr, 'd') + R(30, 36, 17, 7, pr, 'd');
  }

  /* ---------- registry ---------- */
  var REG = [];
  function add(key, kind, label, build) { REG.push({ key: key, kind: kind, label: label, build: build }); }
  function cpu(key, label, spec) { add(key, 'cpu', label, function () { return makeCpuArt(spec); }); }

  cpu('cpu-generic', 'Generic processor', { vendor: 'generic', family: 'CPU' });
  [3, 5, 7, 9].forEach(function (t) { cpu('cpu-intel-core-i' + t, 'Intel Core i' + t, { vendor: 'intel', family: 'Core i' + t, generation: '13th Gen' }); });
  [5, 7, 9].forEach(function (t) { cpu('cpu-intel-ultra-' + t, 'Intel Core Ultra ' + t, { vendor: 'intel', family: 'Ultra ' + t, generation: 'Series 2' }); });
  cpu('cpu-intel-celeron', 'Intel Celeron', { vendor: 'intel', family: 'Celeron', generation: 'N4500' });
  cpu('cpu-intel-pentium', 'Intel Pentium', { vendor: 'intel', family: 'Pentium', generation: 'Gold' });
  cpu('cpu-intel', 'Intel processor (other)', { vendor: 'intel', family: 'Intel' });
  [3, 5, 7, 9].forEach(function (t) { cpu('cpu-amd-ryzen-' + t, 'AMD Ryzen ' + t, { vendor: 'amd', family: 'Ryzen ' + t, generation: '5000' }); });
  cpu('cpu-amd-ryzen-ai', 'AMD Ryzen AI', { vendor: 'amd', family: 'Ryzen AI', generation: '300', ai: true });
  cpu('cpu-amd-threadripper', 'AMD Ryzen Threadripper', { vendor: 'amd', family: 'Ryzen TR', generation: '5000' });
  cpu('cpu-amd-athlon', 'AMD Athlon', { vendor: 'amd', family: 'Athlon', generation: '3000' });
  cpu('cpu-amd', 'AMD processor (other)', { vendor: 'amd', family: 'AMD' });
  cpu('cpu-snapdragon-7c', 'Qualcomm Snapdragon 7c', { vendor: 'qualcomm', family: '7c', generation: 'Gen 2' });
  cpu('cpu-snapdragon-8cx', 'Qualcomm Snapdragon 8cx', { vendor: 'qualcomm', family: '8cx', generation: 'Gen 3' });
  cpu('cpu-snapdragon-x-plus', 'Qualcomm Snapdragon X Plus', { vendor: 'qualcomm', family: 'X Plus', generation: 'X1P' });
  cpu('cpu-snapdragon-x-elite', 'Qualcomm Snapdragon X Elite', { vendor: 'qualcomm', family: 'X Elite', generation: 'X1E' });
  cpu('cpu-snapdragon', 'Qualcomm Snapdragon (other)', { vendor: 'qualcomm', family: 'Qualcomm' });
  cpu('cpu-arm', 'ARM processor (generic)', { vendor: 'arm', family: 'ARM', generation: '64-bit' });
  [1, 2, 3, 4].forEach(function (g) { cpu('cpu-apple-m' + g, 'Apple silicon M' + g, { vendor: 'apple', family: 'M' + g }); });
  cpu('cpu-apple-pro', 'Apple silicon Pro', { vendor: 'apple', family: 'M2', generation: 'Pro' });
  cpu('cpu-apple-max', 'Apple silicon Max', { vendor: 'apple', family: 'M2', generation: 'Max' });
  cpu('cpu-apple-ultra', 'Apple silicon Ultra', { vendor: 'apple', family: 'M2', generation: 'Ultra' });
  cpu('cpu-apple-silicon', 'Apple silicon (other)', { vendor: 'apple', family: 'Apple', generation: 'SoC' });

  add('gpu-nvidia-rtx', 'gpu', 'NVIDIA GeForce RTX card', function () { return svg(gpuCard('nvidia', 'RTX'), 'NVIDIA RTX graphics card'); });
  add('gpu-nvidia-geforce', 'gpu', 'NVIDIA GeForce card (GTX, MX, GT)', function () { return svg(gpuCard('nvidia', 'GTX'), 'NVIDIA GeForce graphics card'); });
  add('gpu-amd-radeon', 'gpu', 'AMD Radeon card', function () { return svg(gpuCard('amd', 'RX'), 'AMD Radeon graphics card'); });
  add('gpu-intel-arc', 'gpu', 'Intel Arc card', function () { return svg(gpuCard('arc', 'Arc'), 'Intel Arc graphics card'); });
  add('gpu-integrated', 'gpu', 'Integrated graphics (built into the processor)', function () { return svg(gpuIntegrated(), 'Integrated graphics on the processor'); });
  add('gpu-generic', 'gpu', 'Generic graphics card', function () { return svg(gpuCard('generic'), 'Graphics card'); });

  add('device-laptop', 'device', 'Laptop', function () { return svg(DEV.laptop(), 'Laptop'); });
  add('device-gaming-laptop', 'device', 'Gaming laptop', function () { return svg(DEV.gaming(), 'Gaming laptop'); });
  add('device-desktop-tower', 'device', 'Desktop tower', function () { return svg(DEV.tower(), 'Desktop tower'); });
  add('device-all-in-one', 'device', 'All-in-one', function () { return svg(DEV.aio(), 'All-in-one computer'); });
  add('device-tablet', 'device', 'Tablet', function () { return svg(DEV.tablet(), 'Tablet'); });
  add('device-2in1', 'device', '2-in-1 convertible', function () { return svg(DEV.twoinone(), '2-in-1 laptop'); });
  add('device-macbook', 'device', 'MacBook', function () { return svg(DEV.mac(), 'MacBook laptop'); });
  add('device-mini-pc', 'device', 'Mini PC', function () { return svg(DEV.mini(), 'Mini PC'); });
  add('device-handheld', 'device', 'Handheld gaming PC', function () { return svg(DEV.handheld(), 'Handheld gaming PC'); });
  add('device-generic-pc', 'device', 'Generic PC', function () { return svg(DEV.pc(), 'Computer'); });

  add('ram-ddr4', 'memory', 'DDR4 memory stick', function () { return svg(ramStick('DDR4', 33), 'DDR4 memory stick'); });
  add('ram-ddr5', 'memory', 'DDR5 memory stick', function () { return svg(ramStick('DDR5', 24), 'DDR5 memory stick'); });
  add('ram-sodimm-ddr4', 'memory', 'Laptop memory (SO-DIMM) DDR4', function () { return svg(ramStick('DDR4', 31, true), 'DDR4 laptop memory'); });
  add('ram-sodimm-ddr5', 'memory', 'Laptop memory (SO-DIMM) DDR5', function () { return svg(ramStick('DDR5', 25, true), 'DDR5 laptop memory'); });
  add('ram-lpddr', 'memory', 'Soldered LPDDR memory', function () { return svg(ramSoldered('LPDDR'), 'Soldered LPDDR memory'); });
  add('ram-soldered', 'memory', 'Soldered memory (other)', function () { return svg(ramSoldered('RAM'), 'Soldered memory'); });
  add('ram-generic', 'memory', 'Generic memory stick', function () { return svg(ramStick(null, 28), 'Memory stick'); });

  add('storage-nvme', 'storage', 'NVMe M.2 SSD', function () { return svg(STO.nvme(), 'NVMe M.2 solid state drive'); });
  add('storage-sata-ssd', 'storage', '2.5 in SATA SSD', function () { return svg(STO.sata(), 'SATA solid state drive'); });
  add('storage-hdd', 'storage', 'Hard disk drive', function () { return svg(STO.hdd(), 'Hard disk drive'); });
  add('storage-external', 'storage', 'External USB drive', function () { return svg(STO.external(), 'External USB drive'); });
  add('storage-usb-stick', 'storage', 'USB flash drive', function () { return svg(STO.usb(), 'USB flash drive'); });
  add('storage-generic', 'storage', 'Generic drive', function () { return svg(STO.generic(), 'Drive'); });

  add('battery-full', 'battery', 'Battery, full', function () { return svg(battery('full'), 'Battery full'); });
  add('battery-mid', 'battery', 'Battery, half', function () { return svg(battery('mid'), 'Battery half'); });
  add('battery-low', 'battery', 'Battery, low', function () { return svg(battery('low'), 'Battery low'); });
  add('battery-charging', 'battery', 'Battery, charging', function () { return svg(battery('charging'), 'Battery charging'); });
  add('battery-none', 'battery', 'No battery', function () { return svg(battery('none'), 'No battery'); });
  add('battery-unknown', 'battery', 'Battery, level not reported', function () { return svg(battery('unknown'), 'Battery level not reported'); });

  add('display-laptop-panel', 'display', 'Laptop screen', function () { return svg(DISP.panel(), 'Laptop screen'); });
  add('display-monitor', 'display', 'External monitor', function () { return svg(DISP.monitor(), 'Monitor'); });
  add('display-multi', 'display', 'Multiple monitors', function () { return svg(DISP.multi(), 'Multiple monitors'); });

  add('camera-webcam', 'camera', 'Webcam', function () { return svg(CAM.webcam(), 'Webcam'); });
  add('camera-builtin', 'camera', 'Built-in camera', function () { return svg(CAM.builtin(), 'Built-in camera'); });
  add('camera-none', 'camera', 'No camera', function () { return svg(CAM.none(), 'No camera'); });

  add('os-windows-11', 'os', 'Windows 11', function () { return svg(osWindow('11', true), 'Windows 11'); });
  add('os-windows-10', 'os', 'Windows 10', function () { return svg(osWindow('10', false), 'Windows 10'); });
  add('os-generic', 'os', 'Operating system (other)', function () { return svg(osWindow('', true), 'Operating system'); });

  var HARDWARE_ART = {}, HARDWARE_META = {};
  REG.forEach(function (e) { HARDWARE_ART[e.key] = e.build(); HARDWARE_META[e.key] = { kind: e.kind, label: e.label }; });
  var HARDWARE_KINDS = ['cpu', 'gpu', 'device', 'memory', 'storage', 'battery', 'display', 'camera', 'os'];
  var HARDWARE_FALLBACK = {
    cpu: 'cpu-generic', gpu: 'gpu-generic', device: 'device-generic-pc', memory: 'ram-generic', storage: 'storage-generic',
    battery: 'battery-unknown', display: 'display-monitor', camera: 'camera-webcam', os: 'os-generic'
  };

  /* ---------- picking ---------- */
  function str() { var o = []; for (var i = 0; i < arguments.length; i++) if (arguments[i] != null) o.push(String(arguments[i])); return o.join(' '); }
  function parseGpuName(raw) {
    var s = clean(raw);
    if (!s) return { key: HARDWARE_FALLBACK.gpu };
    if (/microsoft basic|virtual|vmware|hyper-v|parallels|remote display/i.test(s)) return { key: 'gpu-generic' };
    if (/nvidia|geforce|\bRTX\b|\bGTX\b|quadro|\bMX\s?\d{3}|titan/i.test(s)) return { key: /\bRTX\b/i.test(s) ? 'gpu-nvidia-rtx' : 'gpu-nvidia-geforce' };
    if (/\bintel\b|\barc\b|iris|\buhd\b|\bxe\b/i.test(s) && !/amd|radeon/i.test(s)) {
      if (/\barc\b/i.test(s) && /\b[AB]\d{3}\w?\b/i.test(s)) return { key: 'gpu-intel-arc' };
      return { key: 'gpu-integrated' };
    }
    if (/amd|radeon|\bati\b/i.test(s)) {
      if (/\bRX\s*\d|Pro\s*W\d|\bVII\b|Radeon\s+(?:R[579]|HD)\s|instinct/i.test(s)) return { key: 'gpu-amd-radeon' };
      return { key: 'gpu-integrated' };
    }
    if (/adreno|apple|mali|powervr|videocore/i.test(s)) return { key: 'gpu-integrated' };
    return { key: HARDWARE_FALLBACK.gpu };
  }

  var CHASSIS = { 3: 'desktop', 4: 'desktop', 5: 'desktop', 6: 'desktop', 7: 'desktop', 15: 'desktop', 16: 'desktop', 13: 'aio', 8: 'laptop', 9: 'laptop', 10: 'laptop', 11: 'laptop', 12: 'laptop', 14: 'laptop', 18: 'laptop', 21: 'laptop', 30: 'tablet', 31: '2in1', 32: 'tablet', 34: 'mini', 35: 'mini', 36: 'mini' };
  function pickDevice(f) {
    var text = str(f.model, f.name, f.manufacturer, f.product, f.family, f.chassisText);
    var ch = f.chassis, kind = null;
    if (typeof ch === 'number' || /^\d+$/.test(String(ch == null ? '' : ch))) kind = CHASSIS[+ch] || null;
    else if (ch) {
      var c = String(ch);
      kind = /all[- ]?in[- ]?one/i.test(c) ? 'aio' : /convertible|2-in-1|detachable/i.test(c) ? '2in1' : /tablet/i.test(c) ? 'tablet' :
        /mini|stick|nuc/i.test(c) ? 'mini' : /desktop|tower|pizza|space|lunch/i.test(c) ? 'desktop' : /laptop|notebook|portable|sub|hand/i.test(c) ? 'laptop' : null;
    }
    if (f.isLaptop === true && !kind) kind = 'laptop';
    if (/macbook/i.test(text)) return 'device-macbook';
    if (/imac/i.test(text)) return 'device-all-in-one';
    if (/mac\s?mini|\bNUC\b|mini\s?pc|thinkcentre\s*(?:m\d+\w*\s*)?tiny|optiplex.*micro/i.test(text)) return 'device-mini-pc';
    if (/rog\s*ally|steam\s*deck|legion\s*go|handheld|ayaneo|\bONEXPLAYER\b|msi\s*claw/i.test(text)) return 'device-handheld';
    if (kind === 'aio' || /all[- ]?in[- ]?one|\bAIO\b/i.test(text)) return 'device-all-in-one';
    if (kind === 'tablet' || /surface\s*(?:pro|go)|\btablet\b|\bipad\b/i.test(text)) return 'device-tablet';
    if (kind === '2in1' || /2-in-1|\byoga\b|\bflip\b|x360|convertible|surface\s*laptop\s*studio/i.test(text)) return 'device-2in1';
    if (kind === 'mini') return 'device-mini-pc';
    var gaming = /\bTUF\b|\bROG\b|legion|predator|nitro|alienware|\bOMEN\b|victus|katana|raider|stealth|\bGF\d{2}|\bGP\d{2}|\bGS\d{2}|\bloq\b|gaming|g15|g16|g14|\bGL\d{2}/i.test(text);
    if (kind === 'laptop') return gaming ? 'device-gaming-laptop' : 'device-laptop';
    if (kind === 'desktop' || /desktop|tower/i.test(text)) return 'device-desktop-tower';
    if (gaming && f.isLaptop !== false) return 'device-gaming-laptop';
    if (f.isLaptop === true || /laptop|notebook|thinkpad|latitude|elitebook|probook|zenbook|vivobook|inspiron|ideapad|swift|aspire|spectre|envy|pavilion|xps|surface\s*laptop/i.test(text)) return 'device-laptop';
    return HARDWARE_FALLBACK.device;
  }
  var SMB_TYPE = { 24: 'DDR3', 26: 'DDR4', 34: 'DDR5', 27: 'LPDDR', 28: 'LPDDR', 29: 'LPDDR', 30: 'LPDDR', 35: 'LPDDR' };
  var WIN_FORM = { 8: 'dimm', 12: 'sodimm', 21: 'solder', 22: 'solder' };
  function pickMemory(f) {
    var t = String(f.type == null ? (f.memoryType == null ? '' : f.memoryType) : f.type);
    if (/^\d+$/.test(t)) t = SMB_TYPE[+t] || '';
    var ff = f.formFactor == null ? '' : String(f.formFactor);
    if (/^\d+$/.test(ff)) ff = WIN_FORM[+ff] || ''; else ff = /so-?dimm/i.test(ff) ? 'sodimm' : /solder|onboard|chip|bga/i.test(ff) ? 'solder' : /dimm/i.test(ff) ? 'dimm' : '';
    var lp = /lpddr/i.test(t), d4 = /ddr4/i.test(t) && !lp, d5 = /ddr5/i.test(t) && !lp;
    if (lp) return 'ram-lpddr';
    if (f.soldered === true || ff === 'solder') return 'ram-soldered';
    if (ff === 'sodimm' || (!ff && f.isLaptop === true)) return d4 ? 'ram-sodimm-ddr4' : d5 ? 'ram-sodimm-ddr5' : HARDWARE_FALLBACK.memory;
    return d4 ? 'ram-ddr4' : d5 ? 'ram-ddr5' : HARDWARE_FALLBACK.memory;
  }
  function pickStorage(f) {
    var text = str(f.model, f.name, f.caption), bus = String(f.busType == null ? (f.interface == null ? '' : f.interface) : f.busType), media = String(f.mediaType == null ? '' : f.mediaType);
    if (/usb/i.test(bus) || f.external === true || f.removable === true) return /flash|stick|thumb|cruzer|datatraveler|jetflash|ultra\s*fit/i.test(text) ? 'storage-usb-stick' : 'storage-external';
    if (/nvme/i.test(bus) || /nvme|pcie|m\.2|\bSN\d{3}|\b980\b|\b970\b|\bMZVL/i.test(text)) return 'storage-nvme';
    if (/hdd|hard/i.test(media) || /\bHDD\b|ST\d{3,}|WD\d|HGST|Barracuda|Toshiba\s*MQ|\bMQ\d{2}/i.test(text)) return 'storage-hdd';
    if (/ssd|solid/i.test(media) || /\bSSD\b|\bsata\b/i.test(text)) return 'storage-sata-ssd';
    return HARDWARE_FALLBACK.storage;
  }
  function pickBattery(f) {
    if (f.present === false) return 'battery-none';
    var pct = f.percent == null ? (f.level == null ? null : +f.level) : +f.percent;
    if (f.charging === true) return 'battery-charging';
    if (pct == null || isNaN(pct)) return HARDWARE_FALLBACK.battery;
    return pct >= 70 ? 'battery-full' : pct >= 25 ? 'battery-mid' : 'battery-low';
  }
  function pickDisplay(f) {
    if ((f.count | 0) > 1) return 'display-multi';
    if (f.internal === true || f.isLaptop === true || /internal|built-?in|laptop|panel|edp|lvds/i.test(str(f.kind, f.type, f.connection, f.name))) return 'display-laptop-panel';
    if (f.external === true || /external|hdmi|displayport|monitor|dvi|vga/i.test(str(f.kind, f.type, f.connection, f.name))) return 'display-monitor';
    return HARDWARE_FALLBACK.display;
  }
  function pickCamera(f) {
    if (f.present === false || f.count === 0) return 'camera-none';
    if (f.builtIn === true || f.internal === true || /integrated|built-?in|internal|\bIR\b|hd\s*camera|laptop/i.test(str(f.name, f.kind, f.connection))) return 'camera-builtin';
    if (f.external === true || /usb|external|logitech|webcam/i.test(str(f.name, f.kind, f.connection))) return 'camera-webcam';
    return HARDWARE_FALLBACK.camera;
  }
  function pickOs(f) {
    var t = str(f.name, f.caption, f.edition, f.version), b = parseInt(String(f.build == null ? (String(f.version || '').split('.')[2] || '') : f.build), 10);
    if (/windows\s*10/i.test(t)) return 'os-windows-10';
    if (/windows\s*11/i.test(t) || (!isNaN(b) && b >= 22000)) return 'os-windows-11';
    if (!isNaN(b) && b >= 10240) return 'os-windows-10';
    return HARDWARE_FALLBACK.os;
  }

  function norm(facts) { return typeof facts === 'string' ? { name: facts, model: facts, type: facts } : (facts || {}); }
  function pickArt(kind, facts) {
    var f = norm(facts), key;
    switch (kind) {
      case 'cpu': key = parseCpuName(str(f.name, f.model, f.brand, f.processor)).key;
        if (key === 'cpu-generic' && /arm|aarch/i.test(str(f.arch, f.architecture))) key = 'cpu-arm';
        break;
      case 'gpu': key = parseGpuName(str(f.name, f.model, f.adapter)).key; break;
      case 'device': key = pickDevice(f); break;
      case 'memory': key = pickMemory(f); break;
      case 'storage': key = pickStorage(f); break;
      case 'battery': key = pickBattery(f); break;
      case 'display': key = pickDisplay(f); break;
      case 'camera': key = pickCamera(f); break;
      case 'os': key = pickOs(f); break;
      default: return HARDWARE_FALLBACK.device;
    }
    return HARDWARE_ART[key] ? key : (HARDWARE_FALLBACK[kind] || HARDWARE_FALLBACK.device);
  }
  function renderArt(kind, facts) {
    var f = norm(facts), key = pickArt(kind, f);
    if (kind === 'cpu') {
      var p = parseCpuName(str(f.name, f.model, f.brand, f.processor));
      if (p.key === key && key !== 'cpu-generic') {
        return { key: key, label: HARDWARE_META[key].label, svg: makeCpuArt({ vendor: p.vendor, family: p.family, generation: p.generation, ai: p.ai }) };
      }
    }
    return { key: key, label: HARDWARE_META[key].label, svg: HARDWARE_ART[key] };
  }

  var api = {
    HARDWARE_ART: HARDWARE_ART, HARDWARE_META: HARDWARE_META, HARDWARE_KINDS: HARDWARE_KINDS, HARDWARE_FALLBACK: HARDWARE_FALLBACK,
    pickArt: pickArt, renderArt: renderArt, makeCpuArt: makeCpuArt, parseCpuName: parseCpuName, parseGpuName: parseGpuName
  };
  if (typeof window !== 'undefined') Object.keys(api).forEach(function (k) { root[k] = api[k]; });
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
