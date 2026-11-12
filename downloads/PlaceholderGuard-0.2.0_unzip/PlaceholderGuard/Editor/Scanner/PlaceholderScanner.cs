using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlaceholderGuard.Editor
{
    public static class PlaceholderScanner
    {
        private static readonly string[] PlaceholderTerms =
        {
            "placeholder", "temporary", "temp_", "_temp", "dummy", "proxy",
            "blockout", "greybox", "graybox", "prototype", "wip"
        };

        private static readonly string[] PlaceholderFolders =
        {
            "/placeholder", "/placeholders", "/temp/", "/temporary/", "/blockout/",
            "/greybox/", "/graybox/", "/prototype/", "/prototypes/"
        };

        public static ScanReport ScanProject()
        {
            AssetRegistry.Load();
            var report = NewReport("project");
            var scannedScenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                ScanLoadedScenes(report, scannedScenePaths);
                ScanAllSceneAssets(report, scannedScenePaths);
                ScanPrefabAssets(report);
                ScanCandidateAssets(report);
                AddDuplicateIdIssues(report);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetRegistry.Save();
            }

            report.placeholderCount = report.findings.Count(f => f.findingType == "tagged");
            report.suggestions = AssetRegistry.Data.suggestions.Where(s => s.status == "pending").ToList();
            report.suggestionCount = report.suggestions.Count;
            report.missingReferenceCount = report.issues.Count(i => i.category == "missing-reference");
            report.missingScriptCount = report.issues.Count(i => i.category == "missing-script");
            report.duplicateIdCount = report.issues.Count(i => i.category == "duplicate-id");
            return report;
        }

        public static ScanReport ScanBuildScenes()
        {
            AssetRegistry.Load();
            var report = NewReport("build-scenes");
            foreach (var sceneSetting in EditorBuildSettings.scenes)
            {
                if (!sceneSetting.enabled || string.IsNullOrEmpty(sceneSetting.path)) continue;
                ScanScenePath(sceneSetting.path, report);
            }

            AddDuplicateIdIssues(report);
            AssetRegistry.Save();
            report.placeholderCount = report.findings.Count(f => f.findingType == "tagged");
            report.missingReferenceCount = report.issues.Count(i => i.category == "missing-reference");
            report.missingScriptCount = report.issues.Count(i => i.category == "missing-script");
            report.duplicateIdCount = report.issues.Count(i => i.category == "duplicate-id");
            return report;
        }

        private static ScanReport NewReport(string scope)
        {
            return new ScanReport { scannedAtUtc = DateTime.UtcNow.ToString("o"), scanScope = scope };
        }

        private static void ScanLoadedScenes(ScanReport report, HashSet<string> scannedPaths)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded && !string.IsNullOrEmpty(scene.path) && scannedPaths.Add(scene.path))
                    ScanScene(scene, report);
            }
        }

        private static void ScanAllSceneAssets(ScanReport report, HashSet<string> scannedPaths)
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!scannedPaths.Add(path)) continue;
                EditorUtility.DisplayProgressBar("PlaceholderGuard", "Scanning scene " + path, (float)i / Math.Max(1, guids.Length));
                ScanScenePath(path, report);
            }
        }

        private static void ScanScenePath(string path, ScanReport report)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                ScanScene(scene, report);
            }
            catch (Exception ex)
            {
                report.issues.Add(new ScanIssue
                {
                    severity = "warning",
                    category = "scan-error",
                    contextPath = path,
                    message = "Could not scan scene: " + ex.Message
                });
                Debug.LogWarning("[PlaceholderGuard] Could not scan scene " + path + ": " + ex.Message);
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ScanScene(Scene scene, ScanReport report)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            report.scenesScanned++;
            foreach (var root in scene.GetRootGameObjects())
                ScanHierarchy(root, scene.path, report);
        }

        private static void ScanPrefabAssets(ScanReport report)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefabRoot = null;
                EditorUtility.DisplayProgressBar("PlaceholderGuard", "Scanning prefab " + path, (float)i / Math.Max(1, guids.Length));
                try
                {
                    prefabRoot = PrefabUtility.LoadPrefabContents(path);
                    if (prefabRoot != null)
                    {
                        report.prefabsScanned++;
                        ScanHierarchy(prefabRoot, path, report);
                    }
                }
                catch (Exception ex)
                {
                    report.issues.Add(new ScanIssue
                    {
                        severity = "warning", category = "scan-error", contextPath = path,
                        message = "Could not scan prefab: " + ex.Message
                    });
                }
                finally
                {
                    if (prefabRoot != null) PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static void ScanHierarchy(GameObject root, string contextPath, ScanReport report)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var transform in transforms)
            {
                string objectPath = GetHierarchyPath(transform);
                InspectMissingReferences(transform.gameObject, contextPath, objectPath, report);

                var tag = transform.GetComponent<PlaceholderTag>();
                if (tag != null)
                {
                    var record = AssetRegistry.Register(tag, contextPath, objectPath, false);
                    report.findings.Add(new ScanFinding
                    {
                        findingType = "tagged",
                        assetId = tag.AssetId,
                        displayName = string.IsNullOrWhiteSpace(tag.DisplayName) ? tag.name : tag.DisplayName,
                        sceneOrPrefab = contextPath,
                        gameObjectPath = objectPath,
                        assetType = tag.AssetType.ToString(),
                        replacementStatus = record.replacementStatus,
                        exemptFromBuildBlock = tag.ExemptFromBuildBlock,
                        exemptionReason = tag.ExemptionReason,
                        message = "Explicitly tagged placeholder is present. Linking a replacement does not remove this finding."
                    });
                }
                else
                {
                    PlaceholderSuggestion suggestion = BuildGameObjectSuggestion(transform.gameObject, contextPath, objectPath);
                    if (suggestion != null) AssetRegistry.UpsertSuggestion(suggestion);
                }
            }
        }

        private static void InspectMissingReferences(GameObject gameObject, string contextPath,
            string objectPath, ScanReport report)
        {
            Component[] components = gameObject.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null)
                {
                    report.issues.Add(new ScanIssue
                    {
                        severity = "error", category = "missing-script", contextPath = contextPath,
                        objectPath = objectPath, message = "GameObject has a missing MonoBehaviour script component."
                    });
                    continue;
                }

                try
                {
                    SerializedObject serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    bool enterChildren = true;
                    while (property.Next(enterChildren))
                    {
                        enterChildren = false;
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (property.name == "m_Script") continue;
                        if (property.objectReferenceValue == null)
                        {
                            bool hasMissingReference;
#if UNITY_6000_4_OR_NEWER
                            hasMissingReference = property.objectReferenceEntityIdValue != EntityId.None;
#else
                            hasMissingReference = property.objectReferenceInstanceIDValue != 0;
#endif
                            if (hasMissingReference)
                            {
                                report.issues.Add(new ScanIssue
                                {
                                    severity = "error", category = "missing-reference", contextPath = contextPath,
                                    objectPath = objectPath, propertyPath = property.propertyPath,
                                    message = component.GetType().Name + " has a missing object reference at " + property.propertyPath + "."
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    report.issues.Add(new ScanIssue
                    {
                        severity = "warning", category = "inspection-warning", contextPath = contextPath,
                        objectPath = objectPath, message = "Could not inspect " + component.GetType().Name + ": " + ex.Message
                    });
                }
            }
        }

        private static PlaceholderSuggestion BuildGameObjectSuggestion(GameObject gameObject, string contextPath, string objectPath)
        {
            string reason;
            float confidence;
            if (!EvaluateHeuristics(gameObject.name, contextPath, out confidence, out reason)) return null;
            string key = "go|" + contextPath + "|" + objectPath;
            return new PlaceholderSuggestion
            {
                suggestionId = StableId(key), suggestionKey = key, targetKind = "gameObject",
                contextPath = contextPath, gameObjectPath = objectPath, displayName = gameObject.name,
                assetType = "Other", confidence = confidence, reason = reason
            };
        }

        private static void ScanCandidateAssets(ScanReport report)
        {
            string[][] filters =
            {
                new[] { "t:Texture2D", "Texture" },
                new[] { "t:Material", "Material" },
                new[] { "t:AudioClip", "Audio" },
                new[] { "t:AnimationClip", "Animation" },
                new[] { "t:Prefab", "Model" }
            };
            foreach (string[] filter in filters)
            {
                string[] guids = AssetDatabase.FindAssets(filter[0], new[] { "Assets" });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    report.assetsChecked++;
                    string reason;
                    float confidence;
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (!EvaluateHeuristics(fileName, path, out confidence, out reason)) continue;
                    string key = "asset|" + path;
                    AssetRegistry.UpsertSuggestion(new PlaceholderSuggestion
                    {
                        suggestionId = StableId(key), suggestionKey = key, targetKind = "asset",
                        assetPath = path, contextPath = path, displayName = fileName,
                        assetType = filter[1], confidence = confidence, reason = reason
                    });
                }
            }
        }

        private static bool EvaluateHeuristics(string itemName, string path, out float confidence, out string reason)
        {
            string name = (itemName ?? string.Empty).ToLowerInvariant();
            string lowerPath = (path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
            if (name.StartsWith("temp", StringComparison.Ordinal) && !name.StartsWith("template", StringComparison.Ordinal) && name.Length > 4)
            {
                confidence = 0.78f;
                reason = "Name starts with the temporary-content prefix 'temp'. Review before approving.";
                return true;
            }
            foreach (string term in PlaceholderTerms)
            {
                if (name.Contains(term))
                {
                    confidence = (term == "placeholder" || term == "dummy" || term == "blockout") ? 0.92f : 0.78f;
                    reason = "Name contains the placeholder-related term '" + term + "'. Review before approving.";
                    return true;
                }
            }
            foreach (string folder in PlaceholderFolders)
            {
                if (lowerPath.Contains(folder))
                {
                    confidence = 0.72f;
                    reason = "Asset is stored in a folder commonly used for temporary or prototype content: " + folder.Trim('/');
                    return true;
                }
            }
            confidence = 0f;
            reason = string.Empty;
            return false;
        }

        private static void AddDuplicateIdIssues(ScanReport report)
        {
            var duplicates = report.findings.Where(f => f.findingType == "tagged" && !string.IsNullOrEmpty(f.assetId))
                .GroupBy(f => f.assetId).Where(g => g.Count() > 1);
            foreach (var group in duplicates)
            {
                report.issues.Add(new ScanIssue
                {
                    severity = "warning", category = "duplicate-id",
                    message = "Placeholder ID " + group.Key + " appears in " + group.Count() +
                              " scanned contexts. This can be legitimate for prefab instances; review whether these are separate placeholders."
                });
            }
        }

        public static string GetHierarchyPath(Transform transform)
        {
            var parts = new Stack<string>();
            while (transform != null)
            {
                parts.Push(transform.name);
                transform = transform.parent;
            }
            return string.Join("/", parts.ToArray());
        }

        public static string StableId(string value)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                return new Guid(bytes).ToString("N");
            }
        }
    }
}
