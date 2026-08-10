import fs from "node:fs";
import path from "node:path";
import zlib from "node:zlib";
import crypto from "node:crypto";

const root = path.resolve(import.meta.dirname, "../..");
const assetsRoot = path.join(root, "Assets", "Art", "DesignSystem");

const palette = {
  void: "#8FD4FF",
  deep: "#EAF5FF",
  panel: "#FFF9EA",
  cyan: "#4E8FEF",
  cyanSoft: "#92C9FF",
  magenta: "#31599F",
  magentaSoft: "#CADBFF",
  purple: "#789ED7",
  violet: "#A9C5E8",
  mint: "#82B56D",
  gold: "#C7A45A",
  text: "#24304D",
};

const files = {
  skybox: "Textures/Skybox/T_CyberRaid_PanoramicSkybox.png",
  primaryButton: "Textures/UI/T_UI_Button_Primary.png",
  secondaryButton: "Textures/UI/T_UI_Button_Secondary.png",
  dangerButton: "Textures/UI/T_UI_Button_Danger.png",
  inputField: "Textures/UI/T_UI_Input_Field.png",
  raidPanel: "Textures/UI/T_UI_Panel_RaidFrame.png",
  logPanel: "Textures/UI/T_UI_Panel_LogFrame.png",
  statCard: "Textures/UI/T_UI_StatCard.png",
  progressFrame: "Textures/UI/T_UI_Progress_Frame.png",
  progressCyan: "Textures/UI/T_UI_ProgressFill_Cyan.png",
  progressMagenta: "Textures/UI/T_UI_ProgressFill_Magenta.png",
  hexBadge: "Textures/UI/T_UI_HexBadge_Frame.png",
  skyboxMaterial: "Materials/Skybox/M_CyberRaid_PanoramicSkybox.mat",
  tokens: "DesignTokens/RasshiineDesignTokens.json",
  readme: "README.md",
};

function relAsset(rel) {
  return path.join(assetsRoot, rel);
}

function unityGuid(projectRelativePath) {
  return crypto
    .createHash("md5")
    .update(`AttackOnRasshiine:${projectRelativePath.replaceAll(path.sep, "/")}`)
    .digest("hex");
}

function writeFile(filePath, data) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, data);
}

function writeText(filePath, text) {
  writeFile(filePath, `${text.trimEnd()}\n`);
}

function projectRel(filePath) {
  return path.relative(root, filePath).replaceAll(path.sep, "/");
}

function writeFolderMeta(dirPath) {
  const rel = projectRel(dirPath);
  const meta = `${dirPath}.meta`;
  if (fs.existsSync(meta)) return;
  writeText(
    meta,
    `fileFormatVersion: 2
guid: ${unityGuid(rel)}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:`
  );
}

function ensureAssetFolders() {
  const dirs = [
    "Assets/Art",
    "Assets/Art/DesignSystem",
    "Assets/Art/DesignSystem/DesignTokens",
    "Assets/Art/DesignSystem/Materials",
    "Assets/Art/DesignSystem/Materials/Skybox",
    "Assets/Art/DesignSystem/Textures",
    "Assets/Art/DesignSystem/Textures/Skybox",
    "Assets/Art/DesignSystem/Textures/UI",
  ];
  for (const dir of dirs) {
    const full = path.join(root, dir);
    fs.mkdirSync(full, { recursive: true });
    writeFolderMeta(full);
  }
}

function hexToRgb(hex) {
  const raw = hex.replace("#", "");
  return {
    r: Number.parseInt(raw.slice(0, 2), 16),
    g: Number.parseInt(raw.slice(2, 4), 16),
    b: Number.parseInt(raw.slice(4, 6), 16),
  };
}

function mix(a, b, t) {
  return Math.round(a + (b - a) * t);
}

function makeCanvas(width, height, clear = { r: 0, g: 0, b: 0, a: 0 }) {
  const data = new Uint8Array(width * height * 4);
  for (let i = 0; i < data.length; i += 4) {
    data[i] = clear.r;
    data[i + 1] = clear.g;
    data[i + 2] = clear.b;
    data[i + 3] = clear.a;
  }
  return { width, height, data };
}

