using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Highlight: MonoBehaviour
{
    private bool _highlighted;
    private MeshRenderer[] _renderers;
    [SerializeField][Required] private Color _highlightedColour;
    private Color _unhighlightedColour;
    private static Dictionary<string, bool> _highlightable = new(); //Mapping from tag to whether or not that tag can be highlighted

    private void Awake()
    {
        _highlighted = false;
        _renderers = GetComponentsInChildren<MeshRenderer>();
        _unhighlightedColour = _renderers[0].material.GetColor("_BaseColor");
    }

    private void Start()
    {
        if (!_highlightable.ContainsKey(tag)) { _highlightable.Add(tag, true); }
    }

    static public bool GetHighlightable(string tag) => _highlightable[tag];
    static public void SetHighlightable(string tag, bool val)
    {
        _highlightable[tag] = val;
        foreach (GameObject o in GameObject.FindGameObjectsWithTag(tag))
        {
            if (o.TryGetComponent<Highlight>(out Highlight highlight))
            {                
                //No mismatch
                if (_highlightable[tag] == highlight._highlighted) { return; }

                //Mismatch between _highlighted and _highlightable
                //If object is highlightable but not highlighted, no issues
                if (_highlightable[tag] && !highlight._highlighted) { return; }

                //Object is highlighted but not highlightable, unhighlight it
                foreach (MeshRenderer renderer in highlight._renderers)
                {
                    renderer.material.SetColor("_BaseColor", highlight._unhighlightedColour);
                }
                highlight._highlighted = false;
            }
        }
    }

	private void Update()
	{
        bool beingLookedAt = (CrosshairDetection._hitTransform == transform) || GetComponentsInChildren<Transform>().Contains(CrosshairDetection._hitTransform);

        if (beingLookedAt && !_highlighted && _highlightable[tag])
        {
            //Object is being looked at and is highlightable, but is not highlighted, highlight it
            foreach (MeshRenderer renderer in _renderers)
            {
                renderer.material.SetColor("_BaseColor", _highlightedColour);
            }
            _highlighted = true;
        }
        else if (!beingLookedAt && _highlighted)
        {
            //Object isn't being looked at but is highlighted, unhighlight it
            foreach (MeshRenderer renderer in _renderers)
            {
                renderer.material.SetColor("_BaseColor", _unhighlightedColour);
            }
            _highlighted = false;
        }
    }
}