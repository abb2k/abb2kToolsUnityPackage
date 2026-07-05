using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

[System.Serializable]
public class UIReference<E> where E : VisualElement
{
    public UIDocument Document;

    [SerializeField, HideInInspector]
    private string _elementPath;
    [SerializeField, HideInInspector]
    private string _elementName;
    [SerializeField, HideInInspector]
    private string _viewDataKey;
    
#pragma warning disable CS0414
    [SerializeField, HideInInspector]
    private int _siblingIndex = -1;
    [SerializeField, HideInInspector]
    private int _parentChildCount = -1;
#pragma warning restore CS0414

    public E Element
    {
        get
        {
            var root = Document?.rootVisualElement;
            if (root == null) return null;

            if (!string.IsNullOrEmpty(_viewDataKey))
            {
                var el = root.Query<E>().Build().FirstOrDefault(e => e.viewDataKey == _viewDataKey);
                if (el != null) return el;
            }

            var allCandidates = root.Query<E>().Build().ToList();
            if (allCandidates.Count == 0) return null;

            E bestMatch = null;
            int maxScore = -1;

            foreach (var candidate in allCandidates)
            {
                int score = 0;
                string candName = candidate.name ?? "";
                
                if (!string.IsNullOrEmpty(_elementName) && candName == _elementName) 
                    score += 1000;
                
                if (candidate.parent != null)
                {
                    if (candidate.parent.IndexOf(candidate) == _siblingIndex) score += 100;
                    if (candidate.parent.childCount == _parentChildCount) score += 50;
                }

                if (score > maxScore)
                {
                    maxScore = score;
                    bestMatch = candidate;
                }
            }

            return bestMatch;
        }
    }

    public static implicit operator E(UIReference<E> reference) => reference.Element;
}