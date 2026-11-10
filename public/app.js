const $ = (id) => document.getElementById(id);
const setStatus = (id, message, kind = '') => {
  const el = $(id);
  el.textContent = message;
  el.className = `hint ${kind}`.trim();
};

async function apiPost(path, body) {
  const response = await fetch(path, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
  });
  const payload = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(payload.error || `Request failed (${response.status}).`);
  return payload;
}

async function checkBackend() {
  try {
    const r = await fetch('/api/health', { cache: 'no-store' });
    const data = await r.json();
    $('apiStatus').textContent = data.geminiConfigured ? 'Gemini API configured' : 'Gemini key not configured';
    $('apiStatus').className = `tag ${data.geminiConfigured ? '' : 'muted-tag'}`;
  } catch {
    $('apiStatus').textContent = 'Backend unavailable';
    $('apiStatus').className = 'tag muted-tag';
  }
}

// Tabs
for (const button of document.querySelectorAll('.tab')) {
  button.addEventListener('click', () => {
    document.querySelectorAll('.tab').forEach(tab => {
      const active = tab === button;
      tab.classList.toggle('active', active);
      tab.setAttribute('aria-selected', String(active));
    });
    document.querySelectorAll('.tool-panel').forEach(panel => {
      const active = panel.id === `panel-${button.dataset.tab}`;
      panel.hidden = !active;
      panel.classList.toggle('active', active);
    });
    if (button.dataset.tab === 'mesh') resizeMeshRenderer();
  });
}

// Texture generation
const textureCanvas = $('textureCanvas');
const texCtx = textureCanvas.getContext('2d');
function hexToRgb(hex) { return [1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16)); }
function mixColor(a, b, t) { return a.map((v, i) => Math.round(v + (b[i] - v) * t)); }
function rgbCss(rgb) { return `rgb(${rgb[0]},${rgb[1]},${rgb[2]})`; }
function hash2(x, y, seed = 73) {
  let n = Math.imul(x, 374761393) + Math.imul(y, 668265263) + seed * 1442695041;
  n = Math.imul(n ^ (n >>> 13), 1274126177);
  return ((n ^ (n >>> 16)) >>> 0) / 4294967295;
}
function smoothNoise(x, y, period) {
  const gx = x / period, gy = y / period;
  const x0 = Math.floor(gx), y0 = Math.floor(gy);
  const tx = gx - x0, ty = gy - y0;
  const fade = t => t * t * (3 - 2 * t);
  const a = hash2(x0, y0), b = hash2(x0 + 1, y0), c = hash2(x0, y0 + 1), d = hash2(x0 + 1, y0 + 1);
  const top = a + (b - a) * fade(tx), bottom = c + (d - c) * fade(tx);
  return top + (bottom - top) * fade(ty);
}
function drawTexture() {
  const size = Number($('textureResolution').value);
  const pattern = $('texturePattern').value;
  const c1 = hexToRgb($('textureColor1').value);
  const c2 = hexToRgb($('textureColor2').value);
  textureCanvas.width = textureCanvas.height = size;
  const image = texCtx.createImageData(size, size);
  const tile = Math.max(16, Math.floor(size / 8));
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      let t = 0;
      if (pattern === 'checkerboard') t = ((Math.floor(x / tile) + Math.floor(y / tile)) % 2) ? 1 : 0;
      else if (pattern === 'grid') t = (x % tile < Math.max(2, size / 256) || y % tile < Math.max(2, size / 256)) ? 1 : 0;
      else if (pattern === 'gradient') t = (x + y) / (2 * size - 2);
      else {
        t = .58 * smoothNoise(x, y, size / 5) + .27 * smoothNoise(x, y, size / 15) + .15 * smoothNoise(x, y, size / 45);
      }
      const rgb = mixColor(c1, c2, Math.max(0, Math.min(1, t)));
      const i = (y * size + x) * 4;
      image.data[i] = rgb[0]; image.data[i + 1] = rgb[1]; image.data[i + 2] = rgb[2]; image.data[i + 3] = 255;
    }
  }
  texCtx.putImageData(image, 0, 0);
  setStatus('textureStatus', `Generated ${pattern} texture locally (${size} × ${size}).`, 'success');
}
function showTextureMode() {
  const ai = $('textureMode').value === 'ai';
  $('proceduralTextureControls').classList.toggle('hidden', ai);
  $('aiTextureControls').classList.toggle('hidden', !ai);
  setStatus('textureStatus', ai ? 'Gemini image generation needs a configured server-side API key.' : 'Procedural textures are generated locally.');
}
$('textureMode').addEventListener('change', showTextureMode);
$('renderTexture').addEventListener('click', drawTexture);
['texturePattern', 'textureColor1', 'textureColor2', 'textureResolution'].forEach(id => $(id).addEventListener('change', drawTexture));
$('generateAITexture').addEventListener('click', async () => {
  const prompt = $('texturePrompt').value.trim();
  if (!prompt) return setStatus('textureStatus', 'Enter a texture description first.', 'error');
  const button = $('generateAITexture'); button.disabled = true; button.textContent = 'Generating…';
  setStatus('textureStatus', 'Sending the prompt to Gemini…');
  try {
    const data = await apiPost('/api/generate/image', { prompt, aspectRatio: $('textureAspect').value });
    const image = new Image();
    image.onload = () => {
      textureCanvas.width = image.naturalWidth; textureCanvas.height = image.naturalHeight;
      texCtx.clearRect(0, 0, textureCanvas.width, textureCanvas.height);
      texCtx.drawImage(image, 0, 0);
      setStatus('textureStatus', `AI texture ready (${image.naturalWidth} × ${image.naturalHeight}).`, 'success');
    };
    image.onerror = () => setStatus('textureStatus', 'The generated image could not be decoded.', 'error');
    image.src = `data:${data.mimeType};base64,${data.imageBase64}`;
  } catch (error) { setStatus('textureStatus', error.message, 'error'); }
  finally { button.disabled = false; button.textContent = 'Generate with Gemini'; }
});
$('downloadTexture').addEventListener('click', () => {
  textureCanvas.toBlob(blob => {
    if (!blob) return setStatus('textureStatus', 'Could not export this canvas.', 'error');
    downloadBlob(blob, 'placeholderguard-texture.png');
  }, 'image/png');
});

