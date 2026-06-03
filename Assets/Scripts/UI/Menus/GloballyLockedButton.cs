using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UI
{
    [InfoBox("Denotes a button that should globally lock and unlock based on game state")]
    [RequireComponent(typeof(Button))]
    public class GloballyLockedButton : MonoBehaviour
    {
        private static readonly HashSet<Object> _lockSources = new();

        public static bool Locked => _lockSources.Count > 0;

        public static void AddLockSource(Object source)
        {
            _lockSources.Add(source);
            if (_lockSources.Count == 1) _onLock.Invoke();
        }

        public static void RemoveLockSource(Object source)
        {
            _lockSources.Remove(source);
            if (_lockSources.Count == 0) _onUnlock.Invoke();
        }

        private static readonly UnityEvent _onLock = new();
        private static readonly UnityEvent _onUnlock = new();

        private Button _button;

        private void OnEnable()
        {
            _button = GetComponent<Button>();
            _onLock.AddListener(Lock);
            _onUnlock.AddListener(Unlock);
        }

        private void OnDisable()
        {
            _onLock.RemoveListener(Lock);
            _onUnlock.RemoveListener(Unlock);
        }

        private void Lock()
        {
            _button.interactable = false;
        }

        private void Unlock()
        {
            _button.interactable = true;
        }
    }
}