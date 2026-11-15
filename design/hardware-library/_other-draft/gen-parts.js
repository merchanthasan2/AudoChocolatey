const fs = require('fs');
const path = require('path');

function esc(s) {
  return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
}

function wrap(inner, w, h) {
  w = w || 56;
  h = h || 56;
  return (
    '<svg class="ill" viewBox="0 0 ' +
    w +
    ' ' +
    h +
    '" aria-hidden="true" xmlns="http://www.w3.org/2000/svg">' +
    inner +
    '</svg>'
  );
}

function text(cx, cy, line1, line2, sz1, sz2) {
  sz1 = sz1 || 7;
  sz2 = sz2 || 5.5;
  const y1 = line2 ? cy - 2 : cy + 2;
  const y2 = cy + 6;
  let t =
    '<text fill="var(--art-accent)" font-family="Segoe UI, sans-serif" font-weight="600" text-anchor="middle">';
  t +=
    '<tspan x="' +
    cx +
    '" y="' +
    y1 +
    '" font-size="' +
    sz1 +
    '">' +
    esc(line1) +
    '</tspan>';
  if (line2) {
    t +=
      '<tspan x="' +
      cx +
      '" y="' +
      y2 +
      '" font-size="' +
      sz2 +
      '" fill="var(--art-line)">' +
      esc(line2) +
      '</tspan>';
  }
  return t + '</text>';
}

function pinsIntel() {
  return (
    '<path class="m" d="M20 6V14M20 42V50M6 20H14M42 20H50"/>' +
    '<path class="m" d="M28 6V14M28 42V50M6 28H14M42 28H50"/>' +
    '<path class="m" d="M36 6V14M36 42V50M6 36H14M42 36H50"/>'
  );
}

function cpuIntel(line1, line2) {
  return wrap(
    pinsIntel() +
      '<rect class="l" x="14" y="14" width="28" height="28" rx="4"/>' +
      '<rect class="f" x="21" y="21" width="14" height="14" rx="2" opacity="0.35"/>' +
      '<circle class="m" cx="18" cy="18" r="1"/>' +
      text(28, 28, line1, line2)
  );
}

function cpuAmd(line1, line2) {
  return wrap(
    pinsIntel() +
      '<rect class="l" x="12" y="12" width="32" height="32" rx="6"/>' +
      '<rect class="f" x="19" y="19" width="18" height="18" rx="4" opacity="0.35"/>' +
      text(28, 28, line1, line2)
  );
}

function cpuWide(line1, line2) {
  let pins = '';
  for (let i = 8; i <= 48; i += 8) {
    pins += '<path class="m" d="M' + i + ' 4V12M' + i + ' 44V52"/>';
    pins += '<path class="m" d="M4 ' + i + 'H12M44 ' + i + 'H52"/>';
  }
  return wrap(
    pins +
      '<rect class="l" x="8" y="16" width="40" height="24" rx="4"/>' +
      '<rect class="f" x="14" y="22" width="28" height="12" rx="2" opacity="0.35"/>' +
      text(28, 28, line1, line2, 6.5, 5)
  );
}

function cpuMobile(line1, line2) {
  return wrap(
    '<rect class="l" x="10" y="14" width="36" height="28" rx="5"/>' +
      '<rect class="f" x="16" y="20" width="24" height="16" rx="3" opacity="0.35"/>' +
      '<path class="m" d="M14 32H42"/>' +
      text(28, 28, line1, line2, 6.5, 5)
  );
}

function cpuApple(line1, line2) {
  return wrap(
    '<rect class="l" x="11" y="11" width="34" height="34" rx="9"/>' +
      '<rect class="f" x="17" y="17" width="22" height="22" rx="6" opacity="0.35"/>' +
      text(28, 28, line1, line2)
  );
}

function cpuArm(line1, line2) {
  return wrap(
    '<rect class="l" x="13" y="13" width="30" height="30" rx="8"/>' +
      '<path class="f" d="M20 20h16v16H20z" opacity="0.35"/>' +
      '<path class="m" d="M16 28h6M34 28h6M28 16v6M28 34v6"/>' +
      text(28, 28, line1, line2)
  );
}

