// Generates src/assets/tray.png (32x32) — Electron's nativeImage cannot decode
// SVG data URLs, so the tray icon must be a real PNG. Pure Node, no deps.
const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');

const SIZE = 32;
const SS = 4; // supersampling factor for anti-aliasing
const INK = [23, 32, 20]; // #172014

// The tray icon's background colour encodes how much of the 5-hour window is
// left (see README); the neutral lime variant is used when usage is unknown.
const PALETTE = {
  'tray.png': '#b8f36b',
  'tray-red.png': '#e5484d',
  'tray-orange.png': '#f5813c',
  'tray-yellow.png': '#f0c93a',
  'tray-green.png': '#3da35b'
};

const parseHex = (hex) => [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));

const CRC_TABLE = (() => {
  const table = new Int32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    table[n] = c;
  }
  return table;
})();

function crc32(buffer) {
  let c = 0xffffffff;
  for (const byte of buffer) c = CRC_TABLE[(c ^ byte) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const out = Buffer.alloc(8 + data.length + 4);
  out.writeUInt32BE(data.length, 0);
  out.write(type, 4, 'ascii');
  data.copy(out, 8);
  out.writeUInt32BE(crc32(out.subarray(4, 8 + data.length)), 8 + data.length);
  return out;
}

function encodePng(width, height, rgba) {
  const raw = Buffer.alloc(height * (1 + width * 4));
  for (let y = 0; y < height; y++) {
    raw[y * (1 + width * 4)] = 0; // filter: none
    rgba.copy(raw, y * (1 + width * 4) + 1, y * width * 4, (y + 1) * width * 4);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8; // bit depth
  ihdr[9] = 6; // color type: RGBA
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0))
  ]);
}

function coverage(sd, edge) {
  return Math.max(0, Math.min(1, 0.5 - sd / edge));
}

// Signed distance helpers
const sdRoundBox = (px, py, half, radius) => {
  const qx = Math.abs(px) - half + radius;
  const qy = Math.abs(py) - half + radius;
  return Math.min(Math.max(qx, qy), 0) + Math.hypot(Math.max(qx, 0), Math.max(qy, 0)) - radius;
};
// Distance to a circular arc band: ring radius r, angular half-width, center angle.
const sdArcBand = (px, py, r, halfAngle, centerAngle, strokeHalf) => {
  const dist = Math.hypot(px, py) - r;
  let delta = Math.atan2(py, px) - centerAngle;
  while (delta > Math.PI) delta -= 2 * Math.PI;
  while (delta < -Math.PI) delta += 2 * Math.PI;
  if (Math.abs(delta) <= halfAngle) return Math.abs(dist) - strokeHalf;
  const ex = r * Math.cos(centerAngle + Math.sign(delta) * halfAngle);
  const ey = r * Math.sin(centerAngle + Math.sign(delta) * halfAngle);
  return Math.hypot(px - ex, py - ey) - strokeHalf;
};

function sample(u, v, edge, bg) {
  // u, v in [-1, 1]; rounded-square background
  const bgA = coverage(sdRoundBox(u, v, 0.86, 0.22), edge);
  if (bgA <= 0) return null;
  // Two opposing arcs forming the refresh glyph (matches the original SVG shape).
  const strokeHalf = 0.095;
  const ring = 0.44;
  const top = sdArcBand(u, v, ring, Math.PI * 0.42, -Math.PI / 2, strokeHalf);
  const bottom = sdArcBand(u, v, ring, Math.PI * 0.42, Math.PI / 2, strokeHalf);
  const inkA = coverage(Math.min(top, bottom), edge);
  const r = bg[0] + (INK[0] - bg[0]) * inkA;
  const g = bg[1] + (INK[1] - bg[1]) * inkA;
  const b = bg[2] + (INK[2] - bg[2]) * inkA;
  return [r, g, b, bgA];
}

function renderRgba(bg) {
  const rgba = Buffer.alloc(SIZE * SIZE * 4);
  const edge = 1 / SS; // soft edge in supersample units
  for (let y = 0; y < SIZE; y++) {
    for (let x = 0; x < SIZE; x++) {
      let r = 0, g = 0, b = 0, a = 0;
      for (let sy = 0; sy < SS; sy++) {
        for (let sx = 0; sx < SS; sx++) {
          const u = ((x + (sx + 0.5) / SS) / SIZE) * 2 - 1;
          const v = ((y + (sy + 0.5) / SS) / SIZE) * 2 - 1;
          const s = sample(u, v, edge, bg);
          if (s) { r += s[0] * s[3]; g += s[1] * s[3]; b += s[2] * s[3]; }
          a += s ? s[3] : 0;
        }
      }
      const n = SS * SS;
      r /= n; g /= n; b /= n; a /= n;
      const i = (y * SIZE + x) * 4;
      rgba[i] = a > 0 ? Math.round(r / a) : 0;
      rgba[i + 1] = a > 0 ? Math.round(g / a) : 0;
      rgba[i + 2] = a > 0 ? Math.round(b / a) : 0;
      rgba[i + 3] = Math.round(a * 255);
    }
  }
  return rgba;
}

const dir = path.join(__dirname, '..', 'src', 'assets');
fs.mkdirSync(dir, { recursive: true });
for (const [name, hex] of Object.entries(PALETTE)) {
  const out = path.join(dir, name);
  fs.writeFileSync(out, encodePng(SIZE, SIZE, renderRgba(parseHex(hex))));
  console.log(`Wrote ${out} (${fs.statSync(out).size} bytes)`);
}
