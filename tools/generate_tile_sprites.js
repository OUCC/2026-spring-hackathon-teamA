/**
 * FLOOR BREAKER - Tile Sprite Generator
 * 32x32 pixel art sprites for each tile state.
 * Wood-themed floor with visible borders.
 */
const { PNG } = require("pngjs");
const fs = require("fs");
const path = require("path");

const SIZE = 32;
const OUT_DIR = path.join(__dirname, "..", "Assets", "App", "Features", "Stage", "Presentation", "Sprites");

// --- Color helpers ---
function rgba(r, g, b, a = 255) { return { r, g, b, a }; }
function lerp(a, b, t) { return Math.round(a + (b - a) * t); }
function lerpColor(c1, c2, t) {
  return rgba(lerp(c1.r, c2.r, t), lerp(c1.g, c2.g, t), lerp(c1.b, c2.b, t), lerp(c1.a, c2.a, t));
}

// Seeded RNG for deterministic output
function mulberry32(seed) {
  return function () {
    seed |= 0; seed = seed + 0x6D2B79F5 | 0;
    let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}

// --- PNG write helper ---
function createPng() {
  const png = new PNG({ width: SIZE, height: SIZE });
  return png;
}

function setPixel(png, x, y, color) {
  const idx = (y * SIZE + x) << 2;
  png.data[idx] = color.r;
  png.data[idx + 1] = color.g;
  png.data[idx + 2] = color.b;
  png.data[idx + 3] = color.a;
}

function fill(png, color) {
  for (let y = 0; y < SIZE; y++)
    for (let x = 0; x < SIZE; x++)
      setPixel(png, x, y, color);
}

function savePng(png, name) {
  const filePath = path.join(OUT_DIR, name + ".png");
  const buffer = PNG.sync.write(png);
  fs.writeFileSync(filePath, buffer);
  console.log("  Created: " + name + ".png");
}

// --- Border helper: draw a darker border around the tile ---
function drawBorder(png, borderColor, thickness) {
  for (let i = 0; i < thickness; i++) {
    for (let p = 0; p < SIZE; p++) {
      setPixel(png, p, i, borderColor);           // top
      setPixel(png, p, SIZE - 1 - i, borderColor); // bottom
      setPixel(png, i, p, borderColor);            // left
      setPixel(png, SIZE - 1 - i, p, borderColor); // right
    }
  }
}

// --- Wood grain noise ---
function drawWoodGrain(png, baseColor, grainColor, seed) {
  const rng = mulberry32(seed);
  // Fill base
  for (let y = 0; y < SIZE; y++) {
    for (let x = 0; x < SIZE; x++) {
      // Slight per-pixel variation
      const variation = (rng() - 0.5) * 12;
      const c = rgba(
        Math.max(0, Math.min(255, baseColor.r + variation)),
        Math.max(0, Math.min(255, baseColor.g + variation)),
        Math.max(0, Math.min(255, baseColor.b + variation)),
        baseColor.a
      );
      setPixel(png, x, y, c);
    }
  }

  // Horizontal grain lines (wood runs horizontally)
  for (let y = 0; y < SIZE; y++) {
    if ((y % 5 === 2 || y % 7 === 4) && rng() > 0.3) {
      const startX = Math.floor(rng() * 4);
      const endX = SIZE - Math.floor(rng() * 4);
      for (let x = startX; x < endX; x++) {
        if (rng() > 0.15) {
          const t = 0.25 + rng() * 0.15;
          const idx = (y * SIZE + x) << 2;
          const existing = rgba(png.data[idx], png.data[idx + 1], png.data[idx + 2], png.data[idx + 3]);
          setPixel(png, x, y, lerpColor(existing, grainColor, t));
        }
      }
    }
  }

  // Knot spots (small darker circles, 1-2 per tile)
  const knotCount = 1 + Math.floor(rng() * 2);
  for (let k = 0; k < knotCount; k++) {
    const cx = 6 + Math.floor(rng() * 20);
    const cy = 6 + Math.floor(rng() * 20);
    const r = 1 + Math.floor(rng() * 1.5);
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        if (dx * dx + dy * dy <= r * r) {
          const px = cx + dx, py = cy + dy;
          if (px >= 0 && px < SIZE && py >= 0 && py < SIZE) {
            const idx = (py * SIZE + px) << 2;
            const existing = rgba(png.data[idx], png.data[idx + 1], png.data[idx + 2], png.data[idx + 3]);
            setPixel(png, px, py, lerpColor(existing, grainColor, 0.45));
          }
        }
      }
    }
  }
}

