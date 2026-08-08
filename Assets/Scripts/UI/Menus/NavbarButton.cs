using Sirenix.OdinInspector;
using UnityEngine;

namespace UI
{
    public class NavbarButton : MonoBehaviour
    {
        private MainMenuButton _mainMenuButton;
        [SerializeField] [RequiredIn(PrefabKind.PrefabInstance)] private Transform _settingsTab;

        private void Awake()
        {
            _mainMenuButton = GetComponent<MainMenuButton>();
        }

        public void Update()
        {
            _settingsTab.gameObject.SetActive(_mainMenuButton.ForcedActive);
        }
    }
}
