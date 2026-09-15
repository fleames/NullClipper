/**
 * GIF89a encoder: 256-color median-cut, LZW, Netscape loop.
 * Delay is in centiseconds (desktop 12 fps → 8 cs).
 */

const MAX_CODES = 4095;

class BitWriter {
  constructor() {
    this.bytes = [];
    this.cur = 0;
    this.bit = 0;
  }

  write(code, size) {
    this.cur |= (code << this.bit) >>> 0;
    this.bit += size;
    while (this.bit >= 8) {
      this.bytes.push(this.cur & 0xff);
      this.cur >>>= 8;
      this.bit -= 8;
    }
  }

  end() {
    if (this.bit > 0) {
      this.bytes.push(this.cur & 0xff);
    }
    return this.bytes;
  }
}

function lzw(indices, minCodeSize) {
  const clear = 1 << minCodeSize;
  const eoi = clear + 1;
  const writer = new BitWriter();
  let codeSize = minCodeSize + 1;
  let nextCode = eoi + 1;
  const dict = new Map();

  const reset = () => {
    dict.clear();
    codeSize = minCodeSize + 1;
    nextCode = eoi + 1;
  };

  writer.write(clear, codeSize);
  let prefix = indices[0];
  for (let i = 1; i < indices.length; i += 1) {
    const k = indices[i];
    const key = (prefix << 8) | k;
    const existing = dict.get(key);
    if (existing !== undefined) {
      prefix = existing;
      continue;
    }

    writer.write(prefix, codeSize);
    if (nextCode < MAX_CODES) {
      dict.set(key, nextCode);
      nextCode += 1;
      if (nextCode === 1 << codeSize && codeSize < 12) {
        codeSize += 1;
      }
    } else {
      writer.write(clear, codeSize);
      reset();
    }
    prefix = k;
  }
  writer.write(prefix, codeSize);
  writer.write(eoi, codeSize);
  return writer.end();
}

function packBlocks(bytes) {
  const out = [];
  for (let i = 0; i < bytes.length; i += 255) {
    const len = Math.min(255, bytes.length - i);
    out.push(len);
    for (let j = 0; j < len; j += 1) {
      out.push(bytes[i + j]);
    }
  }
  out.push(0);
  return out;
}

function medianCut(colors, maxColors) {
  const boxes = [{ colors, channel: 0 }];
  const channelRange = (list, ch) => {
    let min = 255;
    let max = 0;
    for (const color of list) {
      const v = color[ch];
      if (v < min) min = v;
      if (v > max) max = v;
    }
    return max - min;
  };

  while (boxes.length < maxColors) {
    let pick = 0;
    let pickRange = -1;
    for (let i = 0; i < boxes.length; i += 1) {
      const box = boxes[i];
      if (box.colors.length < 2) continue;
      let bestCh = 0;
      let bestRange = -1;
      for (let ch = 0; ch < 3; ch += 1) {
        const range = channelRange(box.colors, ch);
        if (range > bestRange) {
          bestRange = range;
          bestCh = ch;
        }
      }
      box.channel = bestCh;
      if (bestRange > pickRange) {
        pickRange = bestRange;
        pick = i;
      }
    }
    if (pickRange <= 0) break;
    const box = boxes[pick];
    const ch = box.channel;
    box.colors.sort((a, b) => a[ch] - b[ch]);
    const mid = Math.max(1, box.colors.length >> 1);
    boxes[pick] = { colors: box.colors.slice(0, mid), channel: 0 };
    boxes.push({ colors: box.colors.slice(mid), channel: 0 });
  }

  return boxes.map((box) => {
    let r = 0;
    let g = 0;
    let b = 0;
    let n = 0;
    for (const color of box.colors) {
      const count = color[3];
      r += color[0] * count;
      g += color[1] * count;
      b += color[2] * count;
      n += count;
    }
    if (!n) return [0, 0, 0];
    return [Math.round(r / n), Math.round(g / n), Math.round(b / n)];
  });
}