// --- Crack pattern for collapsing tiles ---
function drawCracks(png, crackColor, seed) {
  const rng = mulberry32(seed);
  // Draw 3-4 crack lines
  const crackCount = 3 + Math.floor(rng() * 2);
  for (let c = 0; c < crackCount; c++) {
    let x = 4 + Math.floor(rng() * 24);
    let y = 4 + Math.floor(rng() * 24);
    const length = 6 + Math.floor(rng() * 10);
    for (let step = 0; step < length; step++) {
      if (x >= 1 && x < SIZE - 1 && y >= 1 && y < SIZE - 1) {
        setPixel(png, x, y, crackColor);
      }
      // Random walk direction
      const dir = Math.floor(rng() * 4);
      if (dir === 0) x++;
      else if (dir === 1) x--;
      else if (dir === 2) y++;
      else y--;
    }
  }
}

// --- Fire overlay pattern ---
function drawFireOverlay(png, seed) {
  const rng = mulberry32(seed);
  const fireColors = [
    rgba(255, 100, 20, 100),
    rgba(255, 160, 30, 80),
    rgba(255, 60, 10, 120),
  ];
  // Blend fire tint over existing pixels
  for (let y = 0; y < SIZE; y++) {
    for (let x = 0; x < SIZE; x++) {
      if (rng() > 0.5) {
        const fc = fireColors[Math.floor(rng() * fireColors.length)];
        const idx = (y * SIZE + x) << 2;
        const existing = rgba(png.data[idx], png.data[idx + 1], png.data[idx + 2], png.data[idx + 3]);
        const t = 0.3 + rng() * 0.25;
        setPixel(png, x, y, lerpColor(existing, fc, t));
      }
    }
  }
}

// ============================================================
// Tile generators
// ============================================================

function generateFloorNormal() {
  const png = createPng();
  const base = rgba(194, 154, 100);     // warm light wood
  const grain = rgba(140, 105, 60);     // darker grain
  const border = rgba(110, 80, 45);     // dark border
  const highlight = rgba(220, 185, 140); // light inner edge

  drawWoodGrain(png, base, grain, 42);

  // Inner highlight (1px inside border)
  drawBorder(png, highlight, 1);
  // Main border
  drawBorder(png, border, 2);
  // Corner accents (darker)
  const corner = rgba(85, 60, 35);
  for (let i = 0; i < 3; i++) {
    for (let j = 0; j < 3; j++) {
      setPixel(png, i, j, corner);
      setPixel(png, SIZE - 1 - i, j, corner);
      setPixel(png, i, SIZE - 1 - j, corner);
      setPixel(png, SIZE - 1 - i, SIZE - 1 - j, corner);
    }
  }

  savePng(png, "Tile_Floor_Normal");
}

function generateWall() {
  const png = createPng();
  const base = rgba(120, 95, 65);       // darker wood / crate
  const grain = rgba(85, 65, 40);
  const border = rgba(65, 48, 28);
  const highlight = rgba(150, 120, 85);

  drawWoodGrain(png, base, grain, 99);

  // Cross brace pattern (wooden crate look)
  const braceColor = rgba(90, 68, 42);
  // Horizontal plank lines
  for (let x = 0; x < SIZE; x++) {
    setPixel(png, x, 10, braceColor);
    setPixel(png, x, 11, braceColor);
    setPixel(png, x, 20, braceColor);
    setPixel(png, x, 21, braceColor);
  }
  // Vertical plank lines
  for (let y = 0; y < SIZE; y++) {
    setPixel(png, 10, y, braceColor);
    setPixel(png, 11, y, braceColor);
    setPixel(png, 20, y, braceColor);
    setPixel(png, 21, y, braceColor);
  }

  // Nails at intersections
  const nail = rgba(180, 170, 155);
  const nailPositions = [[10, 10], [10, 20], [20, 10], [20, 20]];
  for (const [nx, ny] of nailPositions) {
    setPixel(png, nx, ny, nail);
    setPixel(png, nx + 1, ny, nail);
    setPixel(png, nx, ny + 1, nail);
    setPixel(png, nx + 1, ny + 1, nail);
  }

  drawBorder(png, border, 2);
  // Highlight on top-left edges
  for (let p = 2; p < SIZE - 2; p++) {
    setPixel(png, p, 2, highlight);
    setPixel(png, 2, p, highlight);
  }

  savePng(png, "Tile_Wall");
}

