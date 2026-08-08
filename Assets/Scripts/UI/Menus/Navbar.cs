using UnityEngine;

namespace UI
{
    public class Navbar : MonoBehaviour
    {
        private MainMenuButton[] navbarButtons;

        private void Awake()
        {
            navbarButtons = GetComponentsInChildren<MainMenuButton>();
        }

        public void OnNavbarButtonClick(MainMenuButton clickedButton)
        {
            foreach (MainMenuButton button in navbarButtons)
            {
                button.SetForcedActive(button == clickedButton);
            }
        }
    }
}