function buildPalette(data, maxColors = 256) {
  const hist = new Map();
  for (let i = 0; i < data.length; i += 4) {
    const r = data[i];
    const g = data[i + 1];
    const b = data[i + 2];
    const key = ((r & 0xf8) << 8) | ((g & 0xfc) << 3) | (b >> 3);
    const prev = hist.get(key);
    if (prev) {
      prev[3] += 1;
    } else {
      hist.set(key, [r, g, b, 1]);
    }
  }
  const colors = [...hist.values()];
  if (colors.length <= maxColors) {
    return colors.map((c) => [c[0], c[1], c[2]]);
  }
  return medianCut(colors, maxColors);
}

function mapIndices(data, palette) {
  const cache = new Int16Array(32768);
  cache.fill(-1);
  const indices = new Uint8Array(data.length / 4);
  for (let p = 0, i = 0; p < data.length; p += 4, i += 1) {
    const r = data[p];
    const g = data[p + 1];
    const b = data[p + 2];
    const ck = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
    let idx = cache[ck];
    if (idx < 0) {
      let best = 0;
      let bestDist = Infinity;
      for (let c = 0; c < palette.length; c += 1) {
        const pr = palette[c][0] - r;
        const pg = palette[c][1] - g;
        const pb = palette[c][2] - b;
        const dist = pr * pr + pg * pg + pb * pb;
        if (dist < bestDist) {
          bestDist = dist;
          best = c;
          if (dist === 0) break;
        }
      }
      idx = best;
      cache[ck] = idx;
    }
    indices[i] = idx;
  }
  return indices;
}

function colorTableBytes(palette) {
  const size = 256;
  const table = new Uint8Array(size * 3);
  for (let i = 0; i < palette.length && i < size; i += 1) {
    table[i * 3] = palette[i][0];
    table[i * 3 + 1] = palette[i][1];
    table[i * 3 + 2] = palette[i][2];
  }
  return table;
}

export class GifEncoder {
  constructor({ delayCs = 8, loop = 0 } = {}) {
    this.delayCs = delayCs;
    this.loop = loop;
    this.width = 0;
    this.height = 0;
    this.frames = [];
    this.finished = null;
  }

  addFrame(imageData) {
    if (this.finished) {
      throw new Error('GIF encoder already finished');
    }
    if (!this.width) {
      this.width = imageData.width;
      this.height = imageData.height;
    }
    if (imageData.width !== this.width || imageData.height !== this.height) {
      throw new Error('GIF frames must share a size');
    }
    const palette = buildPalette(imageData.data, 256);
    const indices = mapIndices(imageData.data, palette);
    this.frames.push({ palette, indices });
  }

  finish() {
    if (this.finished) return this.finished;
    if (!this.frames.length) {
      throw new Error('GIF has no frames');
    }

    const out = [];
    const u8 = (v) => out.push(v & 0xff);
    const u16 = (v) => {
      out.push(v & 0xff);
      out.push((v >> 8) & 0xff);
    };

    out.push(0x47, 0x49, 0x46, 0x38, 0x39, 0x61);
    u16(this.width);
    u16(this.height);
    u8(0x70);
    u8(0);
    u8(0);

    out.push(0x21, 0xff, 0x0b);
    for (const ch of 'NETSCAPE2.0') u8(ch.charCodeAt(0));
    u8(0x03);
    u8(0x01);
    u16(this.loop);
    u8(0);

    for (const frame of this.frames) {
      out.push(0x21, 0xf9, 0x04);
      u8(0x04);
      u16(this.delayCs);
      u8(0);
      u8(0);

      u8(0x2c);
      u16(0);
      u16(0);
      u16(this.width);
      u16(this.height);
      u8(0x87);

      const table = colorTableBytes(frame.palette);
      for (let i = 0; i < table.length; i += 1) out.push(table[i]);

      u8(8);
      const packed = packBlocks(lzw(frame.indices, 8));
      for (let i = 0; i < packed.length; i += 1) out.push(packed[i]);
    }

    u8(0x3b);
    this.finished = new Uint8Array(out);
    this.frames = [];
    return this.finished;
  }
}

export function encodeGif(frames, delayCs = 8) {
  const encoder = new GifEncoder({ delayCs });
  for (const frame of frames) {
    encoder.addFrame(frame);
  }
  return encoder.finish();
}
