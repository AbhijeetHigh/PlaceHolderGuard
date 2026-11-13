# PlaceholderGuard — Unity Manager & AI Asset Studio

> **Project overview · Current build: PlaceholderGuard 0.2.0 · Planned manager release: 0.2.1**

PlaceholderGuard is a Unity development companion with two connected parts:

1. **PlaceholderGuard Unity Manager** — a Unity Editor package for identifying, tracking, reviewing, and replacing temporary project content.
2. **PlaceholderGuard Web / AI Asset Studio** — a companion website where users can download the Unity manager and access procedural and AI-assisted asset tools.

The website and Unity package are separate applications. The website does not replace the Unity Editor manager, and the manager's core features work locally inside Unity.

---

# 1. Problem statement

Game-development projects commonly use temporary content while ideas and gameplay are being prototyped. This can include greybox objects, temporary models, draft textures, test audio, prototype characters, placeholder dialogue, and other assets that are expected to be replaced or consciously approved before release.

As a project grows, it becomes harder to remember where temporary content is used, which items still need attention, and whether a proposed replacement has actually been applied. A temporary object can remain in a scene simply because nobody remembers that it was temporary. Developers may also have difficulty reviewing project readiness before creating a build.

Asset creation can be fragmented too. A developer may manage placeholders in Unity, use separate websites or applications to create replacement assets, and search elsewhere for installation instructions and documentation.

## The problem in short

- Temporary content can be forgotten and accidentally left in a project.
- Developers need a reliable way to identify and track known placeholders.
- Automatically detecting a possible placeholder is not the same as confirming it.
- Choosing or linking a replacement does not mean it has been applied.
- Developers need a way to review project status and check explicitly tagged placeholders before a build.
- Asset-generation tools and manager downloads can be scattered across different places.
- Users need clear documentation and guidance for installing and using the manager.

---

# 2. Proposed solution

PlaceholderGuard brings placeholder management and asset-creation access together through a Unity package and a companion website.

## 2.1 Unity Manager

The Unity Manager provides an Editor workflow to:

- Mark known placeholders with an explicit component.
- Scan supported scenes, prefabs, and selected asset categories.
- Present possible placeholders as suggestions for human review.
- Store placeholder records in a project-local JSON registry.
- Link and verify replacement assets.
- Apply supported prefab replacements after user confirmation.
- Review project status through a Unity Editor dashboard.
- Export scan reports.
- Warn or block builds when explicitly tagged, non-exempt placeholders remain in enabled Build Settings scenes.

## 2.2 Companion website

The website provides a central place to:

- Download the PlaceholderGuard Unity package.
- Generate procedural textures.
- Request AI-generated textures.
- Create procedural audio and AI-generated speech.
- Preview supported 3D primitive meshes.
- Generate AI-assisted primitive mesh recipes and export OBJ geometry.

## 2.3 Unified goal

The long-term goal is to make PlaceholderGuard a convenient companion for Unity creators: manage temporary content inside Unity, access asset-generation tools through the website, and learn how to use the manager without searching through disconnected documentation.

---

# 3. Current build — PlaceholderGuard 0.2.0

**This section describes the current baseline. The current Unity Manager version is 0.2.0.** Version 0.2.1 is a future manager build and its additional capabilities are listed in Section 4. Do not treat 0.2.1 features as part of the current 0.2.0 release.

## 3.1 Unity Manager 0.2.0

PlaceholderGuard 0.2.0 is an early source-code release for Unity 2021.3 or newer. It provides a local Editor workflow for tracking temporary content.

### 0.2.0 feature summary