// Audio generation: local PCM buffer, preview and WAV download.
let lastAudioBlob = null;
let currentSamples = null;
function downloadBlob(blob, name) {
  const href = URL.createObjectURL(blob);
  const a = document.createElement('a'); a.href = href; a.download = name; a.click();
  setTimeout(() => URL.revokeObjectURL(href), 1500);
}
function writeWav(samples, sampleRate = 44100) {
  const bytesPerSample = 2;
  const buffer = new ArrayBuffer(44 + samples.length * bytesPerSample);
  const view = new DataView(buffer);
  const writeString = (offset, value) => [...value].forEach((c, i) => view.setUint8(offset + i, c.charCodeAt(0)));
  writeString(0, 'RIFF'); view.setUint32(4, 36 + samples.length * 2, true); writeString(8, 'WAVE');
  writeString(12, 'fmt '); view.setUint32(16, 16, true); view.setUint16(20, 1, true);
  view.setUint16(22, 1, true); view.setUint32(24, sampleRate, true); view.setUint32(28, sampleRate * 2, true);
  view.setUint16(32, 2, true); view.setUint16(34, 16, true); writeString(36, 'data'); view.setUint32(40, samples.length * 2, true);
  let offset = 44;
  for (const sample of samples) { const v = Math.max(-1, Math.min(1, sample)); view.setInt16(offset, v < 0 ? v * 32768 : v * 32767, true); offset += 2; }
  return new Blob([buffer], { type: 'audio/wav' });
}
function drawWaveform(samples) {
  const canvas = $('waveformCanvas'), ctx = canvas.getContext('2d');
  const w = canvas.width, h = canvas.height;
  ctx.clearRect(0, 0, w, h); ctx.fillStyle = '#15181c'; ctx.fillRect(0, 0, w, h);
  ctx.strokeStyle = '#35404a'; ctx.lineWidth = 1;
  for (let i = 1; i < 8; i++) { const x = i * w / 8; ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, h); ctx.stroke(); }
  ctx.beginPath(); ctx.moveTo(0, h / 2); ctx.lineTo(w, h / 2); ctx.stroke();
  ctx.strokeStyle = '#4ec9b0'; ctx.lineWidth = 2; ctx.beginPath();
  const step = Math.max(1, Math.floor(samples.length / w));
  for (let x = 0; x < w; x++) {
    const value = samples[Math.min(samples.length - 1, x * step)] || 0;
    const y = h / 2 - value * h * .42;
    if (x === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
  }
  ctx.stroke();
}
function makeSfx(preset, waveType, frequency, duration) {
  const rate = 44100, total = Math.floor(rate * duration), samples = new Float32Array(total);
  let f0 = frequency, f1 = frequency, type = waveType;
  if (preset === 'coin') { f0 = 1250; f1 = 720; type = 'sine'; duration = Math.max(duration, .35); }
  if (preset === 'jump') { f0 = 240; f1 = 850; type = 'sine'; }
  if (preset === 'laser') { f0 = 980; f1 = 130; type = 'sawtooth'; }
  if (preset === 'sparkle') { f0 = 1450; f1 = 900; type = 'sine'; }
  const end = Math.floor(rate * duration);
  const noiseSeed = 89421;
  for (let i = 0; i < total; i++) {
    const t = i / rate, p = i / Math.max(1, total - 1);
    const freq = f0 + (f1 - f0) * p;
    let raw = 0;
    if (type === 'noise') { const r = Math.sin(i * 12.9898 + noiseSeed) * 43758.5453; raw = (r - Math.floor(r)) * 2 - 1; }
    else {
      const phase = 2 * Math.PI * freq * t;
      if (type === 'square') raw = Math.sign(Math.sin(phase));
      else if (type === 'sawtooth') raw = 2 * ((freq * t) % 1) - 1;
      else raw = Math.sin(phase);
    }
    const attack = Math.min(1, p * 24);
    const decay = Math.pow(1 - p, preset === 'coin' ? 2.8 : 1.8);
    const modulation = preset === 'sparkle' ? (.65 + .35 * Math.sin(t * 2 * Math.PI * 11)) : 1;
    samples[i] = raw * attack * decay * modulation * .66;
  }
  return samples;
}
async function previewAudio(blob) {
  const url = URL.createObjectURL(blob);
  $('audioPlayer').src = url; $('audioPlayer').load();
  $('downloadAudio').disabled = false; lastAudioBlob = blob;
  try { await $('audioPlayer').play(); } catch { /* Browser may require a second user click. */ }
}
$('frequency').addEventListener('input', () => $('frequencyValue').textContent = `${$('frequency').value} Hz`);
$('duration').addEventListener('input', () => $('durationValue').textContent = `${Number($('duration').value).toFixed(1)} s`);
$('soundPreset').addEventListener('change', () => {
  const preset = $('soundPreset').value;
  if (preset !== 'custom') $('waveType').value = preset === 'laser' ? 'sawtooth' : 'sine';
});
$('synthesizeSound').addEventListener('click', async () => {
  const preset = $('soundPreset').value;
  currentSamples = makeSfx(preset, $('waveType').value, Number($('frequency').value), Number($('duration').value));
  drawWaveform(currentSamples);
  const blob = writeWav(currentSamples); await previewAudio(blob);
  setStatus('audioStatus', `Generated local ${preset === 'custom' ? $('waveType').value : preset} sound (${(currentSamples.length / 44100).toFixed(2)} s).`, 'success');
});
$('downloadAudio').addEventListener('click', () => { if (lastAudioBlob) downloadBlob(lastAudioBlob, 'placeholderguard-audio.wav'); });
function showAudioMode() {
  const tts = $('audioMode').value === 'tts';
  $('synthControls').classList.toggle('hidden', tts); $('ttsControls').classList.toggle('hidden', !tts);
  setStatus('audioStatus', tts ? 'Gemini speech generation needs a configured server-side API key.' : 'Procedural SFX are generated locally.');
}
$('audioMode').addEventListener('change', showAudioMode);
$('generateTTS').addEventListener('click', async () => {
  const text = $('ttsText').value.trim();
  if (!text) return setStatus('audioStatus', 'Enter a voice line first.', 'error');
  const button = $('generateTTS'); button.disabled = true; button.textContent = 'Generating…';
  setStatus('audioStatus', 'Generating speech with Gemini…');
  try {
    const data = await apiPost('/api/generate/tts', { text, voice: $('ttsVoice').value, style: $('ttsStyle').value });
    const binary = atob(data.audioBase64), bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    const blob = new Blob([bytes], { type: data.mimeType || 'audio/wav' });
    await previewAudio(blob);
    drawWaveform(new Float32Array(1000));
    setStatus('audioStatus', 'Gemini speech is ready. Preview it above or export the WAV file.', 'success');
  } catch (error) { setStatus('audioStatus', error.message, 'error'); }
  finally { button.disabled = false; button.textContent = 'Generate speech with Gemini'; }
});

