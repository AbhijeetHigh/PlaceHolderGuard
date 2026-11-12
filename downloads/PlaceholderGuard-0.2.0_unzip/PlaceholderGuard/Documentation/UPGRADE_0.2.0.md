# Upgrade from PlaceholderGuard 0.1.0 to 0.2.0

1. Save the Unity project and close Unity.
2. Make a copy of `ProjectSettings/PlaceholderGuard/registry.json` somewhere outside the project as an extra precaution.
3. Open the Unity project folder in Windows File Explorer.
4. Open `Packages` and remove the old `PlaceholderGuard` folder.
5. Extract the new ZIP and copy its inner `PlaceholderGuard` folder into `Packages`.
6. Reopen Unity and wait for package compilation.
7. Open **Tools → PlaceholderGuard → Open Dashboard** and run **Scan Entire Project**.

The package reads the existing v1 registry fields and upgrades the data structure to schema version 2 on the next save. Before overwriting a v1 registry, it attempts to create a timestamped `registry.json.v1-backup-....json` copy beside the registry. Keep your manual backup until you have checked the updated dashboard.

The replacement status values are `unlinked`, `linked`, `verified`, and `applied`. A linked/verified record is not necessarily applied to the scene. Existing v1 links are preserved as far as the old registry data allows.

Do not keep both the old and new package folders in `Packages`; that can cause duplicate assembly definitions and confusing menu/compile errors.