| Area | Capability in 0.2.0 | Important boundary |
|---|---|---|
| Explicit placeholder tagging | `PlaceholderTag` component stores an ID, display name, category, reason, and build-exemption information | Tags are attached to GameObjects, not arbitrary files |
| Project scanning | Scans loaded and unopened scenes under `Assets`, prefab assets under `Assets`, and selected asset categories | Does not scan every Unity asset format or packages outside `Assets` |
| Heuristic suggestions | Uses name and folder hints to identify possible placeholders | Suggestions can be false positives and require human review |
| Local registry | Stores placeholder identity, context, type, reason, replacement path, status, and history in project-local JSON | It is local project data, not a cloud account |
| Replacement links | Lets users select, link, verify, and unlink replacement assets | Linking does not modify the scene or asset |
| Applying replacements | Supports confirmed prefab replacement of a scene-authored GameObject | Prefab instances, prefab contents, and asset-only records are not automatically applied |
| Undo support | Uses Unity Undo for supported scene replacement and refreshes state after Undo/Redo | Review and save the scene yourself |
| Analytics | Counts records by status, type, and scene/asset context; calculates a readiness estimate | The estimate is a project-management indicator, not a quality guarantee |
| Reports | Exports the current scan to JSON or CSV | Export reflects the latest scan held by the dashboard |
| Build protection | Warns or blocks builds for explicit, non-exempt tags in enabled Build Settings scenes | Suggestions, asset-only records, and broken-reference issues do not block builds |
| AI generation in the Unity package | Not included | AI generation belongs to the companion website or future integrations |

### How the 0.2.0 manager works

1. A developer adds a `PlaceholderTag` component to a GameObject that contains temporary content.
2. The developer runs a project scan.
3. PlaceholderGuard registers explicitly tagged objects and separately lists heuristic matches as suggestions.
4. The developer reviews suggestions and decides whether to approve or dismiss each one.
5. The developer can link a candidate replacement asset and verify that it exists and is compatible.
6. For supported scene-authored GameObjects, the developer can confirm and apply a prefab replacement.
7. The developer reviews the scene and saves it manually.
8. The dashboard provides counts, readiness information, and scan reports.
9. Before a build, the validator can warn or block when explicitly tagged, non-exempt placeholders remain in enabled Build Settings scenes.

### Manager dashboard in 0.2.0

The dashboard is opened from **Tools → PlaceholderGuard → Open Dashboard**.

The 0.2.0 dashboard provides:

- **Overview:** scan summary, placeholder totals, replacement status, analytics, readiness estimate, and build-blocking preference.
- **Placeholders:** registered records and replacement management.
- **Suggestions:** heuristic candidates for review, approval, or dismissal.
- **Reports:** scan findings and JSON/CSV export.

### Important 0.2.0 design boundaries

- Explicitly tagged placeholders are authoritative records.
- Heuristic matches are suggestions, not confirmed placeholders.
- Linking a replacement is separate from applying it.
- Applying a replacement requires user confirmation.
- Scene modifications are not saved automatically.
- The package does not silently delete or overwrite production assets.
- The core manager runs locally in the Unity Editor. Version 0.2.0 does not require a cloud account, AI service, or external backend.

### 0.2.0 limitations

- Name/folder heuristics can produce false positives.
- Scanning is limited to the supported scenes, prefabs, and selected asset categories described in the package documentation.
- Replacement application is limited to supported scene-authored GameObjects.
- Applying a prefab does not copy arbitrary components, component values, animation state, external references, or gameplay state from the old object.
- Verification checks asset existence and compatibility; it does not prove visual quality or gameplay correctness.
- Build validation only blocks explicit, non-exempt tags in enabled Build Settings scenes. Suggestions and asset-only records are not build blockers.
- The readiness score is not a guarantee that the final game is complete or production-ready.

## 3.2 Website / AI Asset Studio current build

The supplied website starter is a companion application. Its README documents these capabilities:

