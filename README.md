# PlaceholderGuard Website + AI Asset Studio (starter)

A small, functional website that keeps the existing PlaceholderGuard Unity manager separate from its asset-generation tools.

## What's included

- **Manager download:** bundles the existing `PlaceholderGuard-0.2.0-Documented.zip` package as `downloads/PlaceholderGuard-0.2.0.zip`. Its Unity package source was not modified.
- **Procedural textures:** checkerboard, grid, fractal-noise and gradient PNG generation in the browser.
- **AI textures:** Gemini image generation through a server-side endpoint. The prompt is framed for a flat game texture.
- **Procedural audio:** local waveform synthesis, four presets, preview and WAV export.
- **AI speech:** Gemini TTS with voice and style selection; preview and WAV export.
- **3D meshes:** Three.js primitive preview, orbit/zoom, AI-assisted primitive recipe generation, and OBJ export.

### 3D generation boundary

Gemini does not return a native mesh file in this implementation. It returns a constrained JSON recipe describing supported primitive shapes; Three.js builds that recipe into a mesh. OBJ export contains geometry. It does not currently export a Unity material file or textures alongside the OBJ.

## Requirements

- Windows 10/11: Node.js 20 or newer (the launcher checks this before starting).
- PowerShell, which is included with Windows 10/11, for the launcher readiness check.
- A Gemini API key for the two AI image/speech endpoints and AI-assisted mesh recipe endpoint.
- An internet connection for the CDN-hosted Three.js scripts and Gemini API calls.

## Run locally

1. **Extract the entire ZIP** using File Explorer → right-click → **Extract All**. Do not run the BAT from inside the ZIP preview.
2. Open the extracted `PlaceholderGuard-Web-Starter` folder.
3. Copy `.env.example` to `.env` (the Windows launcher creates it automatically on first run).
4. Put your Gemini key in `.env`:

   ```env
   GEMINI_API_KEY=your_key_here
   ```

5. Start the site:
   - **Windows:** double-click `START-WINDOWS.bat`. It checks Node.js, starts the server, waits for its health endpoint, and only then opens the browser. Keep the separate server terminal open.
   - **Terminal:** run `npm start` (or `node server.js`) from this folder.
6. Open `http://127.0.0.1:3000`.

No `npm install` is required; the server uses Node's built-in modules only.

## API models used

- Images: `gemini-3.1-flash-image` (override with `GEMINI_IMAGE_MODEL`).
- TTS: `gemini-3.8-flash-tts` (override with `GEMINI_TTS_MODEL`).
- Mesh recipe: `gemini-3.8-flash` (override with `GEMINI_TEXT_MODEL`).

These IDs and request patterns are based on Google's Gemini API documentation available when this starter was prepared. Model access, regional availability, account quotas, and billing are controlled by Google and may differ by API key.

## Security notes — read before deployment

- The Gemini key is read only by `server.js`; it is never sent to the browser.
- Do not commit `.env` or paste the API key into `public/` files.
- The server binds to `127.0.0.1` by default. This is a local-development starter, not a production deployment.
- It includes a small in-memory request limit, input size limits, fixed model IDs, and a constrained mesh recipe schema. The in-memory limit resets when the server restarts and is not suitable for a public multi-user deployment.
- Before making this website public, add authentication, per-user quotas, persistent rate limiting, request logging/monitoring, HTTPS/reverse-proxy configuration, and a budget cap. Otherwise visitors could consume the API quota attached to the server key.

## Downloading PlaceholderGuard

The bundled manager is version 0.2.0. Extract the download and copy its inner `PlaceholderGuard` folder into a Unity project's `Packages/` directory. See `docs/PlaceholderGuard-0.2.0-README.txt` for complete manager information. Test in a disposable Unity project and check the Unity Console before using it in a production project.

## Project layout

```text
PlaceholderGuard-Web-Starter/
├── downloads/PlaceholderGuard-0.2.0.zip
├── docs/PlaceholderGuard-0.2.0-README.txt
├── public/
│   ├── index.html
│   ├── styles.css
│   └── app.js
├── .env.example
├── .gitignore
├── package.json
├── server.js
└── README.md
```

### Windows quick start

You can double-click `START-WINDOWS.bat`. On first run it creates an empty `.env` from `.env.example`, opens the local page, and starts the Node server in a terminal. Add the Gemini key to `.env` before using AI generation; restart the server after editing it.


## Windows troubleshooting

- **“Node.js is not installed” / “node is not recognized”:** install Node.js 20 LTS or newer from <https://nodejs.org/en/download>, then close and reopen VS Code/Command Prompt and retry.
- **The browser says it cannot reach the page:** keep the `PlaceholderGuard Server` terminal open. The launcher waits for `/api/health` before opening the browser.
- **“Port 3000 is already in use”:** close the other program using port 3000, or stop any older PlaceholderGuard server window, then retry.
- **A file or folder is missing:** extract the ZIP again with **Extract All**. The folder must contain `server.js`, `public`, and `downloads`.
- **The website opens but Gemini does not work:** add `GEMINI_API_KEY=your_key_here` to `.env` and restart the server. Procedural tools do not require a Gemini key.
- **Still not starting:** copy the full text from the `PlaceholderGuard Server` terminal and share it. That message identifies the specific startup failure.
