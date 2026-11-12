# PlaceholderGuard 0.2.0 — Quick Start

## 1. Install

Copy the extracted `PlaceholderGuard` directory into the project's `Packages` folder. Confirm `package.json` is directly inside `Packages/PlaceholderGuard/`. Wait for Unity to compile.

Open **Tools → PlaceholderGuard → Open Dashboard**.

## 2. Tag a placeholder manually

1. Select a scene GameObject.
2. In the Inspector choose **Add Component**.
3. Search for `Placeholder Tag` and add it.
4. Set its display name, reason, and asset category.
5. Open PlaceholderGuard and click **Scan Entire Project**.

An explicitly tagged object appears under **Placeholders** and in the report. The scan checks loaded and unopened scenes under `Assets`, plus prefab assets.

## 3. Link a replacement

1. Open the **Placeholders** tab.
2. Find the placeholder and click **Manage Replacement**.
3. Drag a compatible production asset from the Project window into **Replacement asset**.
4. Click **Link Replacement**.
5. Click **Verify Link** to check that the asset still exists and passes the type check.

Linking and verifying do not modify the scene object. A linked or verified placeholder remains a build blocker unless it is applied or explicitly exempted with a non-empty reason.

## 4. Apply a replacement to a scene object

1. Link and verify a prefab replacement.
2. Select the placeholder record in the dashboard.
3. Click **Apply Replacement to Scene** and confirm.
4. Review the scene. The replacement prefab is placed at the same local transform, the old object's children are reparented to the replacement, and the scene is marked dirty.
5. Save the scene yourself when satisfied.

Use **Edit → Undo** (Ctrl+Z) to revert the operation while it is still in Unity's Undo history. Automatic application is limited to scene-authored GameObjects; prefab instances and prefab asset contents are deliberately not auto-applied in this release.

## 5. Review detection suggestions

After **Scan Entire Project**, open **Suggestions**.

- Candidates are based on placeholder-related names and folders.
- The confidence value is a heuristic score, not a machine-learning probability.
- **Approve as Placeholder** adds a `PlaceholderTag` to a scene object. For a prefab, PlaceholderGuard backs up the prefab under `Library/PlaceholderGuardBackups` before saving the added component.
- Approving a standalone asset creates an asset-only record; that record is advisory and is not currently a scene build blocker.
- **Dismiss** keeps the candidate out of the pending list.

Review each candidate. Do not bulk-approve based only on confidence.

## 6. Review analytics and export

- **Overview**: status counts, category counts, context counts, and readiness estimate.
- **Reports**: scan findings, broken object-reference fields, missing scripts, repeated IDs, and scan errors.
- Export the current scan as JSON or CSV from the Reports tab.

Readiness formula: `(applied records + explicitly exempt records with a non-empty reason) / all tracked records × 100`. With no records, the score is 100. It is an estimate and does not replace an actual build or QA pass.

## 7. Build validation

Build protection is enabled by default. It checks enabled Build Settings scenes for non-exempt `PlaceholderTag` components (an exemption requires a non-empty reason). Linking or verifying a replacement does not make the tag disappear; apply the replacement or remove the tag yourself after review. Warning-only mode can be selected from the Overview tab.

## Troubleshooting

- If the Tools menu is missing, wait for compilation and inspect the Console for errors.
- If a replacement picker selection is rejected, check the placeholder category and selected asset type.
- If Apply cannot find the object, run a fresh project scan. The tool refuses to guess when the stored object path/ID is ambiguous.
- If the scene changed after applying, save it manually. PlaceholderGuard never saves scenes automatically.
- If the registry JSON becomes malformed, PlaceholderGuard attempts to preserve a timestamped `.bak` copy before loading an empty registry. Restore a known-good registry backup if needed.

## Scope of this release

The 0.2.0 release includes replacement management for scene-authored objects, heuristic detection, serialized missing-reference checks, dashboard analytics, reports, and existing build blocking. AI-generated art, model generation, animation generation, TTS synthesis, universal asset-reference repair, and Unreal support are future work.