function gpuCard(fans, label) {
  let inner = '<rect class="l" x="3" y="11" width="50" height="29" rx="3"/>';
  if (fans === 2) {
    inner +=
      '<circle class="l" cx="19" cy="25.5" r="9"/><path class="l" d="M19 22V18.5" transform="rotate(0 19 25.5)"/><path class="l" d="M19 22V18.5" transform="rotate(120 19 25.5)"/><path class="l" d="M19 22V18.5" transform="rotate(240 19 25.5)"/><circle class="f" cx="19" cy="25.5" r="2.4"/>';
    inner +=
      '<circle class="l" cx="38" cy="25.5" r="9"/><path class="l" d="M38 22V18.5" transform="rotate(0 38 25.5)"/><path class="l" d="M38 22V18.5" transform="rotate(120 38 25.5)"/><path class="l" d="M38 22V18.5" transform="rotate(240 38 25.5)"/><circle class="f" cx="38" cy="25.5" r="2.4"/>';
  } else {
    inner +=
      '<circle class="l" cx="28" cy="25.5" r="10"/><circle class="f" cx="28" cy="25.5" r="2.6"/>';
  }
  inner += '<path class="l" d="M9 40V46H29V40"/><path class="m" d="M36 46H48"/>';
  if (label) inner += text(28, 47, label, null, 5);
  return wrap(inner);
}

function gpuIntegrated() {
  return wrap(
    '<rect class="l" x="8" y="10" width="40" height="36" rx="4"/>' +
      '<rect class="f" x="14" y="16" width="28" height="24" rx="3" opacity="0.25"/>' +
      '<rect class="l" x="22" y="32" width="12" height="8" rx="2"/>' +
      '<path class="m" d="M18 20h20M18 24h14"/>' +
      text(28, 24, 'iGPU', 'on die', 6, 5)
  );
}

const ART = {};

ART['cpu.generic'] = cpuArm('CPU', '');

['i3', 'i5', 'i7', 'i9'].forEach((t) => {
  ART['cpu.intel.core.' + t] = cpuIntel('Core ' + t.toUpperCase(), 'Gen');
});
ART['cpu.intel.core.ultra'] = cpuIntel('Ultra 7', '');
ART['cpu.intel.celeron'] = cpuIntel('Celeron', '');
ART['cpu.intel.pentium'] = cpuIntel('Pentium', '');

['3', '5', '7', '9'].forEach((n) => {
  ART['cpu.amd.ryzen.' + n] = cpuAmd('Ryzen ' + n, 'Series');
});
ART['cpu.amd.ryzen.ai'] = cpuAmd('Ryzen', 'AI');
ART['cpu.amd.threadripper'] = cpuWide('Threadripper', 'PRO');
ART['cpu.amd.athlon'] = cpuAmd('Athlon', '');

ART['cpu.qualcomm.snapdragon.7c'] = cpuMobile('SD', '7c');
ART['cpu.qualcomm.snapdragon.8cx'] = cpuMobile('SD', '8cx');
ART['cpu.qualcomm.snapdragon.x.plus'] = cpuMobile('X', 'Plus');
ART['cpu.qualcomm.snapdragon.x.elite'] = cpuMobile('X', 'Elite');
ART['cpu.arm.generic'] = cpuArm('ARM', '');

['m1', 'm2', 'm3', 'm4'].forEach((m) => {
  const M = m.toUpperCase();
  ART['cpu.apple.' + m] = cpuApple(M, '');
  ART['cpu.apple.' + m + '.pro'] = cpuApple(M, 'Pro');
  ART['cpu.apple.' + m + '.max'] = cpuApple(M, 'Max');
  ART['cpu.apple.' + m + '.ultra'] = cpuApple(M, 'Ultra');
});

ART['gpu.generic'] = gpuCard(1);
ART['gpu.nvidia.geforce'] = gpuCard(2, 'RTX');
ART['gpu.nvidia.rtx'] = ART['gpu.nvidia.geforce'];
ART['gpu.amd.radeon'] = gpuCard(2, 'Radeon');
ART['gpu.intel.arc'] = gpuCard(2, 'Arc');
ART['gpu.integrated'] = gpuIntegrated();

function devLaptop(gaming) {
  const g = gaming
    ? '<path class="f" d="M38 16h8v6h-8z" opacity="0.45"/>'
    : '';
  return wrap(
    '<rect class="l" x="7" y="7" width="34" height="24" rx="3"/>' +
      '<path class="l" d="M3 35H45L42 41H6Z"/>' +
      g +
      '<rect class="m" x="24" y="38" width="8" height="2" rx="1"/>'
  );
}