function blendPixel(canvas, x, y, color, alpha = 1) {
  const xi = Math.round(x);
  const yi = Math.round(y);
  if (xi < 0 || yi < 0 || xi >= canvas.width || yi >= canvas.height || alpha <= 0) return;
  const idx = (yi * canvas.width + xi) * 4;
  const srcA = Math.max(0, Math.min(1, (color.a ?? 255) / 255 * alpha));
  const dstA = canvas.data[idx + 3] / 255;
  const outA = srcA + dstA * (1 - srcA);
  if (outA <= 0) return;
  canvas.data[idx] = Math.round((color.r * srcA + canvas.data[idx] * dstA * (1 - srcA)) / outA);
  canvas.data[idx + 1] = Math.round((color.g * srcA + canvas.data[idx + 1] * dstA * (1 - srcA)) / outA);
  canvas.data[idx + 2] = Math.round((color.b * srcA + canvas.data[idx + 2] * dstA * (1 - srcA)) / outA);
  canvas.data[idx + 3] = Math.round(outA * 255);
}

function fillGradient(canvas, topHex, bottomHex) {
  const top = hexToRgb(topHex);
  const bottom = hexToRgb(bottomHex);
  for (let y = 0; y < canvas.height; y += 1) {
    const t = y / (canvas.height - 1);
    for (let x = 0; x < canvas.width; x += 1) {
      const idx = (y * canvas.width + x) * 4;
      canvas.data[idx] = mix(top.r, bottom.r, t);
      canvas.data[idx + 1] = mix(top.g, bottom.g, t);
      canvas.data[idx + 2] = mix(top.b, bottom.b, t);
      canvas.data[idx + 3] = 255;
    }
  }
}

function fillRect(canvas, x, y, width, height, color, alpha = 1) {
  const col = typeof color === "string" ? hexToRgb(color) : color;
  const x0 = Math.max(0, Math.floor(x));
  const y0 = Math.max(0, Math.floor(y));
  const x1 = Math.min(canvas.width, Math.ceil(x + width));
  const y1 = Math.min(canvas.height, Math.ceil(y + height));
  for (let py = y0; py < y1; py += 1) {
    for (let px = x0; px < x1; px += 1) blendPixel(canvas, px, py, col, alpha);
  }
}

function inPolygon(x, y, points) {
  let inside = false;
  for (let i = 0, j = points.length - 1; i < points.length; j = i, i += 1) {
    const xi = points[i][0];
    const yi = points[i][1];
    const xj = points[j][0];
    const yj = points[j][1];
    const intersect = yi > y !== yj > y && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi;
    if (intersect) inside = !inside;
  }
  return inside;
}

function fillPolygon(canvas, points, color, alpha = 1) {
  const col = typeof color === "string" ? hexToRgb(color) : color;
  const minX = Math.max(0, Math.floor(Math.min(...points.map((p) => p[0]))));
  const maxX = Math.min(canvas.width - 1, Math.ceil(Math.max(...points.map((p) => p[0]))));
  const minY = Math.max(0, Math.floor(Math.min(...points.map((p) => p[1]))));
  const maxY = Math.min(canvas.height - 1, Math.ceil(Math.max(...points.map((p) => p[1]))));
  for (let y = minY; y <= maxY; y += 1) {
    for (let x = minX; x <= maxX; x += 1) {
      if (inPolygon(x + 0.5, y + 0.5, points)) blendPixel(canvas, x, y, col, alpha);
    }
  }
}

function pointSegmentDistance(px, py, x1, y1, x2, y2) {
  const dx = x2 - x1;
  const dy = y2 - y1;
  if (dx === 0 && dy === 0) return Math.hypot(px - x1, py - y1);
  const t = Math.max(0, Math.min(1, ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy)));
  return Math.hypot(px - (x1 + t * dx), py - (y1 + t * dy));
}

function line(canvas, x1, y1, x2, y2, color, thickness = 1, alpha = 1) {
  const col = typeof color === "string" ? hexToRgb(color) : color;
  const pad = thickness + 1;
  const minX = Math.max(0, Math.floor(Math.min(x1, x2) - pad));
  const maxX = Math.min(canvas.width - 1, Math.ceil(Math.max(x1, x2) + pad));
  const minY = Math.max(0, Math.floor(Math.min(y1, y2) - pad));
  const maxY = Math.min(canvas.height - 1, Math.ceil(Math.max(y1, y2) + pad));
  for (let y = minY; y <= maxY; y += 1) {
    for (let x = minX; x <= maxX; x += 1) {
      const d = pointSegmentDistance(x + 0.5, y + 0.5, x1, y1, x2, y2);
      if (d <= thickness / 2) blendPixel(canvas, x, y, col, alpha * (1 - d / (thickness / 2 + 0.001)));
    }
  }
}

function glowLine(canvas, x1, y1, x2, y2, color, thickness = 2, alpha = 1) {
  line(canvas, x1, y1, x2, y2, color, thickness * 7, alpha * 0.07);
  line(canvas, x1, y1, x2, y2, color, thickness * 3.5, alpha * 0.14);
  line(canvas, x1, y1, x2, y2, color, thickness, alpha);
}