// Three.js preview and OBJ exporter.
let scene, camera, renderer, controls, meshGroup;
function initMeshPreview() {
  if (renderer) return;
  const mount = $('meshViewport');
  scene = new THREE.Scene(); scene.background = new THREE.Color('#171c22');
  camera = new THREE.PerspectiveCamera(45, 1, .1, 100); camera.position.set(4.3, 3.2, 5.2);
  renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  mount.appendChild(renderer.domElement);
  controls = new THREE.OrbitControls(camera, renderer.domElement); controls.enableDamping = true;
  scene.add(new THREE.HemisphereLight(0xffffff, 0x364050, 1.25));
  const key = new THREE.DirectionalLight(0xffffff, 1.4); key.position.set(4, 7, 5); scene.add(key);
  const grid = new THREE.GridHelper(8, 16, 0x52616f, 0x35414c); grid.position.y = -1.25; scene.add(grid);
  meshGroup = new THREE.Group(); scene.add(meshGroup);
  createPrimitive(); resizeMeshRenderer();
  const animate = () => { requestAnimationFrame(animate); controls.update(); renderer.render(scene, camera); };
  animate();
}
function makeGeometry(shape) {
  switch (shape) {
    case 'sphere': return new THREE.SphereGeometry(.7, 20, 14);
    case 'cylinder': return new THREE.CylinderGeometry(.55, .65, 1.3, 12);
    case 'cone': return new THREE.ConeGeometry(.65, 1.4, 10);
    case 'torus': return new THREE.TorusGeometry(.55, .18, 10, 24);
    case 'icosahedron': return new THREE.IcosahedronGeometry(.8, 1);
    default: return new THREE.BoxGeometry(1.2, 1.2, 1.2);
  }
}
function clearMeshGroup() {
  if (!meshGroup) return;
  while (meshGroup.children.length) {
    const obj = meshGroup.children.pop();
    obj.geometry?.dispose();
    if (Array.isArray(obj.material)) obj.material.forEach(m => m.dispose()); else obj.material?.dispose();
  }
}
function addRecipe(recipe) {
  clearMeshGroup();
  for (const part of recipe.components.slice(0, 16)) {
    const geometry = makeGeometry(part.shape);
    const material = new THREE.MeshStandardMaterial({ color: part.color || '#8b9aa8', roughness: .82, metalness: .08, flatShading: true });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.position.set(...part.position); mesh.scale.set(...part.scale); mesh.rotation.set(...part.rotation);
    meshGroup.add(mesh);
  }
  if (meshGroup.children.length) {
    const box = new THREE.Box3().setFromObject(meshGroup);
    const center = box.getCenter(new THREE.Vector3()); meshGroup.position.sub(center);
  }
  $('meshStatus').dataset.recipeTitle = recipe.title || 'Procedural mesh';
  setStatus('meshStatus', `Created “${recipe.title || 'AI prop'}” from ${recipe.components.length} procedural components.`, 'success');
}
function createPrimitive() {
  if (!meshGroup) return;
  addRecipe({ title: $('meshShape').value, components: [{ shape: $('meshShape').value, position: [0, 0, 0], scale: [1, 1, 1], rotation: [0, 0, 0], color: $('meshColor').value }] });
}
function resizeMeshRenderer() {
  if (!renderer) return;
  const mount = $('meshViewport');
  const width = Math.max(1, mount.clientWidth), height = Math.max(1, mount.clientHeight);
  renderer.setSize(width, height, false); camera.aspect = width / height; camera.updateProjectionMatrix();
}
$('createPrimitive').addEventListener('click', () => { initMeshPreview(); createPrimitive(); });
$('generateMeshRecipe').addEventListener('click', async () => {
  const prompt = $('meshPrompt').value.trim();
  if (!prompt) return setStatus('meshStatus', 'Describe an ordinary game prop first.', 'error');
  const button = $('generateMeshRecipe'); button.disabled = true; button.textContent = 'Building recipe…';
  setStatus('meshStatus', 'Asking Gemini for a supported primitive recipe…');
  try {
    const recipe = await apiPost('/api/generate/mesh-recipe', { prompt });
    initMeshPreview(); addRecipe(recipe);
  } catch (error) { setStatus('meshStatus', error.message, 'error'); }
  finally { button.disabled = false; button.textContent = 'Create prop recipe with Gemini'; }
});
$('exportOBJ').addEventListener('click', () => {
  initMeshPreview();
  if (!meshGroup.children.length) return setStatus('meshStatus', 'Create a mesh before exporting.', 'error');
  if (!THREE.OBJExporter) return setStatus('meshStatus', 'OBJ exporter did not load. Check your internet connection and reload.', 'error');
  const exporter = new THREE.OBJExporter();
  const output = exporter.parse(meshGroup);
  downloadBlob(new Blob([output], { type: 'text/plain' }), 'placeholderguard-prop.obj');
  setStatus('meshStatus', 'OBJ exported. Materials are not embedded; export an MTL/material separately in a later workflow.', 'success');
});
window.addEventListener('resize', resizeMeshRenderer);

// Initial state
checkBackend();
drawTexture();
drawWaveform(new Float32Array(1000));
showTextureMode(); showAudioMode();
