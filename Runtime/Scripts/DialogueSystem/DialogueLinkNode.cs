using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Visual representation of a DialogueLink asset, which accepts transitions from room nodes.
public class DialogueLinkNode : Node
{
    public readonly DialogueLink link;
    public readonly Port inputPort;
    public readonly Port outputPort;
    public Port port => inputPort;

    public DialogueLinkNode(DialogueLink link)
    {
        // Retain the asset reference and use its GUID for saved group membership.
        this.link = link;
        title = link.name;
        // Include local file ID so sub-asset link nodes have distinct saved identities.
        viewDataKey = DialogueGraphLayout.GetAssetKey(link);

        // The link receives flow from its source room and outputs flow to its destination room.
        inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(DialogueLink));
        inputPort.portName = "From";
        inputContainer.Add(inputPort);

        outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(DialogueData));
        outputPort.portName = "To";
        outputContainer.Add(outputPort);

        // Display the referenced ScriptableObject without turning the node into an asset picker.
        var objectField = new ObjectField
        {
            objectType = typeof(DialogueLink),
            value = link,
            allowSceneObjects = false
        };

        objectField.style.width = 90;
        objectField.style.height = 15;

        objectField.SetEnabled(false);
        mainContainer.Add(objectField);

        inputPort.style.height = 15;
        outputPort.style.height = 15;

        this.style.width = 110;
        this.style.minHeight = 45;

        RefreshExpandedState();
        RefreshPorts();

        SetPosition(new Rect(DialogueGraphLayout.instance.GetPosition(link), Vector2.zero));
        RefreshWarning();
    }

    public override void SetPosition(Rect newPos)
    {
        // Position data is shared with other graph windows through DialogueGraphLayout.
        base.SetPosition(newPos);
        DialogueGraphLayout.instance.SetPosition(link, newPos.position);
    }

    public void RefreshWarning()
    {
        // Extension point: inspect incoming connections and show validation warnings on this node.
        int count = port.connections.Count();
    }

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        base.BuildContextualMenu(evt);
        // Ping the backing asset in the Project window.
        evt.menu.AppendAction("Select Asset", _ =>
        {
            Selection.activeObject = link;
            EditorGUIUtility.PingObject(link);
        });
    }
}