ART['device.laptop'] = devLaptop(false);
ART['device.laptop.gaming'] = devLaptop(true);
ART['device.desktop'] = wrap(
  '<rect class="l" x="14" y="6" width="28" height="40" rx="3"/>' +
    '<circle class="m" cx="28" cy="18" r="3"/>' +
    '<path class="m" d="M18 30h20"/>' +
    '<rect class="f" x="20" y="42" width="16" height="4" rx="1" opacity="0.4"/>'
);
ART['device.aio'] = wrap(
  '<rect class="l" x="8" y="6" width="40" height="30" rx="4"/>' +
    '<path class="l" d="M22 36h12v6H22z"/>' +
    '<path class="m" d="M18 46h20"/>'
);
ART['device.tablet'] = wrap(
  '<rect class="l" x="12" y="6" width="32" height="44" rx="4"/>' +
    '<circle class="f" cx="28" cy="46" r="1.5"/>'
);
ART['device.macbook'] = wrap(
  '<rect class="l" x="7" y="9" width="34" height="22" rx="3"/>' +
    '<path class="l" d="M3 35H45L42 41H6Z"/>' +
    '<rect class="f" x="26" y="14" width="12" height="12" rx="6" opacity="0.3"/>'
);
ART['device.pc.generic'] = wrap(
  '<rect class="l" x="6" y="8" width="44" height="32" rx="4"/>' +
    '<path class="m" d="M14 20h28M14 26h20"/>' +
    '<circle class="m" cx="42" cy="36" r="2"/>'
);

ART['memory.ddr4'] = wrap(
  '<rect class="l" x="3" y="15" width="50" height="24" rx="3"/>' +
    '<rect class="f" x="8" y="20" width="7" height="10" rx="1.5"/>' +
    '<rect class="f" x="17" y="20" width="7" height="10" rx="1.5"/>' +
    '<rect class="f" x="26" y="20" width="7" height="10" rx="1.5"/>' +
    '<rect class="f" x="35" y="20" width="7" height="10" rx="1.5"/>' +
    '<path class="m" d="M8 34H30"/>' +
    '<text x="28" y="49" text-anchor="middle" fill="var(--art-line)" font-size="6" font-weight="600">DDR4</text>'
);
ART['memory.ddr5'] = wrap(
  '<rect class="l" x="3" y="15" width="50" height="24" rx="3"/>' +
    '<rect class="f" x="8" y="20" width="6" height="10" rx="1.5"/>' +
    '<rect class="f" x="15.5" y="20" width="6" height="10" rx="1.5"/>' +
    '<rect class="f" x="23" y="20" width="6" height="10" rx="1.5"/>' +
    '<rect class="f" x="30.5" y="20" width="6" height="10" rx="1.5"/>' +
    '<rect class="f" x="38" y="20" width="6" height="10" rx="1.5"/>' +
    '<path class="m" d="M8 34H34"/>' +
    '<text x="28" y="49" text-anchor="middle" fill="var(--art-line)" font-size="6" font-weight="600">DDR5</text>'
);
ART['memory.sodimm'] = wrap(
  '<rect class="l" x="6" y="18" width="44" height="20" rx="3"/>' +
    '<rect class="f" x="10" y="22" width="8" height="8" rx="1.5"/>' +
    '<rect class="f" x="22" y="22" width="8" height="8" rx="1.5"/>' +
    '<path class="m" d="M34 26h12"/>' +
    '<text x="28" y="48" text-anchor="middle" fill="var(--art-line)" font-size="6" font-weight="600">SO-DIMM</text>'
);
ART['memory.lpddr'] = wrap(
  '<rect class="l" x="10" y="16" width="36" height="24" rx="4"/>' +
    '<path class="md" d="M16 22h24M16 28h24M16 34h24"/>' +
    '<text x="28" y="48" text-anchor="middle" fill="var(--art-line)" font-size="6" font-weight="600">LPDDR</text>'
);

ART['storage.nvme'] = wrap(
  '<rect class="l" x="8" y="20" width="40" height="16" rx="2"/>' +
    '<rect class="f" x="12" y="24" width="20" height="8" rx="1" opacity="0.35"/>' +
    '<path class="m" d="M38 28h6"/>' +
    '<circle class="m" cx="46" cy="28" r="1.5"/>'
);
ART['storage.sata.ssd'] = wrap(
  '<rect class="l" x="6" y="18" width="44" height="20" rx="3"/>' +
    '<path class="m" d="M12 28h32"/>' +
    '<rect class="f" x="40" y="24" width="4" height="8" rx="1"/>'
);
ART['storage.hdd'] = wrap(
  '<rect class="l" x="6" y="14" width="44" height="28" rx="3"/>' +
    '<circle class="l" cx="28" cy="28" r="9"/>' +
    '<circle class="f" cx="28" cy="28" r="2"/>' +
    '<path class="m" d="M14 38h28"/>'
);
ART['storage.usb.external'] = wrap(
  '<rect class="l" x="10" y="22" width="30" height="18" rx="3"/>' +
    '<path class="l" d="M40 28h8v6h-8z"/>' +
    '<path class="m" d="M16 28h18"/>'
);

