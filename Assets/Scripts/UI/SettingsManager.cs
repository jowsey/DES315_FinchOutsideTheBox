using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoIP;
using Random = UnityEngine.Random;

namespace UI
{
    [Serializable]
    public class UserSettings
    {
        public bool PushToTalk = true;
        public string InputDevice = null;
        public float VoiceChatVolume = 1.0f;
        public string PlayerName = SettingsManager.GetRandomName();
    }

    public class SettingsManager : MonoBehaviour
    {
        public static readonly string[] DefaultPlayerNames =
        {
            "Buttons", "Jupiter", "Misha",
            "Avocado", "Kato",
            "Marley", "Mittens", "Chez", "Batman", "Juno",
            "Felix", "Mollie", "Luna", "Kylo", "Padmé", "Clyde", "Julita",
        };

        public static string GetRandomName() => DefaultPlayerNames[Random.Range(0, DefaultPlayerNames.Length)];

        public static string SettingsFilePath => Application.persistentDataPath + "/settings.json";

        public static UserSettings ActiveSettings { get; private set; }

        // Game
        [SerializeField] [Required] private TMP_InputField _playerNameText;

        // Audio
        [SerializeField] [Required] private Toggle _pttToggle;
        [SerializeField] [Required] private TMP_Dropdown _inputDeviceDropdown;
        [SerializeField] [Required] private Slider _voiceChatVolumeSlider;

        private string[] _oldInputDevices; //checked against Microphone.devices every frame for changes in the list

        private void OnEnable()
        {
            LoadFromDisk();

            // Game
            _playerNameText.onValueChanged.AddListener(OnPlayerNameChanged);

            // Audio
            _pttToggle.onValueChanged.AddListener(OnPttToggleChanged);
            _inputDeviceDropdown.onValueChanged.AddListener(OnInputDeviceChanged);
            _voiceChatVolumeSlider.onValueChanged.AddListener(OnVoiceChatVolumeChanged);
        }

        private void OnDisable()
        {
            // Game
            _playerNameText.onValueChanged.RemoveListener(OnPlayerNameChanged);

            // Audio
            _pttToggle.onValueChanged.RemoveListener(OnPttToggleChanged);
            _inputDeviceDropdown.onValueChanged.RemoveListener(OnInputDeviceChanged);
            _inputDeviceDropdown.ClearOptions();
            _voiceChatVolumeSlider.onValueChanged.RemoveListener(OnVoiceChatVolumeChanged);
        }

        private void Start()
        {
            _oldInputDevices = Microphone.devices;
        }

        private void Update()
        {
            //Poll input device list changes
            if (!Microphone.devices.SequenceEqual(_oldInputDevices))
            {
                _inputDeviceDropdown.ClearOptions();
                _inputDeviceDropdown.AddOptions(new List<string> { "None" });
                _inputDeviceDropdown.AddOptions(Microphone.devices.ToList());

                if (!Microphone.devices.Contains(ActiveSettings.InputDevice))
                {
                    //The currently active input device has been removed, set it to null
                    SetInputDevice(null);
                }

                _oldInputDevices = Microphone.devices;
            }
        }

        private void SetInputDevice(string device)
        {
            ActiveSettings.InputDevice = device;
            NetworkClient.localPlayer?.GetComponent<VoipClient>().SetDevice(ActiveSettings.InputDevice);
        }

        private void AlignUIWithSettings()
        {
            // Game
            _playerNameText.text = ActiveSettings.PlayerName;

            // Audio
            _pttToggle.isOn = ActiveSettings.PushToTalk;
            _inputDeviceDropdown.ClearOptions();
            _inputDeviceDropdown.AddOptions(new List<string> { "None" });
            _inputDeviceDropdown.AddOptions(Microphone.devices.ToList());
            _inputDeviceDropdown.value = (Microphone.devices.Length == 0) ? 0 : Microphone.devices.ToList().IndexOf(ActiveSettings.InputDevice) + 1;
        }

        private void OnPlayerNameChanged(string val)
        {
            ActiveSettings.PlayerName = val;
            SaveToDisk();
        }

        private void OnPttToggleChanged(bool val)
        {
            ActiveSettings.PushToTalk = val;
            SaveToDisk();
        }

        private void OnInputDeviceChanged(int _)
        {
            string uiText = _inputDeviceDropdown.options[_inputDeviceDropdown.value].text;
            SetInputDevice(uiText == "None" ? null : uiText);
            SaveToDisk();
        }

        private void OnVoiceChatVolumeChanged(float val)
        {
            ActiveSettings.VoiceChatVolume = val;
            SaveToDisk();
        }

        private void SaveToDisk()
        {
            var json = JsonConvert.SerializeObject(ActiveSettings, Formatting.Indented);
            System.IO.File.WriteAllText(SettingsFilePath, json);
        }

        private void LoadFromDisk()
        {
            if (System.IO.File.Exists(SettingsFilePath))
            {
                var json = System.IO.File.ReadAllText(SettingsFilePath);
                ActiveSettings = JsonConvert.DeserializeObject<UserSettings>(json);

                if (!Microphone.devices.Contains(ActiveSettings.InputDevice))
                {
                    ActiveSettings.InputDevice = null;
                }

                SetInputDevice(ActiveSettings.InputDevice);
            }
            else
            {
                Debug.Log("Tried loading settings but no file found, using defaults");
                ActiveSettings = new UserSettings();
                SaveToDisk();
            }

            AlignUIWithSettings();
        }
    }
}