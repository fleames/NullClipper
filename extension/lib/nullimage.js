/**
 * Byte-for-byte port of NullClipper's ni1-aes-gcm-256 client
 * (Services/NullImage). The web viewer can only decrypt this if the
 * header, AAD, and chunk framing match exactly.
 *
 * Blob: "NIR1" | version=1 | flags=0 | chunkSize BE u32 | reserved 2
 *        then chunks of iv(12) | ciphertext | tag(16)
 * AAD:  ni1|<id>|<index>|<total>
 */

export const ENCRYPTION_SCHEME = 'ni1-aes-gcm-256';
const IV_BYTES = 12;
const TAG_BITS = 128;
const HEADER_BYTES = 12;
export const DEFAULT_CHUNK_SIZE = 2 * 1024 * 1024;
const MAGIC = new TextEncoder().encode('NIR1');

export class NullImageError extends Error {
  constructor(code, message, statusCode = null) {
    super(message);
    this.name = 'NullImageError';
    this.code = code;
    this.statusCode = statusCode;
  }
}

export function base64UrlEncode(bytes) {
  let binary = '';
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) {
    binary += String.fromCharCode(...bytes.subarray(i, i + chunk));
  }
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

export function buildHeader(chunkSize = DEFAULT_CHUNK_SIZE) {
  const header = new Uint8Array(HEADER_BYTES);
  header.set(MAGIC, 0);
  header[4] = 1;
  header[5] = 0;
  new DataView(header.buffer).setUint32(6, chunkSize, false);
  return header;
}

export function totalChunksFor(size, chunkSize = DEFAULT_CHUNK_SIZE) {
  return Math.max(1, Math.ceil(size / chunkSize));
}

function utf8(text) {
  return new TextEncoder().encode(text);
}

async function encryptAesGcm(cryptoKey, plaintext, iv, aad) {
  const encrypted = new Uint8Array(
    await crypto.subtle.encrypt(
      { name: 'AES-GCM', iv, additionalData: aad, tagLength: TAG_BITS },
      cryptoKey,
      plaintext,
    ),
  );
  const framed = new Uint8Array(IV_BYTES + encrypted.length);
  framed.set(iv, 0);
  framed.set(encrypted, IV_BYTES);
  return framed;
}

export async function encryptChunk(cryptoKey, plaintext, imageId, chunkIndex, totalChunks) {
  const iv = crypto.getRandomValues(new Uint8Array(IV_BYTES));
  const aad = utf8(`ni1|${imageId}|${chunkIndex}|${totalChunks}`);
  return encryptAesGcm(cryptoKey, plaintext, iv, aad);
}

export async function encryptMetadata(cryptoKey, imageId, name, type, size) {
  const plaintext = utf8(JSON.stringify({ name, type, size }));
  const iv = crypto.getRandomValues(new Uint8Array(IV_BYTES));
  const aad = utf8(`ni1-meta|${imageId}`);
  const framed = await encryptAesGcm(cryptoKey, plaintext, iv, aad);
  const combined = framed.subarray(IV_BYTES);
  return `${base64UrlEncode(iv)}.${base64UrlEncode(combined)}`;
}

async function readError(response) {
  try {
    const body = await response.json();
    if (body?.error) {
      return new NullImageError(
        body.error.code ?? 'SERVER_ERROR',
        body.error.message ?? 'The request failed.',
        response.status,
      );
    }
  } catch {
    // Non-JSON error body (blob route can be plain text).
  }
  return new NullImageError(
    'SERVER_ERROR',
    `The request failed with status ${response.status}.`,
    response.status,
  );
}

function joinUrl(baseUrl, path) {
  const base = baseUrl.replace(/\/+$/, '');
  return `${base}/${path.replace(/^\/+/, '')}`;
}

export async function uploadToNullImage(plaintext, fileName, mimeType, options) {
  const { serverUrl, expirySeconds, burnAfterView, password } = options;
  if (!serverUrl) {
    throw new NullImageError('BAD_URL', 'A NullImage server URL is required.');
  }

  const bytes = plaintext instanceof Uint8Array ? plaintext : new Uint8Array(plaintext);
  const cryptoKey = await crypto.subtle.generateKey(
    { name: 'AES-GCM', length: 256 },
    true,
    ['encrypt'],
  );
  const rawKey = new Uint8Array(await crypto.subtle.exportKey('raw', cryptoKey));
  const chunkSize = DEFAULT_CHUNK_SIZE;
  const totalChunks = totalChunksFor(bytes.length, chunkSize);

  const createPayload = {
    declaredPlaintextSize: bytes.length,
    expirySeconds,
    burnAfterView: Boolean(burnAfterView),
    encryptionScheme: ENCRYPTION_SCHEME,
  };
  if (password) {
    createPayload.password = password;
  }

  const createdRes = await fetch(joinUrl(serverUrl, 'api/images'), {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(createPayload),
  });
  if (!createdRes.ok) {
    throw await readError(createdRes);
  }
  const created = await createdRes.json();
  if (!created?.id || !created?.uploadToken) {
    throw new NullImageError('SERVER_ERROR', 'The server returned an empty response.');
  }

  const header = buildHeader(chunkSize);
  const framedChunks = [];
  let bodyLength = header.length;
  for (let index = 0; index < totalChunks; index += 1) {
    const start = index * chunkSize;
    const slice = bytes.subarray(start, Math.min(bytes.length, start + chunkSize));
    const framed = await encryptChunk(cryptoKey, slice, created.id, index, totalChunks);
    framedChunks.push(framed);
    bodyLength += framed.length;
  }

  const body = new Uint8Array(bodyLength);
  body.set(header, 0);
  let offset = header.length;
  for (const chunk of framedChunks) {
    body.set(chunk, offset);
    offset += chunk.length;
  }

  const metadata = await encryptMetadata(cryptoKey, created.id, fileName, mimeType, bytes.length);
  const putRes = await fetch(joinUrl(serverUrl, `api/images/${created.id}/blob`), {
    method: 'PUT',
    headers: {
      'content-type': 'application/octet-stream',
      'x-nullimage-upload-token': created.uploadToken,
      'x-nullimage-metadata': metadata,
      'x-nullimage-plaintext-size': String(bytes.length),
    },
    body,
  });
  if (!putRes.ok) {
    throw await readError(putRes);
  }

  const finalizeRes = await fetch(joinUrl(serverUrl, `api/images/${created.id}/finalize`), {
    method: 'POST',
    headers: { 'x-nullimage-upload-token': created.uploadToken },
  });
  if (!finalizeRes.ok) {
    throw await readError(finalizeRes);
  }

  return {
    imageId: created.id,
    shareUrl: `${serverUrl.replace(/\/+$/, '')}/${created.id}#k=${base64UrlEncode(rawKey)}`,
    manageToken: created.manageToken,
    expiresAt: created.expiresAt ?? null,
  };
}
