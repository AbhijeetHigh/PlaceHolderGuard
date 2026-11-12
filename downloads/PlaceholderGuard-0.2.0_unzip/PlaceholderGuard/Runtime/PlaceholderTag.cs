using UnityEngine;

namespace PlaceholderGuard
{
    /// <summary>
    /// Attach to a GameObject that uses a temporary/proxy asset.
    /// The tag is intentionally lightweight and safe to keep in player builds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlaceholderTag : MonoBehaviour
    {
        [SerializeField, HideInInspector] private string assetId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string reason;
        [SerializeField] private PlaceholderAssetType assetType = PlaceholderAssetType.Other;
        [SerializeField] private bool exemptFromBuildBlock;
        [SerializeField, TextArea(1, 3)] private string exemptionReason;

        public string AssetId => assetId;
        public string DisplayName => displayName;
        public string Reason => reason;
        public PlaceholderAssetType AssetType => assetType;
        public bool ExemptFromBuildBlock => exemptFromBuildBlock;
        public string ExemptionReason => exemptionReason;

        public void Configure(string id, string name, PlaceholderAssetType type, string placeholderReason)
        {
            assetId = id;
            displayName = name;
            assetType = type;
            reason = placeholderReason;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Generate once in the editor so the ID is serialized into the scene/prefab.
            if (string.IsNullOrWhiteSpace(assetId))
                assetId = System.Guid.NewGuid().ToString("D");

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = gameObject.name;
        }
#endif
    }

    public enum PlaceholderAssetType
    {
        Other = 0,
        Audio = 1,
        Texture = 2,
        Model = 3,
        Material = 4,
        Dialogue = 5,
        Animation = 6,
        Script = 7
    }
}
