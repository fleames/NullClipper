export const GIF_FPS = 12;
export const GIF_MAX_SECONDS = 8;
export const GIF_MAX_LONG_EDGE = 640;

export function targetGifSize(width, height, maxLongEdge = GIF_MAX_LONG_EDGE) {
  let w = width;
  let h = height;
  const longEdge = Math.max(w, h);
  if (longEdge > maxLongEdge) {
    const scale = maxLongEdge / longEdge;
    w = Math.max(2, Math.round(w * scale));
    h = Math.max(2, Math.round(h * scale));
  }
  w &= ~1;
  h &= ~1;
  return { width: Math.max(2, w), height: Math.max(2, h) };
}

export function cropPixels(bitmap, cssRect, viewportWidth, viewportHeight) {
  const scaleX = bitmap.width / Math.max(1, viewportWidth);
  const scaleY = bitmap.height / Math.max(1, viewportHeight);
  const sx = Math.max(0, Math.floor(cssRect.x * scaleX));
  const sy = Math.max(0, Math.floor(cssRect.y * scaleY));
  const sw = Math.max(1, Math.min(bitmap.width - sx, Math.ceil(cssRect.w * scaleX)));
  const sh = Math.max(1, Math.min(bitmap.height - sy, Math.ceil(cssRect.h * scaleY)));
  return { sx, sy, sw, sh };
}

export async function bitmapFromDataUrl(dataUrl) {
  const response = await fetch(dataUrl);
  const blob = await response.blob();
  return createImageBitmap(blob);
}

export async function cropToImageData(bitmap, cssRect, viewportWidth, viewportHeight, size = null) {
  const { sx, sy, sw, sh } = cropPixels(bitmap, cssRect, viewportWidth, viewportHeight);
  const width = size?.width ?? sw;
  const height = size?.height ?? sh;
  const canvas = new OffscreenCanvas(width, height);
  const ctx = canvas.getContext('2d', { alpha: false });
  ctx.imageSmoothingEnabled = Boolean(size);
  ctx.imageSmoothingQuality = 'high';
  ctx.drawImage(bitmap, sx, sy, sw, sh, 0, 0, width, height);
  return ctx.getImageData(0, 0, width, height);
}

export async function imageDataToPngBlob(imageData) {
  const canvas = new OffscreenCanvas(imageData.width, imageData.height);
  const ctx = canvas.getContext('2d');
  ctx.putImageData(imageData, 0, 0);
  return canvas.convertToBlob({ type: 'image/png' });
}

export async function pngBlobToDataUrl(blob) {
  const buffer = await blob.arrayBuffer();
  const bytes = new Uint8Array(buffer);
  let binary = '';
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) {
    binary += String.fromCharCode(...bytes.subarray(i, i + chunk));
  }
  return `data:image/png;base64,${btoa(binary)}`;
}

export function timestampName(ext) {
  const now = new Date();
  const pad = (n) => String(n).padStart(2, '0');
  return `clip-${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}.${ext}`;
}