function polyline(canvas, points, color, thickness = 1, alpha = 1, glow = false) {
  for (let i = 1; i < points.length; i += 1) {
    const draw = glow ? glowLine : line;
    draw(canvas, points[i - 1][0], points[i - 1][1], points[i][0], points[i][1], color, thickness, alpha);
  }
}

function ellipse(canvas, cx, cy, rx, ry, color, thickness = 1, alpha = 1, glow = false, start = 0, end = Math.PI * 2) {
  const steps = 180;
  const points = [];
  for (let i = 0; i <= steps; i += 1) {
    const t = start + ((end - start) * i) / steps;
    points.push([cx + Math.cos(t) * rx, cy + Math.sin(t) * ry]);
  }
  polyline(canvas, points, color, thickness, alpha, glow);
}

function rng(seed) {
  let state = seed >>> 0;
  return () => {
    state = (state * 1664525 + 1013904223) >>> 0;
    return state / 0x100000000;
  };
}

function cutRectPoints(x, y, width, height, cut) {
  return [
    [x + cut, y],
    [x + width - cut, y],
    [x + width, y + cut],
    [x + width, y + height - cut],
    [x + width - cut, y + height],
    [x + cut, y + height],
    [x, y + height - cut],
    [x, y + cut],
    [x + cut, y],
  ];
}

function insideRoundedRect(px, py, x, y, width, height, radius) {
  if (px < x || py < y || px > x + width || py > y + height) return false;
  const cx = Math.max(x + radius, Math.min(px, x + width - radius));
  const cy = Math.max(y + radius, Math.min(py, y + height - radius));
  return Math.hypot(px - cx, py - cy) <= radius + 0.5;
}

function fillRoundedRect(canvas, x, y, width, height, radius, color, alpha = 1) {
  const col = typeof color === "string" ? hexToRgb(color) : color;
  const x0 = Math.max(0, Math.floor(x));
  const y0 = Math.max(0, Math.floor(y));
  const x1 = Math.min(canvas.width - 1, Math.ceil(x + width));
  const y1 = Math.min(canvas.height - 1, Math.ceil(y + height));
  for (let py = y0; py <= y1; py += 1) {
    for (let px = x0; px <= x1; px += 1) {
      if (insideRoundedRect(px + 0.5, py + 0.5, x, y, width, height, radius)) blendPixel(canvas, px, py, col, alpha);
    }
  }
}

function fillRoundedVerticalGradient(canvas, x, y, width, height, radius, topHex, bottomHex, alpha = 1) {
  const top = hexToRgb(topHex);
  const bottom = hexToRgb(bottomHex);
  const x0 = Math.max(0, Math.floor(x));
  const y0 = Math.max(0, Math.floor(y));
  const x1 = Math.min(canvas.width - 1, Math.ceil(x + width));
  const y1 = Math.min(canvas.height - 1, Math.ceil(y + height));
  for (let py = y0; py <= y1; py += 1) {
    const t = (py - y) / Math.max(1, height);
    const col = { r: mix(top.r, bottom.r, t), g: mix(top.g, bottom.g, t), b: mix(top.b, bottom.b, t) };
    for (let px = x0; px <= x1; px += 1) {
      if (insideRoundedRect(px + 0.5, py + 0.5, x, y, width, height, radius)) blendPixel(canvas, px, py, col, alpha);
    }
  }
}

function strokeRoundedRect(canvas, x, y, width, height, radius, color, thickness = 2, alpha = 1) {
  line(canvas, x + radius, y, x + width - radius, y, color, thickness, alpha);
  line(canvas, x + radius, y + height, x + width - radius, y + height, color, thickness, alpha);
  line(canvas, x, y + radius, x, y + height - radius, color, thickness, alpha);
  line(canvas, x + width, y + radius, x + width, y + height - radius, color, thickness, alpha);
  ellipse(canvas, x + radius, y + radius, radius, radius, color, thickness, alpha, false, Math.PI, Math.PI * 1.5);
  ellipse(canvas, x + width - radius, y + radius, radius, radius, color, thickness, alpha, false, Math.PI * 1.5, Math.PI * 2);
  ellipse(canvas, x + width - radius, y + height - radius, radius, radius, color, thickness, alpha, false, 0, Math.PI * 0.5);
  ellipse(canvas, x + radius, y + height - radius, radius, radius, color, thickness, alpha, false, Math.PI * 0.5, Math.PI);
}

