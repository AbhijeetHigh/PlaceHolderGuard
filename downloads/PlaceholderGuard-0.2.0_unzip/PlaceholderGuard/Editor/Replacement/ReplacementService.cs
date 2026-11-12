using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlaceholderGuard.Editor
{
    public static class ReplacementService
    {
        public static bool ValidateReplacement(PlaceholderRecord record, string replacementPath, out string message)
        {
            message = string.Empty;
            if (record == null) { message = "Select a placeholder first."; return false; }
            if (string.IsNullOrEmpty(replacementPath) || !replacementPath.StartsWith("Assets/", StringComparison.Ordinal))
            { message = "Choose an asset inside the project's Assets folder."; return false; }

            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(replacementPath);
            if (asset == null) { message = "The selected asset could not be loaded. Refresh the Project window and try again."; return false; }

            string type = record.assetType ?? "Other";
            bool compatible = type == "Other" ||
                (type == "Audio" && asset is AudioClip) ||
                (type == "Texture" && asset is Texture) ||
                (type == "Material" && asset is Material) ||
                (type == "Model" && asset is GameObject) ||
                (type == "Dialogue" && (asset is TextAsset || asset is ScriptableObject)) ||
                (type == "Animation" && (asset is AnimationClip || asset is RuntimeAnimatorController)) ||
                (type == "Script" && asset is MonoScript);

            if (!compatible)
            {
                message = "The selected asset type does not match the placeholder category '" + type + "'.";
                return false;
            }

            if (record.targetKind == "gameObject" && !(asset is GameObject))
            {
                message = "GameObject placeholders must link to a prefab or GameObject asset. Other asset categories can still be linked to asset-only records.";
                return false;
            }
            if (record.targetKind == "gameObject" && PrefabUtility.GetPrefabAssetType(asset as GameObject) == PrefabAssetType.NotAPrefab)
            {
                message = "Choose a prefab asset, not a scene GameObject.";
                return false;
            }
            return true;
        }

        public static bool VerifyLink(PlaceholderRecord record, out string message)
        {
            string ignored;
            if (record == null) { message = "Select a placeholder first."; return false; }
            if (!ValidateReplacement(record, record.replacementAssetPath, out ignored))
            {
                message = "Replacement link is invalid: " + ignored;
                return false;
            }
            if (record.replacementStatus != "applied")
            {
                AssetRegistry.SetReplacementStatus(record.assetId, "verified",
                    "Replacement asset exists and passed the category compatibility check. This does not mean it has been applied to the scene.");
                message = "Replacement asset exists and passed the compatibility check. It is verified as a link, not applied to the scene.";
            }
            else
            {
                message = "Replacement asset link is valid. The record remains Applied; verification did not change the applied state.";
            }
            return true;
        }

        public static bool ApplyToScene(PlaceholderRecord record, out string message)
        {
            message = string.Empty;
            if (record == null) { message = "Select a placeholder first."; return false; }
            if (record.targetKind != "gameObject")
            { message = "Only tagged GameObject placeholders can be applied automatically in this release."; return false; }
            if (string.IsNullOrEmpty(record.sceneOrPrefab) || !record.sceneOrPrefab.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            { message = "Automatic application currently supports scene instances. Prefab asset records can be linked and verified, but must be edited manually."; return false; }

            string validation;
            if (!ValidateReplacement(record, record.replacementAssetPath, out validation))
            { message = validation; return false; }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(record.replacementAssetPath);
            if (prefab == null)
            { message = "Replacement prefab could not be loaded."; return false; }

            Scene scene = SceneManager.GetSceneByPath(record.sceneOrPrefab);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                try { scene = EditorSceneManager.OpenScene(record.sceneOrPrefab, OpenSceneMode.Additive); }
                catch (Exception ex) { message = "Could not open target scene: " + ex.Message; return false; }
            }

            Transform target = FindTarget(scene, record.gameObjectPath, record.assetId);
            if (target == null)
            { message = "Could not find the tagged object in the recorded scene. Run Scan Project and try again."; return false; }

            GameObject original = target.gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(original))
            {
                message = "This object belongs to a prefab instance. To avoid replacing every instance or creating an unsafe prefab override, automatic application is limited to scene-authored GameObjects in this release. Link and verify the asset, then edit the source prefab deliberately.";
                return false;
            }
            Transform originalTransform = original.transform;
            Transform parent = originalTransform.parent;
            Vector3 localPosition = originalTransform.localPosition;
            Quaternion localRotation = originalTransform.localRotation;
            Vector3 localScale = originalTransform.localScale;
            int siblingIndex = originalTransform.GetSiblingIndex();
            string oldName = original.name;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("PlaceholderGuard: Apply replacement");
            try
            {
                GameObject replacement = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (replacement == null) throw new InvalidOperationException("Unity could not instantiate the replacement prefab.");
                Undo.RegisterCreatedObjectUndo(replacement, "Create replacement prefab");
                if (parent != null) Undo.SetTransformParent(replacement.transform, parent, "Parent replacement prefab");
                Undo.RecordObject(replacement.transform, "Position replacement prefab");
                replacement.transform.localPosition = localPosition;
                replacement.transform.localRotation = localRotation;
                replacement.transform.localScale = localScale;
                replacement.transform.SetSiblingIndex(siblingIndex);

                // Preserve children rather than destroying their contents with the old placeholder.
                while (original.transform.childCount > 0)
                {
                    Transform child = original.transform.GetChild(0);
                    Undo.SetTransformParent(child, replacement.transform, "Preserve placeholder child");
                }

                EditorSceneManager.MarkSceneDirty(scene);
                Undo.DestroyObjectImmediate(original);
                Undo.CollapseUndoOperations(undoGroup);

                PlaceholderRecord savedRecord = AssetRegistry.Find(record.assetId);
                if (savedRecord != null)
                {
                    savedRecord.replacementStatus = "applied";
                    savedRecord.updatedAtUtc = DateTime.UtcNow.ToString("o");
                    AssetRegistry.AddHistory(record.assetId, "applied", record.replacementAssetPath,
                        record.replacementAssetPath, record.sceneOrPrefab,
                        "Replaced scene object '" + oldName + "' with prefab '" + prefab.name + "'. Unity Undo can revert this scene operation while it remains in the Undo history.");
                    AssetRegistry.Save();
                }
                message = "Replacement instantiated in the scene. The scene is marked dirty; save it when ready. Use Edit > Undo (Ctrl+Z) to revert while this remains the latest Undo operation.";
                return true;
            }
            catch (Exception ex)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                message = "Replacement was not completed; Unity Undo reverted the partial operation. " + ex.Message;
                return false;
            }
        }

        private static Transform FindTarget(Scene scene, string objectPath, string assetId)
        {
            Transform match = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform item in transforms)
                {
                    if (PlaceholderScanner.GetHierarchyPath(item) != objectPath) continue;
                    PlaceholderTag tag = item.GetComponent<PlaceholderTag>();
                    if (tag == null || tag.AssetId != assetId) continue;
                    if (match != null) return null; // Ambiguous duplicate path/ID: do not guess.
                    match = item;
                }
            }
            return match;
        }
    }

    [InitializeOnLoad]
    internal static class ReplacementUndoWatcher
    {
        static ReplacementUndoWatcher()
        {
            Undo.undoRedoPerformed -= RefreshAppliedStatuses;
            Undo.undoRedoPerformed += RefreshAppliedStatuses;
        }

        private static void RefreshAppliedStatuses()
        {
            try
            {
                AssetRegistry.Load();
                bool changed = false;
                foreach (PlaceholderRecord record in AssetRegistry.Data.placeholders)
                {
                    PlaceholderHistoryEntry latest = null;
                    for (int h = AssetRegistry.Data.history.Count - 1; h >= 0; h--)
                    {
                        if (AssetRegistry.Data.history[h].placeholderId == record.assetId)
                        { latest = AssetRegistry.Data.history[h]; break; }
                    }

                    bool originalIsPresent = false;
                    for (int i = 0; i < SceneManager.sceneCount && !originalIsPresent; i++)
                    {
                        Scene scene = SceneManager.GetSceneAt(i);
                        if (!scene.IsValid() || !scene.isLoaded) continue;
                        foreach (GameObject root in scene.GetRootGameObjects())
                        {
                            PlaceholderTag[] tags = root.GetComponentsInChildren<PlaceholderTag>(true);
                            if (Array.Exists(tags, t => t != null && t.AssetId == record.assetId))
                            { originalIsPresent = true; break; }
                        }
                    }

                    if (originalIsPresent && (record.replacementStatus == "applied" || record.replacementStatus == "verified"))
                    {
                        record.replacementStatus = string.IsNullOrEmpty(record.replacementAssetPath) ? "unlinked" : "linked";
                        record.updatedAtUtc = DateTime.UtcNow.ToString("o");
                        AssetRegistry.AddHistory(record.assetId, "undo-detected", record.replacementAssetPath,
                            record.replacementAssetPath, record.sceneOrPrefab, "Unity Undo restored the original tagged GameObject.");
                        changed = true;
                    }
                    else if (!originalIsPresent && record.replacementStatus != "applied" && latest != null && latest.action == "undo-detected")
                    {
                        record.replacementStatus = "applied";
                        record.updatedAtUtc = DateTime.UtcNow.ToString("o");
                        AssetRegistry.AddHistory(record.assetId, "redo-detected", record.replacementAssetPath,
                            record.replacementAssetPath, record.sceneOrPrefab, "Unity Redo reapplied the replacement operation.");
                        changed = true;
                    }
                }
                if (changed) AssetRegistry.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlaceholderGuard] Could not refresh replacement status after Undo/Redo: " + ex.Message);
            }
        }
    }
}
