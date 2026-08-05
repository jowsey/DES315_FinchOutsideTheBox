using Sirenix.OdinInspector;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[InfoBox("Ensures an item is always selected for gamepad navigation in case of gamepad and mouse being used at the same time")]
public class EnsureSelection : MonoBehaviour
{
    private GameObject _lastSelected;

    private void Update()
    {
        GameObject current = EventSystem.current.currentSelectedGameObject;
        if (current && current.activeInHierarchy)
        {
            _lastSelected = current;
        }
        else if (InputIconManager.LastActiveDevice is Gamepad)
        {
            GameObject newSelected = (_lastSelected && _lastSelected.activeInHierarchy ? _lastSelected : EventSystem.current.firstSelectedGameObject);
            EventSystem.current.SetSelectedGameObject(newSelected);
        }
    }
}