using System.IO;
using System.Text;
using UnityEditor;

namespace PlaceholderGuard.Editor
{
    public static class PlaceholderReportExporter
    {
        public static void ExportJson(ScanReport report)
        {
            string path = EditorUtility.SaveFilePanel("Export PlaceholderGuard scan", "", "placeholderguard-report.json", "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, UnityEngine.JsonUtility.ToJson(report ?? new ScanReport(), true));
            EditorUtility.DisplayDialog("PlaceholderGuard", "JSON scan report exported to:\n" + path, "OK");
        }

        public static void ExportCsv(ScanReport report)
        {
            string path = EditorUtility.SaveFilePanel("Export PlaceholderGuard findings", "", "placeholderguard-findings.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var builder = new StringBuilder();
            builder.AppendLine("FindingType,AssetId,DisplayName,Context,GameObjectPath,AssetType,ReplacementStatus,Exempt,ExemptionReason,Severity,Category,PropertyPath,Message");
            if (report != null)
            {
                foreach (ScanFinding finding in report.findings)
                {
                    builder.AppendLine(string.Join(",", new[]
                    {
                        Csv(finding.findingType), Csv(finding.assetId), Csv(finding.displayName), Csv(finding.sceneOrPrefab),
                        Csv(finding.gameObjectPath), Csv(finding.assetType), Csv(finding.replacementStatus),
                        Csv(finding.exemptFromBuildBlock.ToString()), Csv(finding.exemptionReason), "", "", "", Csv(finding.message)
                    }));
                }
                foreach (ScanIssue issue in report.issues)
                {
                    builder.AppendLine(string.Join(",", new[]
                    {
                        "issue", "", "", Csv(issue.contextPath), Csv(issue.objectPath), "", "", "", "",
                        Csv(issue.severity), Csv(issue.category), Csv(issue.propertyPath), Csv(issue.message)
                    }));
                }
                if (report.suggestions != null)
                {
                    foreach (PlaceholderSuggestion suggestion in report.suggestions)
                    {
                        builder.AppendLine(string.Join(",", new[]
                        {
                            "suggestion", Csv(suggestion.suggestionId), Csv(suggestion.displayName),
                            Csv(suggestion.targetKind == "asset" ? suggestion.assetPath : suggestion.contextPath),
                            Csv(suggestion.gameObjectPath), Csv(suggestion.assetType), Csv(suggestion.status), "", "",
                            Csv("confidence " + suggestion.confidence.ToString("0.00")), Csv(suggestion.targetKind), "", Csv(suggestion.reason)
                        }));
                    }
                }
            }
            File.WriteAllText(path, builder.ToString());
            EditorUtility.DisplayDialog("PlaceholderGuard", "CSV report exported to:\n" + path, "OK");
        }

        private static string Csv(string value)
        {
            string text = value ?? string.Empty;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