function diamond(canvas, cx, cy, radius, color, alpha = 1) {
  fillPolygon(canvas, [
    [cx, cy - radius],
    [cx + radius, cy],
    [cx, cy + radius],
    [cx - radius, cy],
  ], color, alpha);
}

function leaf(canvas, cx, cy, scale, flip = 1) {
  fillPolygon(canvas, [
    [cx, cy],
    [cx + 34 * scale * flip, cy - 13 * scale],
    [cx + 56 * scale * flip, cy],
    [cx + 34 * scale * flip, cy + 13 * scale],
  ], palette.mint, 0.45);
  line(canvas, cx + 8 * scale * flip, cy, cx + 46 * scale * flip, cy, palette.mint, Math.max(1, scale * 3), 0.46);
}

function fantasyPanel(canvas, x, y, width, height, options = {}) {
  const radius = options.radius ?? Math.min(width, height) * 0.13;
  fillRoundedRect(canvas, x + 8, y + 12, width, height, radius, "#9C7C34", 0.16);
  fillRoundedVerticalGradient(canvas, x, y, width, height, radius, options.top ?? "#FFFDF4", options.bottom ?? "#F3EBD3", options.alpha ?? 0.96);
  strokeRoundedRect(canvas, x, y, width, height, radius, palette.gold, options.border ?? 3, 0.78);
  strokeRoundedRect(canvas, x + 12, y + 12, width - 24, height - 24, Math.max(8, radius - 10), palette.cyanSoft, 1.5, 0.38);
  line(canvas, x + width * 0.18, y + 30, x + width * 0.82, y + 30, palette.gold, 1.2, 0.20);
  line(canvas, x + width * 0.18, y + height - 30, x + width * 0.82, y + height - 30, palette.gold, 1.2, 0.16);
  if (!options.quiet) {
    diamond(canvas, x + width / 2, y + 30, 7, palette.gold, 0.62);
    leaf(canvas, x + 38, y + height - 34, 0.36, 1);
    leaf(canvas, x + width - 38, y + height - 34, 0.36, -1);
  }
}

function techPanel(canvas, x, y, width, height, options = {}) {
  const cut = options.cut ?? Math.min(width, height) * 0.1;
  const fill = options.fill ?? palette.panel;
  const borderA = options.borderA ?? palette.cyan;
  const borderB = options.borderB ?? palette.magenta;
  const fillAlpha = options.fillAlpha ?? 0.68;
  const points = cutRectPoints(x, y, width, height, cut);
  fillPolygon(canvas, points, fill, fillAlpha);
  polyline(canvas, points, borderA, options.border ?? 2, 0.85, true);
  polyline(canvas, [
    [x + cut, y],
    [x + width * 0.5, y],
    [x + width - cut, y],
  ], borderB, options.border ?? 2, 0.78, true);
  polyline(canvas, [
    [x + width, y + cut],
    [x + width, y + height - cut],
    [x + width - cut, y + height],
  ], borderB, options.border ?? 2, 0.72, true);
  for (let i = 0; i < 7; i += 1) {
    const tx = x + cut + (i / 7) * (width - cut * 2);
    line(canvas, tx, y + height - 10, tx + width * 0.05, y + height - 10, borderA, 1, 0.25);
  }
}

function addScanlines(canvas, alpha = 0.035) {
  for (let y = 0; y < canvas.height; y += 4) {
    fillRect(canvas, 0, y, canvas.width, 1, "#FFFFFF", alpha);
  }
}

function pngChunk(type, data) {
  const typeBuffer = Buffer.from(type);
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length, 0);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(Buffer.concat([typeBuffer, data])), 0);
  return Buffer.concat([length, typeBuffer, data, crc]);
}

