using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlaceholderGuard.Editor
{
    public sealed class PlaceholderGuardWindow : EditorWindow
    {
        private ScanReport lastReport;
        private Vector2 scroll;
        private Vector2 historyScroll;
        private bool blockBuilds = true;
        private int selectedTab;
        private string selectedAssetId;
        private string searchText = string.Empty;
        private UnityEngine.Object replacementSelection;
        private string statusMessage;
        private MessageType statusMessageType = MessageType.Info;

        private readonly string[] tabNames = { "Overview", "Placeholders", "Suggestions", "Reports" };

        [MenuItem("Tools/PlaceholderGuard/Open Dashboard")]
        public static void Open()
        {
            GetWindow<PlaceholderGuardWindow>("PlaceholderGuard");
        }

        private void OnEnable()
        {
            blockBuilds = EditorPrefs.GetBool("PlaceholderGuard/BlockBuilds", true);
            AssetRegistry.Load();
        }

        private void OnGUI()
        {
            DrawHeader();
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
            EditorGUILayout.Space(8);
            switch (selectedTab)
            {
                case 0: DrawOverview(); break;
                case 1: DrawPlaceholders(); break;
                case 2: DrawSuggestions(); break;
                case 3: DrawReports(); break;
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("PlaceholderGuard", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Track temporary assets, manage replacements and review project readiness.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Entire Project", GUILayout.Height(28))) RunProjectScan();
                if (GUILayout.Button("Scan Build Scenes", GUILayout.Height(28))) RunBuildScan();
            }
            if (!string.IsNullOrEmpty(statusMessage))
                EditorGUILayout.HelpBox(statusMessage, statusMessageType);
            EditorGUILayout.Space(3);
        }

        private void DrawOverview()
        {
            var data = AssetRegistry.Data;
            var summary = PlaceholderAnalytics.BuildSummary(data);
            EditorGUILayout.LabelField("Project readiness", EditorStyles.boldLabel);
            Rect scoreRect = GUILayoutUtility.GetRect(1, 24, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(scoreRect, summary.readinessScore / 100f, "Readiness estimate: " + summary.readinessScore + "%");
            EditorGUILayout.HelpBox("Score formula: applied placeholders and explicitly exempt records count as resolved. Linked or verified links alone do not count as resolved because the placeholder may still remain in the scene. Automatic suggestions do not affect this score.", MessageType.Info);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                DrawMetric("Registered placeholders", summary.totalTracked.ToString());
                DrawMetric("Unlinked", summary.unlinked.ToString());
                DrawMetric("Linked", summary.linked.ToString());
                DrawMetric("Verified links", summary.verified.ToString());
                DrawMetric("Applied replacements", summary.applied.ToString());
                DrawMetric("Explicitly exempt", summary.exempt.ToString());
                DrawMetric("Pending suggestions", summary.pendingSuggestions.ToString());
                DrawMetric("Asset-only records", summary.assetOnlyRecords.ToString());
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Placeholders by category", EditorStyles.boldLabel);
            foreach (var pair in PlaceholderAnalytics.CountByType(data).OrderByDescending(p => p.Value))
                EditorGUILayout.LabelField(pair.Key, pair.Value.ToString());

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Placeholders by scene or asset", EditorStyles.boldLabel);
            foreach (var pair in PlaceholderAnalytics.CountByScene(data).OrderByDescending(p => p.Value).Take(12))
                EditorGUILayout.LabelField(pair.Key, pair.Value.ToString(), EditorStyles.miniLabel);

            EditorGUILayout.Space(8);
            blockBuilds = EditorGUILayout.ToggleLeft("Block builds when tagged placeholders remain in enabled build scenes", blockBuilds);
            EditorPrefs.SetBool("PlaceholderGuard/BlockBuilds", blockBuilds);
            EditorGUILayout.HelpBox("Build blocking checks explicit PlaceholderTag components in enabled Build Settings scenes. Heuristic suggestions and asset-only records are advisory and are not automatically treated as build blockers.", MessageType.None);
        }

        private void DrawMetric(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(value, EditorStyles.boldLabel, GUILayout.Width(55));
            }
        }

        private void DrawPlaceholders()
        {
            AssetRegistry.Load();
            var records = AssetRegistry.Data.placeholders
                .Where(p => string.IsNullOrEmpty(searchText) ||
                    Contains(p.displayName, searchText) || Contains(p.sceneOrPrefab, searchText) ||
                    Contains(p.assetType, searchText) || Contains(p.assetId, searchText))
                .OrderBy(p => p.displayName).ToList();

            searchText = EditorGUILayout.TextField("Search", searchText);
            EditorGUILayout.LabelField(records.Count + " matching placeholder record(s)", EditorStyles.miniLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(130));
            foreach (PlaceholderRecord record in records)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(record.displayName, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(record.replacementStatus, EditorStyles.miniBoldLabel);
                    }
                    EditorGUILayout.LabelField(record.assetType + "  |  " + record.targetKind, EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(record.sceneOrPrefab, EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button(selectedAssetId == record.assetId ? "Selected" : "Manage Replacement", GUILayout.Width(170)))
                        SelectRecord(record);
                }
            }
            EditorGUILayout.EndScrollView();

            PlaceholderRecord selected = AssetRegistry.Find(selectedAssetId);
            if (selected == null) return;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Replacement manager", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(selected.displayName + "  |  " + selected.assetId, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Target: " + selected.sceneOrPrefab + (string.IsNullOrEmpty(selected.gameObjectPath) ? "" : "\n" + selected.gameObjectPath), EditorStyles.wordWrappedMiniLabel);

            if (replacementSelection == null && !string.IsNullOrEmpty(selected.replacementAssetPath))
                replacementSelection = AssetDatabase.LoadMainAssetAtPath(selected.replacementAssetPath);
            replacementSelection = EditorGUILayout.ObjectField("Replacement asset", replacementSelection, typeof(UnityEngine.Object), false);
            string replacementPath = replacementSelection == null ? string.Empty : AssetDatabase.GetAssetPath(replacementSelection);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Link Replacement"))
                {
                    string validation;
                    if (!ReplacementService.ValidateReplacement(selected, replacementPath, out validation))
                        SetMessage(validation, MessageType.Warning);
                    else
                    {
                        AssetRegistry.LinkReplacement(selected.assetId, replacementPath);
                        SetMessage("Replacement linked. The original placeholder remains unchanged.", MessageType.Info);
                    }
                }
                if (GUILayout.Button("Verify Link"))
                {
                    string message;
                    bool success = ReplacementService.VerifyLink(selected, out message);
                    SetMessage(message, success ? MessageType.Info : MessageType.Warning);
                }
                if (GUILayout.Button("Unlink"))
                {
                    if (EditorUtility.DisplayDialog("Unlink replacement?", "Remove the saved replacement link? This will not modify either asset.", "Unlink", "Cancel"))
                    {
                        AssetRegistry.UnlinkReplacement(selected.assetId);
                        replacementSelection = null;
                        SetMessage("Replacement link removed. No scene objects were changed.", MessageType.Info);
                    }
                }
            }

            bool canApply = selected.targetKind == "gameObject" && selected.sceneOrPrefab != null && selected.sceneOrPrefab.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = canApply && !string.IsNullOrEmpty(selected.replacementAssetPath);
                if (GUILayout.Button("Apply Replacement to Scene", GUILayout.Height(26)))
                {
                    bool confirm = EditorUtility.DisplayDialog("Apply replacement?",
                        "Replace the tagged GameObject with the selected prefab in the recorded scene?\n\nThe old object's children will be reparented under the replacement. Unity Undo can revert this while the action remains in the Undo history. The scene will be marked dirty but will not be saved automatically.",
                        "Apply Replacement", "Cancel");
                    if (confirm)
                    {
                        string message;
                        bool success = ReplacementService.ApplyToScene(selected, out message);
                        SetMessage(message, success ? MessageType.Info : MessageType.Error);
                    }
                }
                GUI.enabled = true;
                if (GUILayout.Button("Locate", GUILayout.Width(75))) LocateRecord(selected);
            }
            if (!canApply)
                EditorGUILayout.HelpBox("Automatic replacement currently supports tagged GameObjects in scene files. Prefab asset and standalone asset records can be linked and verified, but their content must be changed manually in this version.", MessageType.Info);

            DrawHistory(selected.assetId);
        }

        private void DrawHistory(string assetId)
        {
            var history = AssetRegistry.Data.history.Where(h => h.placeholderId == assetId).Reverse().Take(12).ToList();
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Recent replacement history", EditorStyles.boldLabel);
            if (history.Count == 0) EditorGUILayout.LabelField("No history yet.", EditorStyles.miniLabel);
            historyScroll = EditorGUILayout.BeginScrollView(historyScroll, GUILayout.Height(Mathf.Min(130, 22 + history.Count * 36)));
            foreach (PlaceholderHistoryEntry item in history)
            {
                EditorGUILayout.LabelField(item.action + "  |  " + item.occurredAtUtc, EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(item.details, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSuggestions()
        {
            AssetRegistry.Load();
            var suggestions = AssetRegistry.Data.suggestions.Where(s => s.status == "pending").OrderByDescending(s => s.confidence).ToList();
            EditorGUILayout.LabelField("Review before approving", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Suggestions are heuristic candidates based on names and folder paths. They are not confirmed placeholders. Review each one; approval adds a tag to a GameObject or creates an asset-only registry record.", MessageType.Info);
            EditorGUILayout.LabelField(suggestions.Count + " pending suggestion(s)", EditorStyles.miniLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (PlaceholderSuggestion suggestion in suggestions)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField(suggestion.displayName, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(suggestion.targetKind + "  |  " + suggestion.assetType + "  |  confidence " + Mathf.RoundToInt(suggestion.confidence * 100f) + "%", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(suggestion.targetKind == "asset" ? suggestion.assetPath : suggestion.contextPath + "\n" + suggestion.gameObjectPath, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField(suggestion.reason, EditorStyles.wordWrappedLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Approve as Placeholder"))
                        {
                            bool confirm = EditorUtility.DisplayDialog("Approve suggestion?",
                                "Approve '" + suggestion.displayName + "' as a placeholder?\n\nFor prefab assets, PlaceholderGuard creates a backup before saving the added tag.",
                                "Approve", "Cancel");
                            if (confirm)
                            {
                                string message;
                                bool success = PlaceholderSuggestionService.Approve(suggestion, out message);
                                SetMessage(message, success ? MessageType.Info : MessageType.Warning);
                                AssetRegistry.Load();
                            }
                        }
                        if (GUILayout.Button("Dismiss"))
                        {
                            PlaceholderSuggestionService.Dismiss(suggestion);
                            AssetRegistry.Load();
                        }
                        if (GUILayout.Button("Locate", GUILayout.Width(75))) LocateSuggestion(suggestion);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawReports()
        {
            if (lastReport == null)
            {
                EditorGUILayout.HelpBox("Run Scan Entire Project or Scan Build Scenes to populate a report.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("Latest scan report", EditorStyles.boldLabel);
            DrawMetric("Scope", lastReport.scanScope);
            DrawMetric("Scanned at (UTC)", lastReport.scannedAtUtc);
            DrawMetric("Tagged placeholders", lastReport.placeholderCount.ToString());
            DrawMetric("Scenes scanned", lastReport.scenesScanned.ToString());
            DrawMetric("Prefabs scanned", lastReport.prefabsScanned.ToString());
            DrawMetric("Candidate assets checked", lastReport.assetsChecked.ToString());
            DrawMetric("Pending suggestions", lastReport.suggestionCount.ToString());
            DrawMetric("Missing references", lastReport.missingReferenceCount.ToString());
            DrawMetric("Missing scripts", lastReport.missingScriptCount.ToString());
            DrawMetric("Duplicate IDs", lastReport.duplicateIdCount.ToString());
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Export JSON")) PlaceholderReportExporter.ExportJson(lastReport);
                if (GUILayout.Button("Export CSV")) PlaceholderReportExporter.ExportCsv(lastReport);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Findings", EditorStyles.boldLabel);
            foreach (ScanFinding finding in lastReport.findings)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField(finding.displayName + "  |  " + finding.findingType, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(finding.sceneOrPrefab + "\n" + finding.gameObjectPath, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField(finding.message, EditorStyles.wordWrappedLabel);
                    if (finding.findingType == "tagged" && GUILayout.Button("Locate")) TrySelect(finding);
                }
            }
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Issues", EditorStyles.boldLabel);
            foreach (ScanIssue issue in lastReport.issues)
            {
                EditorGUILayout.HelpBox(issue.category + ": " + issue.message +
                    (string.IsNullOrEmpty(issue.contextPath) ? "" : "\n" + issue.contextPath) +
                    (string.IsNullOrEmpty(issue.objectPath) ? "" : "\n" + issue.objectPath) +
                    (string.IsNullOrEmpty(issue.propertyPath) ? "" : "\nProperty: " + issue.propertyPath),
                    issue.severity == "error" ? MessageType.Error : MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
        }

        private void RunProjectScan()
        {
            try
            {
                lastReport = PlaceholderScanner.ScanProject();
                SetMessage("Project scan complete. Review findings and suggestions before taking action.", MessageType.Info);
            }
            catch (Exception ex) { SetMessage("Project scan failed: " + ex.Message, MessageType.Error); }
            finally { EditorUtility.ClearProgressBar(); AssetRegistry.Load(); }
        }

        private void RunBuildScan()
        {
            try
            {
                lastReport = PlaceholderScanner.ScanBuildScenes();
                SetMessage("Build-scene scan complete.", MessageType.Info);
            }
            catch (Exception ex) { SetMessage("Build-scene scan failed: " + ex.Message, MessageType.Error); }
            finally { EditorUtility.ClearProgressBar(); AssetRegistry.Load(); }
        }

        private void SelectRecord(PlaceholderRecord record)
        {
            selectedAssetId = record.assetId;
            replacementSelection = string.IsNullOrEmpty(record.replacementAssetPath)
                ? null : AssetDatabase.LoadMainAssetAtPath(record.replacementAssetPath);
        }

        private void LocateRecord(PlaceholderRecord record)
        {
            if (record.targetKind == "asset")
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(record.sceneOrPrefab);
                if (asset != null) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
                return;
            }
            if (record.sceneOrPrefab.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                UnityEngine.Object prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(record.sceneOrPrefab);
                if (prefab != null) { Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab); }
                return;
            }
            Scene scene = SceneManager.GetSceneByPath(record.sceneOrPrefab);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                try { scene = EditorSceneManager.OpenScene(record.sceneOrPrefab, OpenSceneMode.Additive); }
                catch (Exception ex) { SetMessage("Could not open scene: " + ex.Message, MessageType.Warning); return; }
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    PlaceholderTag tag = transform.GetComponent<PlaceholderTag>();
                    if (tag != null && tag.AssetId == record.assetId)
                    {
                        Selection.activeGameObject = transform.gameObject;
                        EditorGUIUtility.PingObject(transform.gameObject);
                        return;
                    }
                }
            }
            SetMessage("Could not find the tagged object. Run a fresh scan.", MessageType.Warning);
        }

        private void LocateSuggestion(PlaceholderSuggestion suggestion)
        {
            if (suggestion.targetKind == "asset")
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(suggestion.assetPath);
                if (asset != null) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
                return;
            }
            if (suggestion.contextPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(suggestion.contextPath);
                if (asset != null) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
                return;
            }
            Scene scene = SceneManager.GetSceneByPath(suggestion.contextPath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                try { scene = EditorSceneManager.OpenScene(suggestion.contextPath, OpenSceneMode.Additive); }
                catch (Exception ex) { SetMessage("Could not open scene: " + ex.Message, MessageType.Warning); return; }
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (PlaceholderScanner.GetHierarchyPath(transform) == suggestion.gameObjectPath)
                    {
                        Selection.activeGameObject = transform.gameObject;
                        EditorGUIUtility.PingObject(transform.gameObject);
                        return;
                    }
                }
            }
        }

        private static void TrySelect(ScanFinding finding)
        {
            Scene scene = SceneManager.GetSceneByPath(finding.sceneOrPrefab);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                try { scene = EditorSceneManager.OpenScene(finding.sceneOrPrefab, OpenSceneMode.Additive); }
                catch { return; }
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    PlaceholderTag tag = transform.GetComponent<PlaceholderTag>();
                    if (tag != null && tag.AssetId == finding.assetId)
                    {
                        Selection.activeGameObject = transform.gameObject;
                        EditorGUIUtility.PingObject(transform.gameObject);
                        return;
                    }
                }
            }
        }

        private void SetMessage(string message, MessageType type)
        {
            statusMessage = message;
            statusMessageType = type;
            Repaint();
        }

        private static bool Contains(string source, string value)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(value ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