function generateFloorBurning() {
  const png = createPng();
  const base = rgba(180, 120, 70);      // slightly reddened wood
  const grain = rgba(140, 80, 40);
  const border = rgba(130, 55, 20);

  drawWoodGrain(png, base, grain, 42);
  drawFireOverlay(png, 77);
  drawBorder(png, border, 2);

  // Embers at corners
  const ember = rgba(255, 80, 0, 200);
  for (let i = 0; i < 4; i++) {
    for (let j = 0; j < 4; j++) {
      setPixel(png, i, j, ember);
      setPixel(png, SIZE - 1 - i, SIZE - 1 - j, ember);
    }
  }

  savePng(png, "Tile_Floor_Burning");
}

function generateFloorCollapsing() {
  const png = createPng();
  const base = rgba(160, 130, 90);      // slightly desaturated wood
  const grain = rgba(120, 90, 55);
  const border = rgba(90, 65, 38);
  const crack = rgba(50, 35, 20);

  drawWoodGrain(png, base, grain, 42);
  drawCracks(png, crack, 55);
  drawBorder(png, border, 2);

  savePng(png, "Tile_Floor_Collapsing");
}

function generateFloorCollapsed() {
  const png = createPng();
  // Dark void / hole
  const base = rgba(25, 20, 18);
  const edgeInner = rgba(55, 40, 30);
  const edgeOuter = rgba(40, 28, 20);

  fill(png, base);

  // Slightly lighter interior noise for depth
  const rng = mulberry32(33);
  for (let y = 4; y < SIZE - 4; y++) {
    for (let x = 4; x < SIZE - 4; x++) {
      if (rng() > 0.85) {
        setPixel(png, x, y, rgba(40, 32, 28));
      }
    }
  }

  // Ragged edge (broken wood)
  for (let p = 0; p < SIZE; p++) {
    const thickness = 2 + Math.floor(rng() * 2);
    for (let t = 0; t < thickness; t++) {
      setPixel(png, p, t, t === 0 ? edgeOuter : edgeInner);
      setPixel(png, p, SIZE - 1 - t, t === 0 ? edgeOuter : edgeInner);
      setPixel(png, t, p, t === 0 ? edgeOuter : edgeInner);
      setPixel(png, SIZE - 1 - t, p, t === 0 ? edgeOuter : edgeInner);
    }
  }

  savePng(png, "Tile_Floor_Collapsed");
}

function generateFloorPermanentlyDestroyed() {
  const png = createPng();
  // Fully transparent - the abyss
  fill(png, rgba(0, 0, 0, 0));

  // Faint edge glow so it's visible in editor
  const edgeColor = rgba(15, 10, 8, 60);
  drawBorder(png, edgeColor, 1);

  savePng(png, "Tile_Floor_Destroyed");
}

// ============================================================
// Main
// ============================================================
console.log("Generating tile sprites (" + SIZE + "x" + SIZE + ")...\n");

if (!fs.existsSync(OUT_DIR)) {
  fs.mkdirSync(OUT_DIR, { recursive: true });
}

generateFloorNormal();
generateWall();
generateFloorBurning();
generateFloorCollapsing();
generateFloorCollapsed();
generateFloorPermanentlyDestroyed();

console.log("\nDone! " + fs.readdirSync(OUT_DIR).filter(f => f.endsWith(".png")).length + " sprites generated in:\n  " + OUT_DIR);
