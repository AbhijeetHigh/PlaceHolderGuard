using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PlaceholderGuard.Editor
{
    /// <summary>
    /// Blocks builds when non-exempt PlaceholderTag components remain in enabled build scenes.
    /// Set PlaceholderGuard/BlockBuilds to false in EditorPrefs to switch to warning-only mode.
    /// </summary>
    public sealed class PlaceholderBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        private const string BlockPreference = "PlaceholderGuard/BlockBuilds";

        public void OnPreprocessBuild(BuildReport report)
        {
            bool shouldBlock = EditorPrefs.GetBool(BlockPreference, true);
            var scan = PlaceholderScanner.ScanBuildScenes();
            // An exemption only counts when the developer documents why this placeholder is allowed.
            var blocking = scan.findings.Where(f => !f.exemptFromBuildBlock ||
                string.IsNullOrWhiteSpace(f.exemptionReason)).ToList();

            if (blocking.Count == 0)
            {
                Debug.Log("[PlaceholderGuard] Build validation passed. No blocking placeholders found in enabled build scenes; any exemptions had documented reasons.");
                return;
            }

            string summary = string.Join("\n", blocking.Select(f =>
                "- " + f.displayName + " | " + f.sceneOrPrefab + " | " + f.gameObjectPath +
                (f.exemptFromBuildBlock && string.IsNullOrWhiteSpace(f.exemptionReason) ? " | exemption reason required" : "")));

            string message = "[PlaceholderGuard] Found " + blocking.Count +
                             " unresolved placeholder reference(s):\n" + summary;

            if (shouldBlock)
                throw new BuildFailedException(message);

            Debug.LogWarning(message);
        }
    }
}
