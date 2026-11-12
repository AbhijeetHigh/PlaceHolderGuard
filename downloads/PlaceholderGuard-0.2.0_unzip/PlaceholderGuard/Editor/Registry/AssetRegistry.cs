using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PlaceholderGuard.Editor
{
    public static class AssetRegistry
    {
        public const string RegistryDirectory = "ProjectSettings/PlaceholderGuard";
        public const string RegistryPath = RegistryDirectory + "/registry.json";
        private static PlaceholderRegistryData cached;
        private static bool needsMigrationBackup;

        public static PlaceholderRegistryData Data
        {
            get
            {
                if (cached == null) Load();
                return cached;
            }
        }

        public static void Load()
        {
            if (!File.Exists(RegistryPath))
            {
                needsMigrationBackup = false;
                cached = new PlaceholderRegistryData();
                EnsureCollections();
                return;
            }

            try
            {
                cached = JsonUtility.FromJson<PlaceholderRegistryData>(File.ReadAllText(RegistryPath))
                         ?? new PlaceholderRegistryData();
                int loadedSchemaVersion = cached.schemaVersion;
                needsMigrationBackup = loadedSchemaVersion < 2;
                EnsureCollections();
                cached.schemaVersion = 2; // Existing v1 JSON is upgraded in memory; fields default safely.
            }
            catch (Exception ex)
            {
                string backupPath = RegistryPath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".bak";
                try { File.Copy(RegistryPath, backupPath, false); }
                catch (Exception backupException)
                {
                    Debug.LogWarning("[PlaceholderGuard] Could not back up the malformed registry: " + backupException.Message);
                }
                Debug.LogError("[PlaceholderGuard] Could not read registry. A backup was attempted at " + backupPath + ". " + ex.Message);
                needsMigrationBackup = false;
                cached = new PlaceholderRegistryData();
                EnsureCollections();
            }
        }

        private static void EnsureCollections()
        {
            if (cached == null) cached = new PlaceholderRegistryData();
            if (cached.placeholders == null) cached.placeholders = new System.Collections.Generic.List<PlaceholderRecord>();
            if (cached.suggestions == null) cached.suggestions = new System.Collections.Generic.List<PlaceholderSuggestion>();
            if (cached.history == null) cached.history = new System.Collections.Generic.List<PlaceholderHistoryEntry>();
            foreach (var record in cached.placeholders)
            {
                if (string.IsNullOrEmpty(record.targetKind)) record.targetKind = "gameObject";
                if (string.IsNullOrEmpty(record.replacementStatus)) record.replacementStatus = "unlinked";
            }
        }

        public static void Save()
        {
            EnsureCollections();
            cached.schemaVersion = 2;
            Directory.CreateDirectory(RegistryDirectory);
            if (needsMigrationBackup && File.Exists(RegistryPath))
            {
                string backupPath = RegistryPath + ".v1-backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json";
                File.Copy(RegistryPath, backupPath, false);
                needsMigrationBackup = false;
            }
            string tempPath = RegistryPath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(Data, true));
            if (File.Exists(RegistryPath))
            {
                try { File.Replace(tempPath, RegistryPath, null); }
                catch
                {
                    File.Delete(RegistryPath);
                    File.Move(tempPath, RegistryPath);
                }
            }
            else File.Move(tempPath, RegistryPath);
        }

        public static PlaceholderRecord Register(PlaceholderTag tag, string contextPath, string objectPath, bool saveImmediately = true)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            string id = string.IsNullOrWhiteSpace(tag.AssetId) ? Guid.NewGuid().ToString("D") : tag.AssetId;
            tag.Configure(id, tag.DisplayName, tag.AssetType, tag.Reason);
            EditorUtility.SetDirty(tag);

            var record = Data.placeholders.FirstOrDefault(p => p.assetId == id);
            if (record == null)
            {
                record = new PlaceholderRecord
                {
                    assetId = id,
                    createdAtUtc = DateTime.UtcNow.ToString("o"),
                    replacementStatus = "unlinked"
                };
                Data.placeholders.Add(record);
            }

            record.displayName = string.IsNullOrWhiteSpace(tag.DisplayName) ? tag.gameObject.name : tag.DisplayName;
            record.assetType = tag.AssetType.ToString();
            record.targetKind = "gameObject";
            // A prefab instance and its source prefab can share a serialized tag ID.
            // Keep the first discovered context (project scans visit scenes before prefab assets)
            // rather than silently redirecting a scene replacement action to the prefab asset.
            bool existingIsScene = !string.IsNullOrEmpty(record.sceneOrPrefab) &&
                record.sceneOrPrefab.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
            bool incomingIsScene = !string.IsNullOrEmpty(contextPath) &&
                contextPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
            bool sameRecordedContext = string.Equals(record.sceneOrPrefab, contextPath, StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(record.sceneOrPrefab) || sameRecordedContext || (!existingIsScene && incomingIsScene))
            {
                record.sceneOrPrefab = contextPath;
                record.gameObjectPath = objectPath;
            }
            if (sameRecordedContext && record.replacementStatus == "applied")
            {
                record.replacementStatus = string.IsNullOrEmpty(record.replacementAssetPath) ? "unlinked" : "linked";
                AddHistory(id, "tag-resurfaced", record.replacementAssetPath, record.replacementAssetPath,
                    contextPath, "A fresh scan found the original PlaceholderTag again. The previous scene replacement may have been undone or the scene may not have been saved.");
            }
            record.reason = tag.Reason;
            record.exemptFromBuildBlock = tag.ExemptFromBuildBlock;
            record.exemptionReason = tag.ExemptionReason;
            record.updatedAtUtc = DateTime.UtcNow.ToString("o");
            if (saveImmediately) Save();
            return record;
        }

        public static PlaceholderRecord RegisterAsset(string assetPath, string displayName, string assetType, string reason)
        {
            string id = "asset:" + AssetDatabase.AssetPathToGUID(assetPath);
            var record = Data.placeholders.FirstOrDefault(p => p.assetId == id);
            if (record == null)
            {
                record = new PlaceholderRecord
                {
                    assetId = id,
                    createdAtUtc = DateTime.UtcNow.ToString("o"),
                    replacementStatus = "unlinked"
                };
                Data.placeholders.Add(record);
            }
            record.displayName = displayName;
            record.assetType = assetType;
            record.targetKind = "asset";
            record.sceneOrPrefab = assetPath;
            record.gameObjectPath = string.Empty;
            record.reason = reason;
            record.updatedAtUtc = DateTime.UtcNow.ToString("o");
            AddHistory(id, "approved-asset-suggestion", null, null, assetPath,
                "An asset-only placeholder record was created. Build-scene enforcement currently applies to tagged GameObjects.");
            Save();
            return record;
        }

        public static void LinkReplacement(string assetId, string replacementAssetPath)
        {
            var record = Find(assetId);
            if (record == null) throw new InvalidOperationException("Placeholder ID not found: " + assetId);
            if (string.IsNullOrWhiteSpace(replacementAssetPath))
                throw new ArgumentException("Choose a replacement asset before linking.");
            if (!replacementAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Replacement must be inside the Unity Assets folder.");
            if (AssetDatabase.LoadMainAssetAtPath(replacementAssetPath) == null)
                throw new FileNotFoundException("The selected replacement asset could not be found or loaded.", replacementAssetPath);

            string previous = record.replacementAssetPath;
            record.replacementAssetPath = replacementAssetPath;
            record.replacementStatus = "linked";
            record.updatedAtUtc = DateTime.UtcNow.ToString("o");
            AddHistory(assetId, "linked", previous, replacementAssetPath, record.sceneOrPrefab,
                "Replacement asset path saved. The placeholder has not been replaced.");
            Save();
        }

        public static void UnlinkReplacement(string assetId)
        {
            var record = Find(assetId);
            if (record == null) throw new InvalidOperationException("Placeholder ID not found: " + assetId);
            string previous = record.replacementAssetPath;
            record.replacementAssetPath = string.Empty;
            record.replacementStatus = "unlinked";
            record.updatedAtUtc = DateTime.UtcNow.ToString("o");
            AddHistory(assetId, "unlinked", previous, null, record.sceneOrPrefab, "Replacement link removed.");
            Save();
        }

        public static void SetReplacementStatus(string assetId, string status, string details)
        {
            var record = Find(assetId);
            if (record == null) throw new InvalidOperationException("Placeholder ID not found: " + assetId);
            string previous = record.replacementStatus;
            record.replacementStatus = status;
            record.updatedAtUtc = DateTime.UtcNow.ToString("o");
            AddHistory(assetId, "status:" + status, record.replacementAssetPath, record.replacementAssetPath,
                record.sceneOrPrefab, (details ?? "") + " Previous status: " + previous);
            Save();
        }

        public static void AddHistory(string assetId, string action, string previousPath, string newPath,
            string contextPath, string details)
        {
            Data.history.Add(new PlaceholderHistoryEntry
            {
                occurredAtUtc = DateTime.UtcNow.ToString("o"),
                placeholderId = assetId,
                action = action,
                previousAssetPath = previousPath,
                newAssetPath = newPath,
                contextPath = contextPath,
                details = details
            });
        }

        public static void UpsertSuggestion(PlaceholderSuggestion incoming)
        {
            var existing = Data.suggestions.FirstOrDefault(s => s.suggestionKey == incoming.suggestionKey);
            if (existing == null)
            {
                incoming.firstSeenUtc = DateTime.UtcNow.ToString("o");
                incoming.status = "pending";
                Data.suggestions.Add(incoming);
            }
            else
            {
                existing.displayName = incoming.displayName;
                existing.assetType = incoming.assetType;
                existing.confidence = incoming.confidence;
                existing.reason = incoming.reason;
                existing.contextPath = incoming.contextPath;
                existing.gameObjectPath = incoming.gameObjectPath;
                existing.assetPath = incoming.assetPath;
                existing.targetKind = incoming.targetKind;
                // Preserve approved/dismissed decisions across future scans.
            }
            existingOrIncoming(incoming).lastSeenUtc = DateTime.UtcNow.ToString("o");
        }

        private static PlaceholderSuggestion existingOrIncoming(PlaceholderSuggestion incoming)
        {
            return Data.suggestions.FirstOrDefault(s => s.suggestionKey == incoming.suggestionKey) ?? incoming;
        }

        public static void SetSuggestionStatus(string suggestionId, string status)
        {
            var suggestion = Data.suggestions.FirstOrDefault(s => s.suggestionId == suggestionId);
            if (suggestion == null) throw new InvalidOperationException("Suggestion not found: " + suggestionId);
            suggestion.status = status;
            Save();
        }

        public static PlaceholderRecord Find(string assetId)
        {
            return Data.placeholders.FirstOrDefault(p => p.assetId == assetId);
        }
    }
}
