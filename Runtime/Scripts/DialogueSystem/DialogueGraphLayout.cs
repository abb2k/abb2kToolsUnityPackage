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
        public Vector2 size;
        public bool sizeWasUserSpecified;
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

    [System.Serializable]
    public class ViewData
    {
        public string graphGuid;
        public Vector3 position;
        public Vector3 scale = Vector3.one;
    }

    [System.Serializable]
    public class VariableNodeEntry
    {
        public string graphKey;
        public string nodeId;
        public string variableId;
        public Vector2 position;
    }

    [SerializeField] private List<Entry> entries = new();
    [SerializeField] private List<GroupData> groups = new();
    [SerializeField] private List<ViewData> views = new();
    [SerializeField] private List<VariableNodeEntry> variableNodes = new();

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

    public Vector2 GetSize(Object asset)
    {
        var entry = FindEntry(asset);
        return entry != null && entry.size.x > 0 && entry.size.y > 0
            ? entry.size
            : new Vector2(380, 260);
    }

    public bool TryGetUserSpecifiedSize(Object asset, out Vector2 size)
    {
        var entry = FindEntry(asset);
        if (entry != null && entry.sizeWasUserSpecified && entry.size.x > 0 && entry.size.y > 0)
        {
            size = entry.size;
            return true;
        }

        size = default;
        return false;
    }

    public bool TryGetSavedSize(Object asset, out Vector2 size)
    {
        var entry = FindEntry(asset);
        if (entry != null && entry.size.x > 0 && entry.size.y > 0)
        {
            size = entry.size;
            return true;
        }

        size = default;
        return false;
    }

    public void SetSize(Object asset, Vector2 size)
    {
        var entry = FindEntry(asset);
        if (entry != null && entry.size == size && entry.sizeWasUserSpecified) return;

        Undo.RecordObject(this, "Resize Dialogue Graph Node");
        entry ??= GetOrCreateEntry(asset);
        entry.size = size;
        entry.sizeWasUserSpecified = true;
        MarkDirty();
        Save(true);
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

    public bool TryGetView(Object graphAsset, out Vector3 position, out Vector3 scale)
    {
        var graphGuid = GetGuid(graphAsset);
        var view = views.Find(item => item.graphGuid == graphGuid);
        if (view == null)
        {
            position = Vector3.zero;
            scale = Vector3.one;
            return false;
        }

        position = view.position;
        scale = view.scale;
        return true;
    }

    public void SetView(Object graphAsset, Vector3 position, Vector3 scale)
    {
        var graphGuid = GetGuid(graphAsset);
        var view = views.Find(item => item.graphGuid == graphGuid);
        if (view != null && view.position == position && view.scale == scale) return;

        view ??= new ViewData { graphGuid = graphGuid };
        if (!views.Contains(view))
            views.Add(view);
        view.position = position;
        view.scale = scale;
        MarkDirty();
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

    public List<VariableNodeEntry> GetVariableNodes(Object graphAsset)
    {
        var graphKey = GetAssetKey(graphAsset);
        bool updatedIds = false;
        foreach (var node in variableNodes)
        {
            if (node.graphKey != graphKey || !string.IsNullOrEmpty(node.nodeId)) continue;
            node.nodeId = System.Guid.NewGuid().ToString("N");
            updatedIds = true;
        }

        if (updatedIds)
        {
            MarkDirty();
            Save(true);
        }

        return variableNodes.FindAll(node => node.graphKey == graphKey);
    }

    public string AddVariableNode(Object graphAsset, string variableId, Vector2 position)
    {
        var nodeId = System.Guid.NewGuid().ToString("N");
        Undo.RecordObject(this, "Create Exposed Variable Node");
        variableNodes.Add(new VariableNodeEntry
        {
            graphKey = GetAssetKey(graphAsset),
            nodeId = nodeId,
            variableId = variableId,
            position = position
        });
        MarkDirty();
        Save(true);
        return nodeId;
    }

    public void SetVariableNodePosition(Object graphAsset, string nodeId, Vector2 position)
    {
        var node = FindVariableNode(graphAsset, nodeId);
        if (node == null || node.position == position) return;
        Undo.RecordObject(this, "Move Exposed Variable Node");
        node.position = position;
        MarkDirty();
    }

    public void RemoveVariableNode(Object graphAsset, string nodeId)
    {
        var node = FindVariableNode(graphAsset, nodeId);
        if (node == null) return;
        Undo.RecordObject(this, "Delete Exposed Variable Node");
        variableNodes.Remove(node);
        MarkDirty();
        Save(true);
    }

    public static string GetVariableNodeKey(Object graphAsset, string nodeId) =>
        $"variable:{GetAssetKey(graphAsset)}:{nodeId}";

    private VariableNodeEntry FindVariableNode(Object graphAsset, string nodeId)
    {
        var graphKey = GetAssetKey(graphAsset);
        return variableNodes.Find(node => node.graphKey == graphKey && node.nodeId == nodeId);
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