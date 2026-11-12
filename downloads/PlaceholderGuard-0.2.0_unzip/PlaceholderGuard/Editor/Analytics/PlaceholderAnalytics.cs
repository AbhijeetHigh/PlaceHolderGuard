using System;
using System.Collections.Generic;
using System.Linq;

namespace PlaceholderGuard.Editor
{
    [Serializable]
    public sealed class PlaceholderAnalyticsSummary
    {
        public int totalTracked;
        public int unlinked;
        public int linked;
        public int verified;
        public int applied;
        public int exempt;
        public int pendingSuggestions;
        public int assetOnlyRecords;
        public int readinessScore;
    }

    public static class PlaceholderAnalytics
    {
        public static PlaceholderAnalyticsSummary BuildSummary(PlaceholderRegistryData data)
        {
            if (data == null) data = new PlaceholderRegistryData();
            if (data.placeholders == null) data.placeholders = new List<PlaceholderRecord>();
            if (data.suggestions == null) data.suggestions = new List<PlaceholderSuggestion>();

            var summary = new PlaceholderAnalyticsSummary();
            summary.totalTracked = data.placeholders.Count;
            summary.unlinked = data.placeholders.Count(p => p.replacementStatus == "unlinked" || string.IsNullOrEmpty(p.replacementStatus));
            summary.linked = data.placeholders.Count(p => p.replacementStatus == "linked");
            summary.verified = data.placeholders.Count(p => p.replacementStatus == "verified");
            summary.applied = data.placeholders.Count(p => p.replacementStatus == "applied");
            summary.exempt = data.placeholders.Count(p => p.exemptFromBuildBlock && !string.IsNullOrWhiteSpace(p.exemptionReason));
            summary.pendingSuggestions = data.suggestions.Count(s => s.status == "pending");
            summary.assetOnlyRecords = data.placeholders.Count(p => p.targetKind == "asset");

            int resolved = data.placeholders.Count(p => p.replacementStatus == "applied" ||
                (p.exemptFromBuildBlock && !string.IsNullOrWhiteSpace(p.exemptionReason)));
            summary.readinessScore = summary.totalTracked == 0 ? 100 : (int)Math.Round(100.0 * resolved / summary.totalTracked);
            return summary;
        }

        public static Dictionary<string, int> CountByType(PlaceholderRegistryData data)
        {
            return data.placeholders.GroupBy(p => string.IsNullOrEmpty(p.assetType) ? "Other" : p.assetType)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public static Dictionary<string, int> CountByScene(PlaceholderRegistryData data)
        {
            return data.placeholders.GroupBy(p => string.IsNullOrEmpty(p.sceneOrPrefab) ? "(unknown)" : p.sceneOrPrefab)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }
}