function bat(level, charge) {
  const fill =
    level === 'full' ? 38 : level === 'mid' ? 24 : level === 'low' ? 12 : 0;
  const ch = charge
    ? '<path class="f" d="M30 22l4 4-6 6-2-4z"/>'
  : '';
  let inner =
    '<rect class="l" x="4" y="16" width="42" height="24" rx="5"/>' +
    '<rect class="f" x="46" y="24" width="5" height="8" rx="1.5"/>';
  if (level !== 'none') {
    inner +=
      '<rect class="f" x="9" y="21" width="' +
      fill +
      '" height="14" rx="2" opacity="0.45"/>';
  } else inner += '<path class="md" d="M12 28h32"/>';
  inner += ch;
  return wrap(inner);
}

ART['battery.full'] = bat('full', false);
ART['battery.mid'] = bat('mid', false);
ART['battery.low'] = bat('low', false);
ART['battery.charging'] = bat('mid', true);
ART['battery.none'] = bat('none', false);

ART['display.laptop'] = wrap(
  '<rect class="l" x="8" y="8" width="40" height="28" rx="3"/>' +
    '<path class="m" d="M18 40h20"/>'
);
ART['display.external'] = wrap(
  '<rect class="l" x="6" y="10" width="44" height="30" rx="3"/>' +
    '<path class="l" d="M22 40h12v6H22z"/>' +
    '<path class="m" d="M16 48h24"/>'
);
ART['display.multiple'] = wrap(
  '<rect class="l" x="4" y="14" width="28" height="22" rx="2"/>' +
    '<rect class="l" x="24" y="10" width="28" height="26" rx="2"/>' +
    '<path class="m" d="M14 40h8M34 40h8"/>'
);

ART['camera.webcam'] = wrap(
  '<circle class="l" cx="28" cy="22" r="17"/>' +
    '<circle class="m" cx="28" cy="22" r="10"/>' +
    '<circle class="f" cx="28" cy="22" r="5.5"/>' +
    '<circle cx="30.5" cy="19.5" r="1.6" fill="var(--art-fill)"/>' +
    '<path class="l" d="M28 39V46M17 49H39"/>'
);
ART['camera.builtin'] = wrap(
  '<rect class="l" x="10" y="12" width="36" height="26" rx="4"/>' +
    '<circle class="f" cx="28" cy="25" r="4" opacity="0.5"/>' +
    '<path class="m" d="M22 42h12"/>'
);
ART['camera.none'] = wrap(
  '<circle class="l" cx="28" cy="28" r="16"/>' +
    '<path class="md" d="M16 16l24 24"/>'
);

ART['os.windows11'] = wrap(
  '<path class="f" d="M7 10L25 7V25H7Z"/>' +
    '<path class="l" d="M30 6.5L49 4V25H30Z"/>' +
    '<path class="l" d="M7 30H25V48.5L7 46Z"/>' +
    '<path class="l" d="M30 30H49V52L30 49.5Z"/>'
);
ART['os.windows10'] = wrap(
  '<path class="f" d="M7 10L25 7V25H7Z" opacity="0.5"/>' +
    '<path class="l" d="M30 6.5L49 4V25H30Z"/>' +
    '<path class="l" d="M7 30H25V48.5L7 46Z"/>' +
    '<path class="l" d="M30 30H49V52L30 49.5Z"/>' +
    '<text x="28" y="30" text-anchor="middle" fill="var(--art-line)" font-size="5.5" font-weight="600">10</text>'
);

function makeCpuArt(opts) {
  opts = opts || {};
  const v = String(opts.vendor || '').toLowerCase();
  const fam = opts.family || '';
  const gen = opts.generation || '';
  if (v.indexOf('intel') >= 0) return cpuIntel(fam || 'Core i7', gen || 'Gen');
  if (v.indexOf('amd') >= 0) return cpuAmd(fam || 'Ryzen 7', gen || 'Series');
  if (v.indexOf('apple') >= 0) return cpuApple(fam || 'M2', gen);
  if (v.indexOf('qualcomm') >= 0) return cpuMobile(fam || 'X', gen || 'Elite');
  if (v.indexOf('arm') >= 0) return cpuArm(fam || 'ARM', gen);
  return cpuArm(fam || 'CPU', gen);
}