| Area | Current documented capability |
|---|---|
| Manager download | Bundles a PlaceholderGuard 0.2.0 ZIP download |
| Procedural textures | Generates checkerboard, grid, fractal-noise, and gradient PNGs in the browser |
| AI textures | Uses a server-side Gemini image-generation endpoint with prompts framed for flat game textures |
| Procedural audio | Local waveform synthesis with four presets, preview, and WAV export |
| AI speech | Gemini text-to-speech with voice and style selection, preview, and WAV export |
| 3D preview | Three.js primitive preview with orbit and zoom controls |
| AI-assisted 3D | Gemini returns a constrained JSON recipe for supported primitive shapes |
| 3D export | Three.js builds the recipe into a mesh and exports OBJ geometry |

### 3D generation boundary

The current website implementation does **not** receive a native 3D mesh file from Gemini. Gemini returns a constrained JSON recipe describing supported primitive shapes, and Three.js constructs the mesh from that recipe. OBJ export contains geometry; it does not currently export a Unity material file or textures alongside the OBJ.

### Current website technology and setup

- Frontend: HTML, CSS, and JavaScript.
- Backend: Node.js using the built-in modules in the supplied starter.
- Current AI provider: Gemini.
- Configuration: server-side `.env` variables.
- 3D preview and mesh construction: Three.js.
- Local development requirement: Node.js 20 or newer.
- The supplied starter README documents `npm start` and says no `npm install` is required for that version because it uses Node's built-in modules.

The Gemini API key is read by the server and must never be sent to browser JavaScript or committed to source control.

---

# 4. Features to add — future of the current build

This section describes the planned future direction. It includes the manager improvements prepared for **PlaceholderGuard 0.2.1**, plus planned website and platform improvements. These are not part of the current 0.2.0 baseline unless explicitly stated.

## 4.1 Future Unity Manager — version 0.2.1

PlaceholderGuard 0.2.1 is the next planned manager build. We plan to add improvements to detection, scanning, reporting, registry handling, and build validation.

### Planned / prepared 0.2.1 manager improvements

| Area | 0.2.1 capability | What improves compared with 0.2.0 |
|---|---|---|
| Whole-word heuristics | Tokenizes names into words, including separators, camelCase, acronym, and letter/digit boundaries | Reduces accidental matches such as a normal word containing `temp` as part of a larger word |
| More precise suggestions | Applies whole-word name matching and directory-name rules to supported assets and prefab roots | Makes heuristic suggestions more deliberate and explainable |
| Missing-reference inspection | Checks serialized object-reference properties in scanned scene and prefab hierarchies, including nested data and arrays/lists within documented limits | Adds visibility into broken serialized references |
| Missing-script reporting | Reports GameObjects with missing component scripts | Helps identify broken scene/prefab components |
| Record reconciliation | After a complete project scan, identifies tags/assets that disappeared, relocates moved records where supported, and removes stale pending suggestions | Keeps the registry more consistent with the project |
| Safer scan failure handling | A failed or partial scene scan does not cause its records to be incorrectly marked as missing | Prevents scan failures from being mistaken for deleted content |
| Better hierarchy paths | Disambiguates same-named siblings and handles escaped path characters | Makes locating a target more reliable |
| Duplicate-ID reporting | Reports independently authored tags that share an ID and identifies their locations | Helps diagnose ambiguous or duplicated records |
| Build-scene scan | Scans enabled Build Settings scenes in read-only mode | Avoids changing the registry during build validation |
| Unsaved build-scene detection | Reports loaded build scenes with unsaved changes | Makes it clear that builds use saved scene files |
| Build preference storage | Stores the block-builds setting in the project registry rather than only per-user Editor preferences | Makes the setting project-wide and shareable with the project |
| Registry migration | Reads earlier registry formats and backs up older schemas before saving the new schema version 3 | Protects existing project data during the upgrade |
| Expanded EditMode tests | Adds tests for heuristics, hierarchy paths, and registry migration/reconciliation, in addition to readiness calculations | Improves coverage of core logic |

### 0.2.1 dashboard and workflow

The 0.2.1 retains the four dashboard tabs:

- **Overview** — status counts, category/context analytics, readiness estimate, and build-blocking setting.
- **Placeholders** — tracked placeholder records and replacement management.
- **Suggestions** — heuristic candidates for user review and approval or dismissal.
- **Reports** — scan findings, missing-reference fields, missing scripts, duplicate IDs, scan errors, and JSON/CSV export.

### Important upgrade warning: 0.2.0 to 0.2.1

The 0.2.0 package shipped without `.meta` files. When upgrading an existing Unity project, preserve the old `Runtime/PlaceholderTag.cs.meta` GUID as described in `Documentation~/UPGRADE_0.2.1.md`. Replacing the package without preserving the relevant GUID can cause existing scene and prefab components to appear as Missing Script.

The 0.2.1 registry reader accepts earlier schemas and backs up an older registry before its first save in schema version 3. Back up the Unity project and registry before upgrading. Follow the actual upgrade guide rather than simply deleting the old folder and copying the new one.

## 4.2 Supabase authentication

Add Supabase Auth to the website.

Planned capabilities:

- User registration and login.
- Logout and session management.
- Protected user areas.
- User-specific generation history.
- Per-user usage limits and quotas.
- Association of generated assets and generation jobs with the correct account.

Security requirements:

- Keep privileged Supabase keys on the server.
- Never expose a Supabase service-role key in frontend JavaScript.
- Apply Row Level Security (RLS) to user-owned records.
- Verify user sessions and permissions in the backend before protected actions.

## 4.3 Better AI support and provider flexibility

Improve the AI integration so the website is not tied permanently to one provider.

Planned capabilities:

- Add OpenRouter support as an alternative AI provider.
- Keep provider communication in reusable backend services.
- Make model selection configurable.
- Allow specialist providers to be connected for image, audio, speech, and 3D generation.
- Improve prompt handling, input validation, timeouts, retries, and error messages.
- Clearly indicate which generation types are supported by each model.

**Provider limitation:** OpenRouter provides access to models through its API, but not every model supports image, audio, speech, or 3D outputs. Each feature must use a model and endpoint that genuinely support the requested output. A text model should not be presented as a native image, audio, or 3D generator.

## 4.4 Better website UI and user experience

Improve the interface while keeping it simple and understandable.

Planned improvements:

- Clear navigation between Manager Downloads and AI Asset Studio.
- More consistent layouts for texture, audio, and 3D tools.
- Better loading, progress, success, and error states.
- Clear previews and download actions.
- Responsive layouts for different screen sizes.
- Better account and settings pages.
- More accessible labels and explanations for generation options.

## 4.5 Manager documentation inside the website UI

Add a dedicated **PlaceholderGuard Documentation** section directly to the website.

Planned documentation:

- What PlaceholderGuard does.
- Installation and first-time setup.
- How to add and configure `PlaceholderTag`.
- How to scan a project.
- How to understand and review Suggestions.
- How to link, verify, and apply replacement assets.
- How Undo works and when scenes must be saved.
- How to read the dashboard and readiness estimate.
- How to export JSON and CSV reports.
- How build validation works and what it does not check.
- Version-specific installation and upgrade instructions.
- Troubleshooting and frequently asked questions.

The documentation should be searchable, accessible without leaving the website, and matched to the manager version the user downloads.

## 4.6 Better generation workflow and asset management

Planned improvements:

- Track long-running generation requests with job IDs.
- Show queued, processing, completed, failed, and cancelled states where supported.
- Keep per-user generation history.
- Provide asset previews and download links.
- Organize generated textures, audio, and models.
- Validate uploaded and generated files.
- Add size limits, cleanup, and storage management.
- Use persistent job records instead of relying only on in-memory state.

## 4.7 Further Unity Manager development

After 0.2.1 is tested, future manager work may explore:

- Broader asset-type coverage.
- More complete asset-reference analysis and repair workflows.
- Additional replacement workflows for asset-only records and prefab-related cases.
- More detailed reporting and team workflows.
- Better in-Editor onboarding and contextual help.
- Further Unity-version compatibility and automated integration testing.

These are future possibilities, not existing 0.2.0 or 0.2.1 capabilities.

---

# 5. Proposed technology stack

The current direction is to keep the website on JavaScript and Node.js. A Python backend migration is not part of the current plan.

| Layer | Technology | Responsibility |
|---|---|---|
| Frontend | HTML, CSS, JavaScript | Website pages, controls, previews, and API requests |
| Backend | Node.js | Server-side application logic |
| HTTP framework | Existing server implementation; Express can be considered if adopted | API routing and middleware |
| Current AI provider | Gemini | Current website image, speech, and mesh-recipe features |
| Planned AI provider | OpenRouter and specialist providers | More model and provider options |
| Authentication | Supabase Auth — planned | Registration, login, and sessions |
| Database | Supabase — planned where persistent user/job data is needed | User records, job history, and metadata |
| Configuration | `.env` / deployment environment variables | Server-side keys and settings |
| 3D preview | Three.js | Browser-side primitive preview and OBJ export |
| Storage | Local storage initially; future hosted storage to be evaluated | Generated files and downloads |

The actual implementation should follow the dependencies present in the project. Do not add a framework or package solely because it appears in this roadmap.

---

# 6. Proposed website project structure

The following is a target structure for future modular development. It is not a claim that every listed file already exists.

```text
PlaceholderGuard-Web/
│
├── server.js
├── package.json
├── package-lock.json
├── .env
├── .env.example
├── .gitignore
├── START-WINDOWS.bat
├── README.md
│
├── public/
│   ├── index.html
│   ├── styles.css
│   ├── app.js
│   ├── pages/
│   ├── assets/
│   └── docs/
│
├── backend/
│   ├── config/
│   ├── routes/
│   ├── services/
│   ├── middleware/
│   └── utils/
│
├── downloads/
│   └── PlaceholderGuard-0.2.0.zip
│
└── generated/
    ├── textures/
    ├── audio/
    └── models/
```

The current server should be preserved while the architecture is modularized gradually. The folder diagram is a development target, not a requirement to restructure everything immediately.

---

# 7. Security and deployment requirements

The supplied website starter is a local-development application, not a production-ready public service.

Before public deployment, the project should add and verify:

- Supabase authentication and server-side authorization.
- Row Level Security for user-owned records.
- Per-user quotas and persistent rate limiting.
- Request and file-size validation.
- Safe file storage and access controls.
- Request logging and error monitoring.
- HTTPS and reverse-proxy configuration.
- Provider usage limits and a budget cap.
- Secret management through deployment environment variables.
- Protection against users accessing other users' generated files or job records.

Never put Gemini, OpenRouter, or Supabase privileged keys in `public/`, browser JavaScript, or downloadable frontend assets. The browser should call the application backend; the backend should communicate with providers using server-side credentials.

---

# 8. Running the current website starter

Follow the instructions shipped with the actual website project. The supplied website README documents this local setup:

1. Extract the website ZIP.
2. Open the extracted project folder.
3. Copy `.env.example` to `.env`.
4. Add the Gemini key to `.env` for the current Gemini-backed AI endpoints.
5. Start the website using the supplied `START-WINDOWS.bat` or the documented terminal command.
6. Open `http://127.0.0.1:3000`.
7. Keep the server terminal open while using the website.

The supplied starter README specifies Node.js 20 or newer, uses Node's built-in modules, and documents `npm start` without requiring `npm install` for that version. Re-check these instructions if dependencies are added later.

Example current environment variable:

```env
GEMINI_API_KEY=your_key_here
```

OpenRouter is a planned provider addition. Changing the environment variable name alone will not complete that integration; the backend request format, model selection, and supported output type must also be updated.

---

# 9. Installing the current Unity Manager 0.2.0

