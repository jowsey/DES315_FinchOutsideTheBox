using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

public class Highlight : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    //Mapping from tag to whether or not that tag can be highlighted
    private static readonly Dictionary<string, bool> _isTagHighlightable = new();

    private static readonly Dictionary<string, List<Highlight>> _lookupByTag = new();

    private bool _highlighted;
    private Renderer[] _renderers;

    [SerializeField] [Required] private Color _highlightedColour;

    private void Awake()
    {
        _highlighted = false;
        _renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnEnable()
    {
        // Init lookup maps
        _isTagHighlightable.TryAdd(tag, true);
        if (!_lookupByTag.ContainsKey(tag))
        {
            _lookupByTag.Add(tag, new List<Highlight>());
        }

        _lookupByTag[tag].Add(this);
    }

    private void OnDisable()
    {
        _lookupByTag[tag].Remove(this);
    }

    public static void SetHighlightable(string tag, bool val)
    {
        _isTagHighlightable[tag] = val;

        if (_lookupByTag.TryGetValue(tag, out var highlights))
        {
            foreach (Highlight highlight in highlights)
            {
                if (!_isTagHighlightable[tag] && highlight._highlighted)
                {
                    //Object is highlighted but not highlightable, unhighlight it
                    highlight.Unhighlight();
                }
            }
        }
    }

    private void Unhighlight()
    {
        foreach (var rend in _renderers)
        {
            rend.SetPropertyBlock(null);
        }

        _highlighted = false;
    }

    private void Update()
    {
        bool beingLookedAt = CrosshairDetection.TargetedTransform && CrosshairDetection.TargetedTransform.IsChildOf(transform);

        if (beingLookedAt && !_highlighted && _isTagHighlightable[tag])
        {
            //Object is being looked at and is highlightable, but is not highlighted, highlight it
            foreach (Renderer rend in _renderers)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(BaseColor, _highlightedColour);
                rend.SetPropertyBlock(mpb);
            }

            _highlighted = true;
        }
        else if (!beingLookedAt && _highlighted)
        {
            //Object is highlighted but not highlightable, unhighlight it
            Unhighlight();
        }
    }
}