using System.Collections.Generic;
using Gilzoide.RoundedCorners;
using Mirror;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class PlayerListItem : MonoBehaviour
    {
        [SerializeField] [Required] private RoundedImage _skinIcon;
        [SerializeField] [Required] private TextMeshProUGUI _playerNameText;

        [SerializeField] [Required] private Slider _voiceVolumeSlider;

        [SerializeField] [Required] private Button _kickButton;
        [SerializeField] [Required] private Button _banButton;

        private PlayerController _player;

        public void Build(PlayerController player)
        {
            _player = player;

            _playerNameText.text = player.PlayerName;
            _skinIcon.Sprite = PlayerController.LoadedSkins[player.PlayerSkinIndex].Icon;

            _voiceVolumeSlider.onValueChanged.AddListener(OnVoiceVolumeChanged);
            _voiceVolumeSlider.value = SettingsManager.ActiveSettings.PlayerVoiceVolumePercents.GetValueOrDefault(player.PlayerUID, 100f);

            if (NetworkServer.active)
            {
                _kickButton.onClick.AddListener(OnKickClicked);
                _banButton.onClick.AddListener(OnBanClicked);
            }
            else
            {
                _kickButton.interactable = false;
                _banButton.interactable = false;
            }
            
            PlayerPresenceFeed.OnPlayerLeave.AddListener(OnPlayerLeave);
        }

        private void OnDestroy()
        {
            PlayerPresenceFeed.OnPlayerLeave.RemoveListener(OnPlayerLeave);
        }

        private void OnPlayerLeave(PlayerController player)
        {
            if (_player == player)
            {
                Destroy(gameObject);
            }
        }
        
        private void OnKickClicked()
        {
            if (!NetworkServer.active) return;

            _player.connectionToClient.Disconnect();
            Destroy(gameObject);
        }

        private void OnBanClicked()
        {
            if (!NetworkServer.active) return;

            ((Networking.NetworkManager)NetworkManager.singleton).BanPlayer(_player);
            Destroy(gameObject);
        }

        private void OnVoiceVolumeChanged(float value)
        {
            SettingsManager.ActiveSettings.PlayerVoiceVolumePercents[_player.PlayerUID] = value;
            SettingsManager.SaveToDisk();
        }
    }
}