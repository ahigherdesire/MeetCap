// Generates a brand-colored app/tray icon PNG with no native deps.
// Run: node electron/make-icon.cjs
const zlib = require('zlib')
const fs = require('fs')
const path = require('path')

const SIZE = 256
const brand = [29, 64, 245] // #1d40f5
const white = [255, 255, 255]

function rounded(x, y, w, h, r, px, py) {
  // inside rounded rect?
  const inX = px >= x + r && px <= x + w - r
  const inY = py >= y + r && py <= y + h - r
  if (px < x || px > x + w || py < y || py > y + h) return false
  if (inX || inY) return true
  const cx = px < x + r ? x + r : x + w - r
  const cy = py < y + r ? y + r : y + h - r
  return (px - cx) ** 2 + (py - cy) ** 2 <= r * r
}

function dist(px, py, cx, cy) {
  return Math.sqrt((px - cx) ** 2 + (py - cy) ** 2)
}

const raw = Buffer.alloc(SIZE * (SIZE * 4 + 1))
let o = 0
const c = SIZE / 2
for (let y = 0; y < SIZE; y++) {
  raw[o++] = 0 // filter byte
  for (let x = 0; x < SIZE; x++) {
    let col = [0, 0, 0]
    let a = 0
    if (rounded(0, 0, SIZE, SIZE, 56, x, y)) {
      col = brand
      a = 255
      const d = dist(x, y, c, c)
      if (d <= 78) col = white // outer white ring/disc
      if (d <= 40) col = brand // inner brand dot
    }
    raw[o++] = col[0]
    raw[o++] = col[1]
    raw[o++] = col[2]
    raw[o++] = a
  }
}

function chunk(type, data) {
  const len = Buffer.alloc(4)
  len.writeUInt32BE(data.length, 0)
  const t = Buffer.from(type, 'ascii')
  const crc = Buffer.alloc(4)
  crc.writeUInt32BE(crc32(Buffer.concat([t, data])) >>> 0, 0)
  return Buffer.concat([len, t, data, crc])
}

function crc32(buf) {
  let c = ~0
  for (let i = 0; i < buf.length; i++) {
    c ^= buf[i]
    for (let k = 0; k < 8; k++) c = (c >>> 1) ^ (0xedb88320 & -(c & 1))
  }
  return ~c
}

const sig = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10])
const ihdr = Buffer.alloc(13)
ihdr.writeUInt32BE(SIZE, 0)
ihdr.writeUInt32BE(SIZE, 4)
ihdr[8] = 8 // bit depth
ihdr[9] = 6 // RGBA
const png = Buffer.concat([
  sig,
  chunk('IHDR', ihdr),
  chunk('IDAT', zlib.deflateSync(raw)),
  chunk('IEND', Buffer.alloc(0)),
])

const outDir = path.join(__dirname)
fs.writeFileSync(path.join(outDir, 'icon.png'), png)
const buildDir = path.join(__dirname, '..', 'build')
fs.mkdirSync(buildDir, { recursive: true })
fs.writeFileSync(path.join(buildDir, 'icon.png'), png)
console.log('Wrote electron/icon.png and build/icon.png (' + png.length + ' bytes)')
