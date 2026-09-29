using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


// Stores editor-only graph presentation data outside the runtime Dialogue assets.
[FilePath("UserSettings/DialogueGraphLayout.asset", FilePathAttribute.Location.ProjectFolder)]
public class DialogueGraphLayout : ScriptableSingleton<DialogueGraphLayout>
{
    // Asset positions are keyed by GUID so renaming or moving an asset does not lose its layout.
    [System.Serializable]
    public class Entry
    {
        public string guid;
        public Vector2 position;
    }

    // Groups belong to a graph asset and keep members by their asset GUIDs.
    [System.Serializable]
    public class GroupData
    {
        public string groupId;
        public string graphGuid;
        public string title;
        public Vector2 position;
        public Vector2 size;
        public List<string> memberAssetGuids = new();
    }

    [SerializeField] private List<Entry> entries = new();
    [SerializeField] private List<GroupData> groups = new();

    // Return the saved position, or origin for assets that have not been laid out yet.
    public Vector2 GetPosition(Object asset)
    {
        var entry = FindEntry(asset);
        return entry != null ? entry.position : Vector2.zero;
    }

    public void SetPosition(Object asset, Vector2 position)
    {
        // ScriptableSingleton.Save writes this editor state to the path declared above.
        var entry = FindEntry(asset);
        if (entry != null && entry.position == position) return;

        Undo.RecordObject(this, "Move Dialogue Graph Element");
        entry ??= GetOrCreateEntry(asset);
        entry.position = position;
        MarkDirty();
    }

    private Entry FindEntry(Object asset)
    {
        // A GUID identifies the file, so include local ID to distinguish its sub-assets.
        var guid = GetAssetKey(asset);
        return entries.Find(e => e.guid == guid);
    }

    private Entry GetOrCreateEntry(Object asset)
    {
        // Create a default position record the first time a node is moved.
        var guid = GetAssetKey(asset);
        var entry = entries.Find(e => e.guid == guid);
        if (entry == null)
        {
            entry = new Entry { guid = guid };
            entries.Add(entry);
        }
        return entry;
    }

    public List<GroupData> GetGroups(Object graphAsset)
    {
        // Filter so each Dialogue graph restores only its own groups.
        var graphGuid = GetGuid(graphAsset);
        return groups.FindAll(g => g.graphGuid == graphGuid);
    }

    public GroupData CreateGroupData(Object graphAsset, string title, Vector2 position, Vector2 size)
    {
        // A generated ID distinguishes groups even when titles are identical.
        Undo.RecordObject(this, "Create Dialogue Graph Group");
        var data = new GroupData
        {
            groupId = System.Guid.NewGuid().ToString(),
            graphGuid = GetGuid(graphAsset),
            title = title,
            position = position,
            size = size
        };
        groups.Add(data);
        EditorUtility.SetDirty(this);
        Save(true);
        return data;
    }

    public void RemoveGroup(GroupData data)
    {
        // Removing the visual group also removes its saved representation.
        if (!groups.Contains(data)) return;

        Undo.RecordObject(this, "Remove Dialogue Graph Group");
        groups.Remove(data);
        EditorUtility.SetDirty(this);
        Save(true);
    }

    public void SaveGroups()
    {
        MarkDirty();
        Save(true);
    }

    public void MarkDirty() => EditorUtility.SetDirty(this);

    // Extension point: other editor-only layout settings can be persisted alongside positions/groups.
    public static string GetAssetKey(Object asset)
    {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId))
            return string.Empty;

        return $"{guid}:{localId}";
    }

    private static string GetGuid(Object asset)
    {
        return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
    }
}