Follow the 0.2.0 package README included with the manager download.

Typical installation:

1. Back up the Unity project.
2. Extract the manager ZIP.
3. Locate the inner `PlaceholderGuard` folder.
4. Copy it into the Unity project's `Packages` directory.
5. Confirm that `package.json` is directly inside `Packages/PlaceholderGuard/`.
6. Reopen Unity and wait for compilation.
7. Open **Tools → PlaceholderGuard → Open Dashboard**.
8. Test in a disposable Unity project and inspect the Unity Console before using it in a production project.

The current website download is documented as PlaceholderGuard 0.2.0. Do not label the website download as 0.2.1 until the bundled ZIP has actually been replaced and verified.

---

# 10. Development roadmap

A sensible order for the project is:

1. **Stabilize the current 0.2.0 baseline.** Verify the website starts, the manager ZIP downloads, and the Unity package compiles in the target editor.
2. **Prepare PlaceholderGuard 0.2.1.** Review its changes, preserve required `.meta` GUIDs during upgrade, compile it in Unity, and run its EditMode tests.
3. **Update the website manager download deliberately.** Only after 0.2.1 is tested should the website download and version label be changed.
4. **Improve website UI and navigation.** Keep Manager Downloads and AI Asset Studio clearly separated.
5. **Add manager documentation inside the website UI.** Keep it version-aware and searchable.
6. **Improve the AI provider layer.** Add OpenRouter and specialist providers without exposing keys or assuming all models support all output types.
7. **Add Supabase authentication.** Protect user-specific endpoints and records.
8. **Add persistent generation jobs, history, quotas, and storage controls.**
9. **Improve deployment readiness and security.**

---

# 11. Known limitations and version boundaries

- **0.2.0 is the current manager baseline** described in the current-build section.
- **0.2.1 is the future manager build** described in the roadmap section. Its features must not be represented as already available in 0.2.0.
- The supplied 0.2.1 README states that it has not yet been compiled or run in Unity. Test it before relying on it.
- The current website README documents a 0.2.0 manager ZIP. The website download must be updated and verified before it can be called 0.2.1.
- The current website's AI integration uses Gemini. OpenRouter support and provider switching are planned, not current features.
- The current 3D website tool constructs meshes from constrained primitive recipes; it is not a general-purpose native 3D model generator.
- Current OBJ export contains geometry only, without a Unity material file or accompanying textures.
- Supabase authentication, integrated manager documentation, and the improved website UI are planned features.
- The current website starter is intended for local development and is not ready for unrestricted public multi-user use.

---

# 12. Project objective

The long-term objective is to make PlaceholderGuard a useful development companion for Unity creators:

- **Identify and track** temporary content inside Unity.
- **Review and replace** placeholders through a controlled workflow.
- **Validate** explicitly tagged placeholders before builds.
- **Generate or prepare** useful game-development assets through the companion website.
- **Learn and troubleshoot** through documentation integrated into the website.
- **Access the manager and asset tools** through one clearer experience.

PlaceholderGuard should remain transparent and safe to use: suggestions must not be confused with confirmed issues, replacements must not silently modify scenes, and AI-generated results must be clearly identified with their actual capabilities and limitations.

---

## Source documents used

This README is based on the supplied documents:

- **Website starter README** — current website functionality, Gemini integration, local setup, and the bundled PlaceholderGuard 0.2.0 download.
- **PlaceholderGuard 0.2.0 README** — current manager features, workflow, dashboard, registry, scanning, replacement handling, analytics, reports, build validation, limitations, and tests.
- **PlaceholderGuard 0.2.1 README** — future manager improvements, upgrade notes, registry migration, new scan/report behavior, and test coverage.

**Versioning rule for this document:** 0.2.0 describes the current manager build. 0.2.1 describes future manager capabilities until it has been compiled, tested, and deliberately adopted as the website's downloadable release.
