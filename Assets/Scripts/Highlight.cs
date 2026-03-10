using Sirenix.OdinInspector;
using System.Linq;
using UnityEngine;

public class Highlight: MonoBehaviour
{
    private bool _highlighted;
    private MeshRenderer[] _renderers;
    [SerializeField][Required] private Color _highlightedColour;
    private Color _unhighlightedColour;

    private void Awake()
    {
        _highlighted = false;
        _renderers = GetComponentsInChildren<MeshRenderer>();
        _unhighlightedColour = _renderers[0].material.GetColor("_BaseColor");
    }

	private void Update()
	{
        bool beingLookedAt = (CrosshairDetection._hitTransform == transform) || GetComponentsInChildren<Transform>().Contains(CrosshairDetection._hitTransform);

        //Supress highlight based on context (todo: this is really yucky, find a better way of handling highlight logic in general)
        if (beingLookedAt)
        {
            if (CompareTag("Flask") && PlayerController.HeldFlask != null)
            {
                beingLookedAt = false;
            }
            else if (CompareTag("FlaskCarrier") && PlayerController.HeldFlask == null)
            {
                beingLookedAt = false;
            }
        }

        if (beingLookedAt && !_highlighted)
        {
            //Object is being looked at but is not highlighted, highlight it
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