function makeGpuArt(opts) {
  opts = opts || {};
  const v = String(opts.vendor || '').toLowerCase();
  const name = String(opts.name || '').toLowerCase();
  if (opts.integrated) return gpuIntegrated();
  if (
    v.indexOf('nvidia') >= 0 ||
    name.indexOf('geforce') >= 0 ||
    name.indexOf('rtx') >= 0
  ) {
    return gpuCard(2, 'RTX');
  }
  if (v.indexOf('amd') >= 0 || name.indexOf('radeon') >= 0) {
    return gpuCard(2, 'Radeon');
  }
  if (v.indexOf('intel') >= 0 || name.indexOf('arc') >= 0) {
    return gpuCard(2, 'Arc');
  }
  return gpuCard(1);
}

function norm(s) {
  return String(s || '').toLowerCase();
}

function pickArt(kind, facts) {
  facts = facts || {};
  const n = norm(facts.name);
  const v = norm(facts.vendor);
  const k = norm(kind);
  if (k === 'cpu' || k === 'processor') {
    if (n.indexOf('threadripper') >= 0) return 'cpu.amd.threadripper';
    if (n.indexOf('athlon') >= 0) return 'cpu.amd.athlon';
    if (n.indexOf('ryzen ai') >= 0) return 'cpu.amd.ryzen.ai';
    if (n.indexOf('ryzen 9') >= 0) return 'cpu.amd.ryzen.9';
    if (n.indexOf('ryzen 7') >= 0) return 'cpu.amd.ryzen.7';
    if (n.indexOf('ryzen 5') >= 0) return 'cpu.amd.ryzen.5';
    if (n.indexOf('ryzen 3') >= 0) return 'cpu.amd.ryzen.3';
    if (v.indexOf('amd') >= 0 && n.indexOf('ryzen') >= 0) return 'cpu.amd.ryzen.7';
    if (n.indexOf('core ultra') >= 0 || n.indexOf('ultra 7') >= 0) {
      return 'cpu.intel.core.ultra';
    }
    if (n.indexOf('core i9') >= 0) return 'cpu.intel.core.i9';
    if (n.indexOf('core i7') >= 0) return 'cpu.intel.core.i7';
    if (n.indexOf('core i5') >= 0) return 'cpu.intel.core.i5';
    if (n.indexOf('core i3') >= 0) return 'cpu.intel.core.i3';
    if (n.indexOf('celeron') >= 0) return 'cpu.intel.celeron';
    if (n.indexOf('pentium') >= 0) return 'cpu.intel.pentium';
    if (n.indexOf('x elite') >= 0) return 'cpu.qualcomm.snapdragon.x.elite';
    if (n.indexOf('x plus') >= 0) return 'cpu.qualcomm.snapdragon.x.plus';
    if (n.indexOf('8cx') >= 0) return 'cpu.qualcomm.snapdragon.8cx';
    if (n.indexOf('7c') >= 0) return 'cpu.qualcomm.snapdragon.7c';
    const apple = n.match(/\bm([1-4])(\s+pro|\s+max|\s+ultra)?\b/);
    if (apple || v.indexOf('apple') >= 0) {
      const m = apple ? apple[1] : '2';
      let tier = '';
      if (n.indexOf('ultra') >= 0) tier = '.ultra';
      else if (n.indexOf('max') >= 0) tier = '.max';
      else if (n.indexOf('pro') >= 0) tier = '.pro';
      const key = 'cpu.apple.m' + m + tier;
      if (HARDWARE_ART[key]) return key;
      return 'cpu.apple.m' + m;
    }
    if (v.indexOf('qualcomm') >= 0) return 'cpu.qualcomm.snapdragon.x.elite';
    if (n.indexOf('arm') >= 0 || v.indexOf('arm') >= 0) return 'cpu.arm.generic';
    return 'cpu.generic';
  }
  if (k === 'gpu' || k === 'graphics') {
    if (facts.integrated === true) return 'gpu.integrated';
    if (
      facts.integrated !== false &&
      (n.indexOf('radeon graphics') >= 0 ||
        n.indexOf('uhd') >= 0 ||
        n.indexOf('iris') >= 0 ||
        n.indexOf('vega') >= 0 ||
        /\bgraphics\b/.test(n))
    ) {
      const dedicated =
        facts.dedicated === true ||
        n.indexOf('rtx') >= 0 ||
        n.indexOf('geforce') >= 0 ||
        n.indexOf('laptop gpu') >= 0;
      if (!dedicated) return 'gpu.integrated';
    }
    if (v.indexOf('nvidia') >= 0 || n.indexOf('geforce') >= 0 || n.indexOf('rtx') >= 0) {
      return 'gpu.nvidia.rtx';
    }
    if (v.indexOf('amd') >= 0 || n.indexOf('radeon') >= 0) return 'gpu.amd.radeon';
    if (n.indexOf('arc') >= 0) return 'gpu.intel.arc';
    return 'gpu.generic';
  }
  if (k === 'device') {
    if (n.indexOf('macbook') >= 0) return 'device.macbook';
    if (n.indexOf('gaming') >= 0) return 'device.laptop.gaming';
    if (n.indexOf('tablet') >= 0 || n.indexOf('2-in-1') >= 0) return 'device.tablet';
    if (n.indexOf('all-in-one') >= 0 || n.indexOf('aio') >= 0) return 'device.aio';
    if (n.indexOf('desktop') >= 0 || n.indexOf('tower') >= 0) return 'device.desktop';
    if (n.indexOf('laptop') >= 0) return 'device.laptop';
    return 'device.pc.generic';
  }
  if (k === 'memory') {
    if (n.indexOf('lpddr') >= 0) return 'memory.lpddr';
    if (n.indexOf('so-dimm') >= 0 || n.indexOf('sodimm') >= 0) return 'memory.sodimm';
    if (n.indexOf('ddr5') >= 0) return 'memory.ddr5';
    return 'memory.ddr4';
  }
  if (k === 'storage' || k === 'drive') {
    if (facts.bus === 'usb' || n.indexOf('external') >= 0 || n.indexOf('removable') >= 0) {
      return 'storage.usb.external';
    }
    if (n.indexOf('nvme') >= 0 || n.indexOf('m.2') >= 0) return 'storage.nvme';
    if (n.indexOf('ssd') >= 0) return 'storage.sata.ssd';
    if (n.indexOf('hdd') >= 0 || n.indexOf('hard') >= 0) return 'storage.hdd';
    return 'storage.nvme';
  }
  if (k === 'battery') {
    if (facts.present === false) return 'battery.none';
    if (facts.charging) return 'battery.charging';
    const pct = facts.percent;
    if (pct != null && pct < 25) return 'battery.low';
    if (pct != null && pct < 60) return 'battery.mid';
    return 'battery.full';
  }
  if (k === 'display') {
    if (facts.count > 1) return 'display.multiple';
    if (facts.external) return 'display.external';
    return 'display.laptop';
  }
  if (k === 'camera') {
    if (facts.present === false) return 'camera.none';
    if (facts.builtin !== false) return 'camera.builtin';
    return 'camera.webcam';
  }
  if (k === 'os') {
    if (n.indexOf('11') >= 0) return 'os.windows11';
    return 'os.windows10';
  }
  return 'cpu.generic';
}

const fnBundle = [
  esc,
  wrap,
  text,
  pinsIntel,
  cpuIntel,
  cpuAmd,
  cpuWide,
  cpuMobile,
  cpuApple,
  cpuArm,
  gpuCard,
  gpuIntegrated,
  makeCpuArt,
  makeGpuArt,
  norm,
  pickArt,
]
  .map((fn) => fn.toString())
  .join('\n\n');

const out =
  '/* Quantum Updater hardware illustration library */\n' +
  '(function (root) {\n' +
  '  "use strict";\n\n' +
  '  var HARDWARE_ART = ' +
  JSON.stringify(ART, null, 2) +
  ';\n\n' +
  fnBundle +
  '\n\n' +
  '  root.HARDWARE_ART = HARDWARE_ART;\n' +
  '  root.makeCpuArt = makeCpuArt;\n' +
  '  root.makeGpuArt = makeGpuArt;\n' +
  '  root.pickArt = pickArt;\n' +
  '})(typeof window !== "undefined" ? window : global);\n';

const target = path.join(__dirname, 'parts.js');
fs.writeFileSync(target, out, 'utf8');
console.log('Wrote', target, 'keys:', Object.keys(ART).length);
