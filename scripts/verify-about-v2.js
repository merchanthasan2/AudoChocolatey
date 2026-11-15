'use strict';
const path = require('path');
const vm = require('vm');
const fs = require('fs');

const root = path.join(__dirname, '..');
const artPath = path.join(root, 'web', 'hardware-art.js');
const appPath = path.join(root, 'web', 'app.js');

function loadArt() {
  const code = fs.readFileSync(artPath, 'utf8');
  const ctx = { window: {}, globalThis: {} };
  ctx.window = ctx.globalThis;
  vm.runInNewContext(code, ctx);
  return ctx.window;
}

function loadBuilder() {
  const code = fs.readFileSync(appPath, 'utf8');
  const start = code.indexOf('function buildAboutComputerPageHtml');
  const end = code.indexOf('if (typeof window !== \'undefined\')', start);
  if (start < 0 || end < 0) throw new Error('buildAboutComputerPageHtml not found');
  const preludeEnd = code.lastIndexOf('const ABOUT_NR_ICON', start);
  const slice = code.slice(preludeEnd, end);
  const art = loadArt();
  const ctx = {
    ...art,
    renderArt: art.renderArt,
    pickArt: art.pickArt,
    makeCpuArt: art.makeCpuArt,
    parseCpuName: art.parseCpuName,
    parseGpuName: art.parseGpuName,
    HARDWARE_ART: art.HARDWARE_ART,
    window: {},
    console
  };
  vm.runInNewContext(slice, ctx);
  return { build: ctx.buildAboutComputerPageHtml, pickArt: art.pickArt, parseGpuName: art.parseGpuName };
}

const mockDiag = {
  model_name: 'ASUS TUF Gaming A15',
  model_id: 'FA506IC',
  arch: 'x64',
  memory: '16 GB',
  cpu: 'AMD Ryzen 7 4800H with Radeon Graphics',
  storage_free_str: '63.36 GB',
  storage_total_str: '453.24 GB',
  storage_used_percent: 86,
  hardware: {
    device: { kind: 'laptop', manufacturer: 'ASUS', model_name: 'ASUS TUF Gaming A15', model_id: 'FA506IC', serial_number: '' },
    cpu: {
      vendor: 'amd',
      brand_string: 'AMD Ryzen 7 4800H with Radeon Graphics',
      family: 'Ryzen 7',
      generation: 'Ryzen 4000 series (Zen 2, Renoir)',
      model_number: '4800H',
      cores: 8,
      threads: 16,
      arch: 'x64'
    },
    gpus: [
      { vendor: 'amd', name: 'AMD Radeon Graphics', integrated: true },
      { vendor: 'nvidia', name: 'NVIDIA GeForce RTX 3050 Laptop GPU', integrated: false, vram_bytes: 4294967296 }
    ],
    memory: { total_bytes: 17179869184, type: 'DDR4', speed_mhz: null, slots_used: 2 },
    storage: [
      { letter: 'C', label: 'Local Disk', fs: 'NTFS', total_bytes: 486539264000, free_bytes: 68018796544, media: 'NVMe SSD' }
    ],
    battery: { present: true, charge_percent: 67, health_percent: 88, on_ac: false, cycle_count: 142 },
    display: [{ name: 'Generic PnP Monitor', width: 1920, height: 1080, refresh_hz: 144, scale_percent: 125 }],
    camera: [],
    os: { name: 'Windows 11 Home Single Language', version: '23H2', build: '22631' }
  }
};

const { build, pickArt, parseGpuName } = loadBuilder();
const html = build(mockDiag, mockDiag.hardware.storage);

const deviceKey = pickArt('device', { model: 'ASUS TUF Gaming A15', isLaptop: true });
const gpuKeys = mockDiag.hardware.gpus.map((g) => parseGpuName(g.name).key);

const banned = ['Unknown', 'Winget', 'Chocolatey', 'MacBook', 'FaceTime'];
const missing = banned.filter((w) => html.includes(w));
const checks = [
  ['device-gaming-laptop', deviceKey === 'device-gaming-laptop'],
  ['RTX 3050 in HTML', /RTX 3050 Laptop GPU/i.test(html)],
  ['Radeon in HTML', /AMD Radeon Graphics/i.test(html)],
  ['CPU generation', /Ryzen 4000 series/i.test(html)],
  ['no banned strings', missing.length === 0],
  ['gpu-nvidia-rtx key', gpuKeys.includes('gpu-nvidia-rtx')],
  ['gpu-integrated key', gpuKeys.includes('gpu-integrated')]
];

let failed = false;
checks.forEach(([label, ok]) => {
  console.log(ok ? 'OK' : 'FAIL', label);
  if (!ok) failed = true;
});
if (missing.length) console.log('Banned found:', missing.join(', '));

process.exit(failed ? 1 : 0);
