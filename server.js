import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PUBLIC_DIR = path.join(__dirname, 'public');
const MAX_BODY_BYTES = 64 * 1024;
const WINDOW_MS = 60_000;
const MAX_REQUESTS_PER_WINDOW = 12;
const requestBuckets = new Map();

function loadEnvFile() {
  const file = path.join(__dirname, '.env');
  if (!fs.existsSync(file)) return;
  for (const rawLine of fs.readFileSync(file, 'utf8').split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith('#')) continue;
    const equals = line.indexOf('=');
    if (equals < 1) continue;
    const key = line.slice(0, equals).trim();
    let value = line.slice(equals + 1).trim();
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }
    if (!(key in process.env)) process.env[key] = value;
  }
}
loadEnvFile();

const PORT = Number(process.env.PORT || 3000);
const HOST = process.env.HOST || '127.0.0.1';
const API_KEY = process.env.GEMINI_API_KEY || '';
const IMAGE_MODEL = process.env.GEMINI_IMAGE_MODEL || 'gemini-3.1-flash-image';
const TTS_MODEL = process.env.GEMINI_TTS_MODEL || 'gemini-3.8-flash-tts';
const TEXT_MODEL = process.env.GEMINI_TEXT_MODEL || 'gemini-3.8-flash';
const GEMINI_BASE = 'https://generativelanguage.googleapis.com';

const MIME_TYPES = {
  '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8', '.json': 'application/json; charset=utf-8',
  '.png': 'image/png', '.jpg': 'image/jpeg', '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon', '.zip': 'application/zip', '.txt': 'text/plain; charset=utf-8',
};

function sendJson(res, status, data) {
  const body = JSON.stringify(data);
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': Buffer.byteLength(body),
    'Cache-Control': 'no-store',
    'X-Content-Type-Options': 'nosniff',
    'Referrer-Policy': 'no-referrer',
  });
  res.end(body);
}

function getClientKey(req) {
  // This starter intentionally does not trust X-Forwarded-For.
  return req.socket.remoteAddress || 'unknown';
}

function isRateLimited(req) {
  const key = getClientKey(req);
  const now = Date.now();
  const bucket = requestBuckets.get(key);
  if (!bucket || now - bucket.start >= WINDOW_MS) {
    requestBuckets.set(key, { start: now, count: 1 });
    return false;
  }
  bucket.count += 1;
  return bucket.count > MAX_REQUESTS_PER_WINDOW;
}

async function readJson(req) {
  let size = 0;
  const chunks = [];
  for await (const chunk of req) {
    size += chunk.length;
    if (size > MAX_BODY_BYTES) {
      const error = new Error('Request body is too large.');
      error.status = 413;
      throw error;
    }
    chunks.push(chunk);
  }
  const raw = Buffer.concat(chunks).toString('utf8');
  try {
    return raw ? JSON.parse(raw) : {};
  } catch {
    const error = new Error('Request body must be valid JSON.');
    error.status = 400;
    throw error;
  }
}

function requireApiKey() {
  if (!API_KEY || API_KEY.includes('PASTE_YOUR_')) {
    const error = new Error('Gemini API key is not configured. Copy .env.example to .env and set GEMINI_API_KEY.');
    error.status = 503;
    throw error;
  }
}

async function geminiGenerate(model, body, apiVersion = 'v1') {
  requireApiKey();
  const url = `${GEMINI_BASE}/${apiVersion}/models/${encodeURIComponent(model)}:generateContent`;
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'x-goog-api-key': API_KEY },
    body: JSON.stringify(body),
    signal: AbortSignal.timeout(120_000),
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const message = data?.error?.message || `Gemini API returned HTTP ${response.status}.`;
    const error = new Error(message);
    error.status = response.status === 429 ? 429 : 502;
    throw error;
  }
  return data;
}

function extractText(data) {
  return (data?.candidates?.[0]?.content?.parts || []).map(p => p.text || '').join('\n').trim();
}