let crcTable;
function crc32(buffer) {
  if (!crcTable) {
    crcTable = new Uint32Array(256);
    for (let n = 0; n < 256; n += 1) {
      let c = n;
      for (let k = 0; k < 8; k += 1) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
      crcTable[n] = c >>> 0;
    }
  }
  let c = 0xffffffff;
  for (const byte of buffer) c = crcTable[(c ^ byte) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function encodePng(canvas) {
  const raw = Buffer.alloc((canvas.width * 4 + 1) * canvas.height);
  for (let y = 0; y < canvas.height; y += 1) {
    const row = y * (canvas.width * 4 + 1);
    raw[row] = 0;
    Buffer.from(canvas.data.buffer, y * canvas.width * 4, canvas.width * 4).copy(raw, row + 1);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(canvas.width, 0);
  ihdr.writeUInt32BE(canvas.height, 4);
  ihdr[8] = 8;
  ihdr[9] = 6;
  ihdr[10] = 0;
  ihdr[11] = 0;
  ihdr[12] = 0;
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    pngChunk("IHDR", ihdr),
    pngChunk("IDAT", zlib.deflateSync(raw, { level: 9 })),
    pngChunk("IEND", Buffer.alloc(0)),
  ]);
}

function generateSkybox() {
  const canvas = makeCanvas(2048, 1024);
  fillGradient(canvas, "#76C6FF", "#DDF5FF");
  const random = rng(0x51a7);
  const horizon = 610;

  for (let i = 0; i < 34; i += 1) {
    const x = random() * canvas.width;
    const y = 78 + random() * 260;
    const w = 120 + random() * 230;
    const h = 28 + random() * 52;
    fillRoundedRect(canvas, x, y, w, h, h / 2, "#FFFFFF", 0.46);
    fillRoundedRect(canvas, x + w * 0.16, y - h * 0.45, w * 0.55, h * 1.15, h * 0.55, "#FFFFFF", 0.36);
  }

  for (let i = 0; i < 14; i += 1) {
    const x = i * 165 - 90;
    fillPolygon(canvas, [
      [x, horizon + 20],
      [x + 190, 240 + random() * 95],
      [x + 420, horizon + 24],
    ], i % 2 ? "#8BB0CF" : "#A0C2DB", 0.72);
    fillPolygon(canvas, [
      [x + 150, 300],
      [x + 190, 240 + random() * 95],
      [x + 238, 305],
    ], "#F6FBFF", 0.72);
  }

  fillRect(canvas, 0, horizon - 34, canvas.width, 80, "#8FCB75", 0.85);
  fillRect(canvas, 0, horizon + 34, canvas.width, canvas.height - horizon, "#63AD58", 0.92);
  for (let i = 0; i < 28; i += 1) {
    const y = horizon + i * 17;
    ellipse(canvas, canvas.width / 2, y, 760 + i * 55, 42 + i * 5, "#7DBE68", 1, 0.16, false, Math.PI, Math.PI * 2);
  }

  for (let i = 0; i < 110; i += 1) {
    const x = random() * canvas.width;
    const y = horizon + random() * (canvas.height - horizon);
    line(canvas, x, y, x + 10 + random() * 30, y - 8 - random() * 16, random() > 0.7 ? "#F8E8A6" : "#DDF7CC", 1, 0.18);
  }
  return canvas;
}

function generateButton(kind) {
  const canvas = makeCanvas(768, 176);
  const fillTop = kind === "primary" ? "#6BA8F4" : kind === "danger" ? "#E7C975" : "#FFFDF4";
  const fillBottom = kind === "primary" ? "#3E7EDC" : kind === "danger" ? "#CBA24C" : "#F3EBD3";
  fillRoundedRect(canvas, 26, 30, 716, 118, 30, "#9C7C34", 0.18);
  fillRoundedVerticalGradient(canvas, 18, 20, 732, 124, 30, fillTop, fillBottom, 0.98);
  strokeRoundedRect(canvas, 18, 20, 732, 124, 30, palette.gold, 4, 0.78);
  strokeRoundedRect(canvas, 30, 32, 708, 100, 22, kind === "primary" ? "#DCEBFF" : palette.cyanSoft, 2, 0.36);
  line(canvas, 120, 38, 648, 38, "#FFFFFF", 2, kind === "primary" ? 0.42 : 0.56);
  line(canvas, 140, 124, 628, 124, palette.gold, 1.2, 0.22);
  diamond(canvas, 78, 82, 8, palette.gold, 0.58);
  diamond(canvas, 690, 82, 8, palette.gold, 0.58);
  if (kind !== "primary") {
    leaf(canvas, 96, 116, 0.28, 1);
    leaf(canvas, 672, 116, 0.28, -1);
  }
  return canvas;
}

function generateInputField() {
  const canvas = makeCanvas(768, 144);
  fillRoundedRect(canvas, 24, 28, 720, 88, 26, "#9C7C34", 0.12);
  fillRoundedVerticalGradient(canvas, 18, 20, 732, 88, 26, "#FFFFFF", "#F8F1DC", 0.96);
  strokeRoundedRect(canvas, 18, 20, 732, 88, 26, palette.gold, 3, 0.72);
  strokeRoundedRect(canvas, 30, 32, 708, 64, 20, palette.cyanSoft, 1.5, 0.32);
  line(canvas, 58, 42, 710, 42, "#FFFFFF", 1.5, 0.54);
  line(canvas, 58, 88, 710, 88, palette.gold, 1.2, 0.18);
  return canvas;
}

function generatePanel(width, height, mood) {
  const canvas = makeCanvas(width, height);
  fantasyPanel(canvas, 18, 18, width - 36, height - 36, {
    radius: mood === "raid" ? 42 : 34,
    alpha: mood === "raid" ? 0.93 : 0.90,
    border: 3,
  });
  const random = rng(mood === "raid" ? 0xc0de : 0x10d0);
  for (let i = 0; i < 22; i += 1) {
    const x = 50 + random() * (width - 100);
    const y = 54 + random() * (height - 108);
    const len = 16 + random() * 80;
    line(canvas, x, y, x + len, y, random() > 0.5 ? palette.cyanSoft : palette.gold, 1, 0.08);
  }
  return canvas;
}

function generateStatCard() {
  const canvas = makeCanvas(512, 192);
  fantasyPanel(canvas, 14, 14, 484, 164, { radius: 28, alpha: 0.88, border: 2.4 });
  fillRoundedRect(canvas, 42, 52, 170, 10, 5, palette.cyanSoft, 0.26);
  fillRoundedRect(canvas, 42, 80, 260, 10, 5, palette.gold, 0.16);
  line(canvas, 42, 124, 450, 124, palette.gold, 1.2, 0.24);
  return canvas;
}

function generateProgressFrame() {
  const canvas = makeCanvas(1024, 96);
  fantasyPanel(canvas, 8, 10, 1008, 76, { radius: 26, alpha: 0.9, border: 2.5 });
  fillRoundedRect(canvas, 46, 34, 932, 28, 14, "#C8D7E9", 0.44);
  return canvas;
}

function generateProgressFill(kind) {
  const canvas = makeCanvas(1024, 48);
  const a = hexToRgb(kind === "cyan" ? palette.cyan : palette.mint);
  const b = hexToRgb(kind === "cyan" ? palette.cyanSoft : "#B8D878");
  const c = hexToRgb("#FFFFFF");
  for (let y = 5; y < 43; y += 1) {
    for (let x = 14; x < 1010; x += 1) {
      const t = x / 1024;
      const shimmer = Math.max(0, 1 - Math.abs(y - 14) / 12) * 0.26;
      const col = {
        r: mix(mix(a.r, b.r, t), c.r, shimmer),
        g: mix(mix(a.g, b.g, t), c.g, shimmer),
        b: mix(mix(a.b, b.b, t), c.b, shimmer),
      };
      if (insideRoundedRect(x, y, 14, 5, 996, 38, 18)) blendPixel(canvas, x, y, col, 0.96);
    }
  }
  line(canvas, 34, 10, 990, 10, "#FFFFFF", 2, 0.42);
  line(canvas, 34, 40, 990, 40, kind === "cyan" ? palette.cyan : palette.mint, 1.5, 0.34);
  return canvas;
}

function generateHexBadge() {
  const canvas = makeCanvas(256, 256);
  const cx = 128;
  const cy = 128;
  const pts = [];
  const inner = [];
  for (let i = 0; i < 7; i += 1) {
    const a = -Math.PI / 2 + (Math.PI * 2 * i) / 6;
    pts.push([cx + Math.cos(a) * 104, cy + Math.sin(a) * 104]);
    inner.push([cx + Math.cos(a) * 76, cy + Math.sin(a) * 76]);
  }
  fillPolygon(canvas, pts, palette.panel, 0.9);
  polyline(canvas, pts, palette.gold, 5, 0.78, false);
  polyline(canvas, inner, palette.cyanSoft, 3, 0.42, false);
  ellipse(canvas, cx, cy, 50, 50, palette.gold, 2, 0.24, false);
  for (let i = 0; i < 6; i += 1) {
    line(canvas, cx, cy, inner[i][0], inner[i][1], i % 2 ? palette.cyanSoft : palette.gold, 1, 0.16);
  }
  return canvas;
}

function writeTextureMeta(rel, options = {}) {
  const filePath = relAsset(rel);
  const guid = unityGuid(projectRel(filePath));
  const isSprite = options.sprite ?? false;
  const border = options.border ?? "{x: 0, y: 0, z: 0, w: 0}";
  writeText(
    `${filePath}.meta`,
    `fileFormatVersion: 2
guid: ${guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 11
  mipmaps:
    mipMapMode: 0
    enableMipMap: ${options.mipmap ? 1 : 0}
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: ${options.wrapRepeat ? 0 : 1}
    wrapV: ${options.wrapRepeat ? 0 : 1}
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: ${isSprite ? 1 : 0}
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: ${border}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: ${isSprite ? 1 : 0}
  spriteTessellationDetail: -1
  textureType: ${isSprite ? 8 : 0}
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: ${isSprite ? 0 : 1}
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  spritePackingTag:
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData:
  assetBundleName:
  assetBundleVariant:`
  );
}

function writeDefaultMeta(rel) {
  const filePath = relAsset(rel);
  writeText(
    `${filePath}.meta`,
    `fileFormatVersion: 2
guid: ${unityGuid(projectRel(filePath))}
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:`
  );
}

function writeMaterialMeta(rel) {
  const filePath = relAsset(rel);
  writeText(
    `${filePath}.meta`,
    `fileFormatVersion: 2
guid: ${unityGuid(projectRel(filePath))}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 2100000
  userData:
  assetBundleName:
  assetBundleVariant:`
  );
}

function writeSkyboxMaterial() {
  const texGuid = unityGuid(projectRel(relAsset(files.skybox)));
  writeText(
    relAsset(files.skyboxMaterial),
    `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: M_Rasshiine_FantasyMeadowSkybox
  m_Shader: {fileID: 10304, guid: 0000000000000000f000000000000000, type: 0}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: {fileID: 2800000, guid: ${texGuid}, type: 3}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Ints: []
    m_Floats:
    - _Exposure: 1.08
    - _ImageType: 0
    - _Mapping: 1
    - _Rotation: 0
    m_Colors:
    - _Tint: {r: 0.9, g: 0.94, b: 1, a: 1}
  m_BuildTextureStacks: []`
  );
  writeMaterialMeta(files.skyboxMaterial);
}

function writeDesignTokens() {
  writeText(
    relAsset(files.tokens),
    JSON.stringify(
      {
        name: "AttackOnRasshiine Fantasy Raid Design System",
        source: "spec.md and selected fantasy RPG visual references",
        principles: [
          "bright fantasy RPG mood for a cooperative development raid",
          "soft sky blue, ivory, meadow green, and restrained gold accents",
          "large 3D boss arena with chibi member characters around the boss",
          "minimal HUD, readable Japanese typography, and clear primary actions",
          "avoid cyber-neon panels, dense dashboards, and decorative copy",
        ],
        colors: {
          void: palette.void,
          deep: palette.deep,
          panel: palette.panel,
          cyan: palette.cyan,
          cyanSoft: palette.cyanSoft,
          magenta: palette.magenta,
          magentaSoft: palette.magentaSoft,
          purple: palette.purple,
          violet: palette.violet,
          mint: palette.mint,
          gold: palette.gold,
          text: palette.text,
        },
        ui: {
          cornerCutPx: 24,
          preferredBorderPx: 2,
          buttonNineSliceBorder: { left: 64, bottom: 42, right: 64, top: 42 },
          inputNineSliceBorder: { left: 56, bottom: 38, right: 56, top: 38 },
          panelNineSliceBorder: { left: 72, bottom: 72, right: 72, top: 72 },
          progressNineSliceBorder: { left: 42, bottom: 24, right: 42, top: 24 },
        },
        assets: {
          skyboxMaterial: `Assets/Art/DesignSystem/${files.skyboxMaterial}`,
          skyboxTexture: `Assets/Art/DesignSystem/${files.skybox}`,
          buttons: [
            `Assets/Art/DesignSystem/${files.primaryButton}`,
            `Assets/Art/DesignSystem/${files.secondaryButton}`,
            `Assets/Art/DesignSystem/${files.dangerButton}`,
          ],
          input: `Assets/Art/DesignSystem/${files.inputField}`,
          panels: [
            `Assets/Art/DesignSystem/${files.raidPanel}`,
            `Assets/Art/DesignSystem/${files.logPanel}`,
            `Assets/Art/DesignSystem/${files.statCard}`,
          ],
          progress: [
            `Assets/Art/DesignSystem/${files.progressFrame}`,
            `Assets/Art/DesignSystem/${files.progressCyan}`,
            `Assets/Art/DesignSystem/${files.progressMagenta}`,
          ],
          badge: `Assets/Art/DesignSystem/${files.hexBadge}`,
        },
      },
      null,
      2
    )
  );
  writeDefaultMeta(files.tokens);
}

function writeReadme() {
  writeText(
    relAsset(files.readme),
    `# AttackOnRasshiine Design System

仕様書と選定済み生成案に合わせた、明るいファンタジーRPG調のUnity用デザインシステムです。

## Assets

- \`Materials/Skybox/M_CyberRaid_PanoramicSkybox.mat\`: 草原・山・青空を持つPanoramic skybox用マテリアル。
- \`Textures/Skybox/T_CyberRaid_PanoramicSkybox.png\`: 明るい草原レイド画面のための2:1背景テクスチャ。
- \`Textures/UI/T_UI_Button_*.png\`: Primary / Secondary / Dangerの9-slice前提ボタン。
- \`Textures/UI/T_UI_Input_Field.png\`: ログイン・開発ログ・申請フォーム用の入力欄。
- \`Textures/UI/T_UI_Panel_*.png\`: レイド演出用パネル、開発ログ用パネル、ステータスカード。
- \`Textures/UI/T_UI_Progress_*.png\`: HP/MP/進捗ゲージ用の枠とフィル。
- \`Textures/UI/T_UI_HexBadge_Frame.png\`: 評価・ランク・役割アイコンを載せる六角形フレーム。
- \`DesignTokens/RasshiineDesignTokens.json\`: 色、9-slice境界、利用方針のトークン。

## Usage Notes

- UIスプライトはSprite設定済みです。Image Typeは \`Sliced\` にして、ボタンは左右64px/上下42px、入力欄は左右56px/上下38px、パネルは72px、進捗枠は左右42px/上下24pxを基準にしてください。
- 背景はLighting SettingsのSkybox Material、またはScene内のVolume/Camera背景設定から \`M_CyberRaid_PanoramicSkybox\` を参照してください。
- 文字色は濃紺を基本にし、背景が明るい画面では淡いアイボリー面の上に配置してください。
- 開発ログやボス戦HUDは最小限の情報だけを初期表示し、詳細はボタンで展開してください。

## Regeneration

\`node Tools/DesignSystem/generate_design_system_assets.mjs\` で同じGUIDを保ったまま素材を再生成できます。`
  );
  writeDefaultMeta(files.readme);
}

function main() {
  ensureAssetFolders();
  writeFile(relAsset(files.skybox), encodePng(generateSkybox()));
  writeTextureMeta(files.skybox, { mipmap: true, wrapRepeat: true });

  const spriteBorderButton = "{x: 64, y: 42, z: 64, w: 42}";
  writeFile(relAsset(files.primaryButton), encodePng(generateButton("primary")));
  writeTextureMeta(files.primaryButton, { sprite: true, border: spriteBorderButton });
  writeFile(relAsset(files.secondaryButton), encodePng(generateButton("secondary")));
  writeTextureMeta(files.secondaryButton, { sprite: true, border: spriteBorderButton });
  writeFile(relAsset(files.dangerButton), encodePng(generateButton("danger")));
  writeTextureMeta(files.dangerButton, { sprite: true, border: spriteBorderButton });
  writeFile(relAsset(files.inputField), encodePng(generateInputField()));
  writeTextureMeta(files.inputField, { sprite: true, border: "{x: 56, y: 38, z: 56, w: 38}" });

  const spriteBorderPanel = "{x: 72, y: 72, z: 72, w: 72}";
  writeFile(relAsset(files.raidPanel), encodePng(generatePanel(1024, 384, "raid")));
  writeTextureMeta(files.raidPanel, { sprite: true, border: spriteBorderPanel });
  writeFile(relAsset(files.logPanel), encodePng(generatePanel(1024, 512, "log")));
  writeTextureMeta(files.logPanel, { sprite: true, border: spriteBorderPanel });
  writeFile(relAsset(files.statCard), encodePng(generateStatCard()));
  writeTextureMeta(files.statCard, { sprite: true, border: "{x: 48, y: 42, z: 48, w: 42}" });

  writeFile(relAsset(files.progressFrame), encodePng(generateProgressFrame()));
  writeTextureMeta(files.progressFrame, { sprite: true, border: "{x: 42, y: 24, z: 42, w: 24}" });
  writeFile(relAsset(files.progressCyan), encodePng(generateProgressFill("cyan")));
  writeTextureMeta(files.progressCyan, { sprite: true, border: "{x: 18, y: 12, z: 18, w: 12}" });
  writeFile(relAsset(files.progressMagenta), encodePng(generateProgressFill("magenta")));
  writeTextureMeta(files.progressMagenta, { sprite: true, border: "{x: 18, y: 12, z: 18, w: 12}" });

  writeFile(relAsset(files.hexBadge), encodePng(generateHexBadge()));
  writeTextureMeta(files.hexBadge, { sprite: true, border: "{x: 24, y: 24, z: 24, w: 24}" });

  writeSkyboxMaterial();
  writeDesignTokens();
  writeReadme();
}

main();
