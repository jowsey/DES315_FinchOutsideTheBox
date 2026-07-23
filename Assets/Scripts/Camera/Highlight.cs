using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class Highlight : MonoBehaviour
{
    private static readonly int BaseColour = Shader.PropertyToID("_BaseColour");
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly Color HighlightedColour = new(1.1f, 1.1f, 1.35f, 1f);

    //Mapping from tag to whether or not that tag can be highlighted
    private static readonly Dictionary<string, bool> _isTagHighlightable = new();

    private static readonly Dictionary<string, List<Highlight>> _lookupByTag = new();

    private bool _highlighted;
    private Renderer[] _renderers;

    private static MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _highlighted = false;
        _renderers = GetComponentsInChildren<Renderer>();
        
        _mpb ??= new MaterialPropertyBlock();
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
        bool beingLookedAt = InteractDetection.TargetedTransform && InteractDetection.TargetedTransform.IsChildOf(transform);

        if (beingLookedAt && !_highlighted && _isTagHighlightable[tag])
        {
            //Object is being looked at and is highlightable, but is not highlighted, highlight it
            foreach (Renderer rend in _renderers)
            {
                //is it worth it to maintain british loyalty? yes
                if (rend.sharedMaterial.HasProperty(BaseColour))
                {
                    var baseColour = rend.sharedMaterial.GetColor(BaseColour);
                    _mpb.SetColor(BaseColour, baseColour * HighlightedColour);
                }
                else if (rend.sharedMaterial.HasProperty(BaseColor))
                {
                    var baseColor = rend.sharedMaterial.GetColor(BaseColor);
                    _mpb.SetColor(BaseColor, baseColor * HighlightedColour);
                }

                rend.SetPropertyBlock(_mpb);
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