const VOICES = new Set([
  'Zephyr', 'Puck', 'Charon', 'Kore', 'Fenrir', 'Leda', 'Orus', 'Aoede',
  'Callirrhoe', 'Autonoe', 'Enceladus', 'Iapetus', 'Umbriel', 'Algieba',
  'Despina', 'Erinome', 'Algenib', 'Rasalgethi', 'Laomedeia', 'Achernar',
  'Alnilam', 'Schedar', 'Gacrux', 'Pulcherrima', 'Achird', 'Zubenelgenubi',
  'Vindemiatrix', 'Sadachbia', 'Sadaltager', 'Sulafat'
]);

const BLOCKED_MESH_TERMS = /\b(gun|firearm|rifle|pistol|shotgun|sword|dagger|knife|blade|bomb|explosive|grenade|missile|ammunition|ammo|bullet|weapon|warhead)\b/i;

async function handleApi(req, res, url) {
  if (req.method === 'GET' && url.pathname === '/api/health') {
    return sendJson(res, 200, {
      ok: true,
      geminiConfigured: Boolean(API_KEY && !API_KEY.includes('PASTE_YOUR_')),
      models: { image: IMAGE_MODEL, tts: TTS_MODEL, text: TEXT_MODEL },
    });
  }
  if (req.method !== 'POST') return sendJson(res, 405, { error: 'Method not allowed.' });
  if (isRateLimited(req)) return sendJson(res, 429, { error: 'Local request limit reached. Wait a minute before trying again.' });

  const body = await readJson(req);

  if (url.pathname === '/api/generate/image') {
    const prompt = String(body.prompt || '').trim();
    const aspectRatio = String(body.aspectRatio || '1:1');
    if (prompt.length < 4 || prompt.length > 1200) return sendJson(res, 400, { error: 'Prompt must be between 4 and 1,200 characters.' });
    if (BLOCKED_MESH_TERMS.test(prompt)) return sendJson(res, 400, { error: 'This texture tool is intended for ordinary game materials and scenery, not weapon-related assets.' });
    if (!new Set(['1:1', '3:2', '2:3', '16:9', '9:16', '4:3', '3:4']).has(aspectRatio)) {
      return sendJson(res, 400, { error: 'Unsupported aspect ratio.' });
    }
    const fullPrompt = [
      'Create a game-ready 2D texture asset. Use a flat, orthographic, straight-on texture view.',
      'No perspective, no scene, no lighting setup, no cast shadows, no text, no watermark.',
      'Aim for clean material detail and, when appropriate, seamless tileability on all edges.',
      `Asset brief: ${prompt}`,
    ].join('\n');
    const result = await geminiGenerate(IMAGE_MODEL, {
      contents: [{ role: 'user', parts: [{ text: fullPrompt }] }],
      generationConfig: {
        responseModalities: ['IMAGE'],
        responseFormat: { image: { aspectRatio, imageSize: '1K' } },
      },
    }, 'v1');
    const parts = result?.candidates?.[0]?.content?.parts || [];
    const imagePart = parts.find(part => part.inlineData?.data);
    if (!imagePart) throw new Error('Gemini returned no image. Try a more specific texture prompt.');
    return sendJson(res, 200, {
      imageBase64: imagePart.inlineData.data,
      mimeType: imagePart.inlineData.mimeType || 'image/png',
      note: extractText(result),
    });
  }

  if (url.pathname === '/api/generate/tts') {
    const text = String(body.text || '').trim();
    const style = String(body.style || 'Clear, natural game voice line.').trim().slice(0, 240);
    const voice = String(body.voice || 'Kore');
    if (text.length < 1 || text.length > 2200) return sendJson(res, 400, { error: 'Speech text must be between 1 and 2,200 characters.' });
    if (!VOICES.has(voice)) return sendJson(res, 400, { error: 'Unsupported voice selection.' });
    requireApiKey();
    const response = await fetch(`${GEMINI_BASE}/v1beta/interactions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'x-goog-api-key': API_KEY },
      body: JSON.stringify({
        model: TTS_MODEL,
        input: [{ type: 'user_input', content: [{
          type: 'text', text,
          annotations: [{ type: 'speech_metadata', style }],
        }] }],
        response_format: { type: 'audio' },
        generation_config: { speech_config: [{ voice }] },
      }),
      signal: AbortSignal.timeout(120_000),
    });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) {
      const error = new Error(result?.error?.message || `Gemini TTS returned HTTP ${response.status}.`);
      error.status = response.status === 429 ? 429 : 502;
      throw error;
    }
    let audioBase64 = result?.output_audio?.data;
    if (!audioBase64 && Array.isArray(result?.steps)) {
      for (const step of result.steps) {
        for (const item of step.content || []) if (item.type === 'audio' && item.data) audioBase64 = item.data;
      }
    }
    if (!audioBase64) throw new Error('Gemini returned no audio data. Check the TTS model access for this API key.');
    return sendJson(res, 200, { audioBase64, mimeType: 'audio/wav' });
  }

  if (url.pathname === '/api/generate/mesh-recipe') {
    const prompt = String(body.prompt || '').trim();
    if (prompt.length < 4 || prompt.length > 500) return sendJson(res, 400, { error: 'Mesh prompt must be between 4 and 500 characters.' });
    if (BLOCKED_MESH_TERMS.test(prompt)) return sendJson(res, 400, { error: 'This prototype only creates non-weapon game props such as crates, furniture, plants, rocks, and decorative objects.' });
    const schema = {
      type: 'OBJECT',
      properties: {
        title: { type: 'STRING' },
        components: {
          type: 'ARRAY',
          items: {
            type: 'OBJECT',
            properties: {
              shape: { type: 'STRING', enum: ['box', 'sphere', 'cylinder', 'cone', 'torus', 'icosahedron'] },
              position: { type: 'ARRAY', items: { type: 'NUMBER' } },
              scale: { type: 'ARRAY', items: { type: 'NUMBER' } },
              rotation: { type: 'ARRAY', items: { type: 'NUMBER' } },
              color: { type: 'STRING' },
            },
            required: ['shape', 'position', 'scale', 'rotation', 'color'],
          },
        },
      },
      required: ['title', 'components'],
    };
    const instruction = `You are a procedural 3D prop recipe assistant. Create a simple stylized low-poly Unity game prop based on this request: ${prompt}\n\nReturn 3 to 16 primitive components. Only use box, sphere, cylinder, cone, torus, icosahedron. Each component has shape, position [x,y,z] (each between -2 and 2), scale [x,y,z] (each between 0.08 and 2.5), rotation [x,y,z] in radians (each between -3.14 and 3.14), and color as a six-digit hex value. Center the result around the origin and keep it compact. Only create ordinary non-weapon props. Do not include code or any text outside the JSON object.`;
    const result = await geminiGenerate(TEXT_MODEL, {
      contents: [{ role: 'user', parts: [{ text: instruction }] }],
      generationConfig: { responseMimeType: 'application/json', responseSchema: schema, thinkingConfig: { thinkingLevel: 'low' } },
    }, 'v1');
    const raw = extractText(result);
    let recipe;
    try { recipe = JSON.parse(raw); }
    catch { throw new Error('The AI mesh recipe was not valid JSON. Try a simpler prop description.'); }
    const allowedShapes = new Set(['box', 'sphere', 'cylinder', 'cone', 'torus', 'icosahedron']);
    if (!Array.isArray(recipe.components) || recipe.components.length < 1) throw new Error('The AI returned an empty mesh recipe.');
    recipe.components = recipe.components.slice(0, 16).map((part) => {
      const shape = allowedShapes.has(part.shape) ? part.shape : 'box';
      const vector = (input, fallback, min, max) => Array.from({ length: 3 }, (_, i) => {
        const n = Number(input?.[i]);
        return Number.isFinite(n) ? Math.max(min, Math.min(max, n)) : fallback[i];
      });
      const color = /^#[0-9a-fA-F]{6}$/.test(part.color) ? part.color : '#8b9aa8';
      return {
        shape,
        position: vector(part.position, [0, 0, 0], -2, 2),
        scale: vector(part.scale, [1, 1, 1], 0.08, 2.5),
        rotation: vector(part.rotation, [0, 0, 0], -3.14, 3.14),
        color,
      };
    });
    recipe.title = String(recipe.title || 'AI procedural prop').slice(0, 80);
    return sendJson(res, 200, recipe);
  }

  return sendJson(res, 404, { error: 'API route not found.' });
}

async function serveStatic(req, res, url) {
  let pathname;
  try { pathname = decodeURIComponent(url.pathname); }
  catch { return sendJson(res, 400, { error: 'Invalid URL path.' }); }
  if (pathname === '/') pathname = '/index.html';
  // Only the manager ZIP is served from the download folder; web assets stay under /public.
  const downloadBase = path.join(__dirname, 'downloads');
  const baseDir = pathname.startsWith('/downloads/') ? downloadBase : PUBLIC_DIR;
  const relativePath = pathname.startsWith('/downloads/') ? pathname.slice('/downloads'.length) : pathname;
  const filePath = path.resolve(baseDir, `.${relativePath}`);
  if (!filePath.startsWith(baseDir + path.sep) && filePath !== baseDir) {
    return sendJson(res, 403, { error: 'Forbidden.' });
  }
  let target = filePath;
  try {
    const stat = await fs.promises.stat(target);
    if (stat.isDirectory()) target = path.join(target, 'index.html');
    const content = await fs.promises.readFile(target);
    res.writeHead(200, {
      'Content-Type': MIME_TYPES[path.extname(target).toLowerCase()] || 'application/octet-stream',
      'Content-Length': content.length,
      'X-Content-Type-Options': 'nosniff',
      'Referrer-Policy': 'no-referrer',
      'X-Frame-Options': 'DENY',
      'Cache-Control': target.endsWith('.html') ? 'no-cache' : 'public, max-age=3600',
    });
    res.end(content);
  } catch {
    sendJson(res, 404, { error: 'File not found.' });
  }
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url || '/', `http://${req.headers.host || 'localhost'}`);
  try {
    if (url.pathname.startsWith('/api/')) return await handleApi(req, res, url);
    if (req.method !== 'GET' && req.method !== 'HEAD') return sendJson(res, 405, { error: 'Method not allowed.' });
    return await serveStatic(req, res, url);
  } catch (error) {
    const status = Number.isInteger(error.status) ? error.status : 500;
    const message = status >= 500 && status !== 503 ? 'The request could not be completed. Check the server log for details.' : error.message;
    console.error(`[${new Date().toISOString()}] ${req.method} ${url.pathname}:`, error.message);
    if (!res.headersSent) sendJson(res, status, { error: message });
    else res.end();
  }
});

server.on('error', (error) => {
  if (error.code === 'EADDRINUSE') {
    console.error(`\n[STARTUP ERROR] Port ${PORT} is already in use.`);
    console.error('Close the other app or older PlaceholderGuard server using this port, then try again.');
  } else if (error.code === 'EACCES') {
    console.error(`\n[STARTUP ERROR] Permission denied while binding to ${HOST}:${PORT}.`);
  } else {
    console.error('\n[STARTUP ERROR]', error);
  }
  process.exit(1);
});

server.listen(PORT, HOST, () => {
  console.log(`PlaceholderGuard web server running at http://${HOST}:${PORT}`);
  console.log(`Gemini API key: ${API_KEY && !API_KEY.includes('PASTE_YOUR_') ? 'configured' : 'not configured (procedural tools still work)'}`);
});
