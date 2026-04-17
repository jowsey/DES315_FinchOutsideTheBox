using System;
using System.Collections.Generic;
using System.Linq;
using AK.Wwise;
using Mirror;
using Newtonsoft.Json;
using PrimeTween;
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
        // Game
        public string PlayerName;
        public float FirstPersonFov = 80f;
        public float FirstPersonSensPercent = 25f;

        // Audio
        public float MasterVolumePercent = 100.0f;
        public float MusicVolumePercent = 75.0f;
        public float SfxVolumePercent = 75.0f;
        public bool PushToTalk = true;
        public bool NoiseSuppression = true;
        public string InputDevice = null;

        public Dictionary<string, float> PlayerVoiceVolumePercents = new();

        // Backend
        public string UserID = Guid.NewGuid().ToString();
    }

    public class SettingsManager : MonoBehaviour
    {
        // Wwise Authoring values. 10 is -6db, 20 is +0dB, 30 is +6db.
        public const float MaxRtpcVolume = 30f;

        public static readonly string[] DefaultPlayerNames =
        {
            "Buttons", "Jupiter", "Misha",
            "Avocado", "Kato",
            "Marley", "Mittens", "Chez", "Batman", "Juno",
            "Felix", "Mollie", "Luna", "Kylo", "Padmé", "Clyde", "Julita",
        };

        public static string GetRandomName() => DefaultPlayerNames[Random.Range(0, DefaultPlayerNames.Length)];

        public static string SettingsFilePath => $"{Application.persistentDataPath}/settings{(Application.isEditor ? "-editor" : "")}.json";

        public static UserSettings ActiveSettings { get; private set; }

        [SerializeField] [Required] private Button _resetButton;

        // Game
        [SerializeField] [Required] private TMP_InputField _playerNameText;
        [SerializeField] [Required] private Slider _firstPersonFovSlider;
        [SerializeField] [Required] private Slider _firstPersonSensitivitySlider;

        // Audio
        [SerializeField] [Required] private Slider _masterVolumeSlider;
        [SerializeField] [Required] private Slider _musicVolumeSlider;
        [SerializeField] [Required] private Slider _sfxVolumeSlider;
        [SerializeField] [Required] private Toggle _pttToggle;
        [SerializeField] [Required] private Toggle _noiseSuppressionToggle;
        [SerializeField] [Required] private TMP_Dropdown _inputDeviceDropdown;

        private string[] _oldInputDevices; //checked against Microphone.devices every frame for changes in the list

        [SerializeField] [Required] private RTPC _masterVolumeRtpc;
        [SerializeField] [Required] private RTPC _musicVolumeRtpc;
        [SerializeField] [Required] private RTPC _sfxVolumeRtpc;

        private static string _sessionRandomName;

        // Gets the player's name, generating a session-consistent random one if none is set
        public static string GetSafeName()
        {
            if (!string.IsNullOrWhiteSpace(ActiveSettings.PlayerName)) return ActiveSettings.PlayerName;

            if (string.IsNullOrEmpty(_sessionRandomName))
                _sessionRandomName = GetRandomName();

            return _sessionRandomName;
        }

        private void OnEnable()
        {
            LoadFromDisk();

            _resetButton.onClick.AddListener(ResetSettings);

            // Game
            _playerNameText.onValueChanged.AddListener(OnPlayerNameChanged);
            _firstPersonFovSlider.onValueChanged.AddListener(OnFirstPersonFovChanged);
            _firstPersonSensitivitySlider.onValueChanged.AddListener(OnFirstPersonSensitivityChanged);

            // Audio
            _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            _musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            _sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

            _pttToggle.onValueChanged.AddListener(OnPttToggleChanged);
            _noiseSuppressionToggle.onValueChanged.AddListener(OnNoiseSuppressionToggleChanged);
            _inputDeviceDropdown.onValueChanged.AddListener(OnInputDeviceChanged);
        }

        private void OnDisable()
        {
            _resetButton.onClick.RemoveListener(ResetSettings);

            // Game
            _playerNameText.onValueChanged.RemoveListener(OnPlayerNameChanged);
            _firstPersonFovSlider.onValueChanged.RemoveListener(OnFirstPersonFovChanged);
            _firstPersonSensitivitySlider.onValueChanged.RemoveListener(OnFirstPersonSensitivityChanged);

            // Audio
            _masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            _musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            _sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);

            _pttToggle.onValueChanged.RemoveListener(OnPttToggleChanged);
            _noiseSuppressionToggle.onValueChanged.RemoveListener(OnNoiseSuppressionToggleChanged);
            _inputDeviceDropdown.onValueChanged.RemoveListener(OnInputDeviceChanged);
            _inputDeviceDropdown.ClearOptions();
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

        private void OnPlayerNameChanged(string val)
        {
            ActiveSettings.PlayerName = val;
            SaveToDisk();
        }

        private void OnFirstPersonFovChanged(float val)
        {
            ActiveSettings.FirstPersonFov = val;
            SaveToDisk();

            if (CameraZoomController.FirstPerson && Camera.main)
            {
                Camera.main.fieldOfView = val;
            }
        }

        private void OnFirstPersonSensitivityChanged(float val)
        {
            ActiveSettings.FirstPersonSensPercent = val;
            SaveToDisk();
        }

        private void OnMasterVolumeChanged(float val)
        {
            ActiveSettings.MasterVolumePercent = val;
            SaveToDisk();

            _masterVolumeRtpc.SetGlobalValue((val / 100) * MaxRtpcVolume);
        }

        private void OnMusicVolumeChanged(float val)
        {
            ActiveSettings.MusicVolumePercent = val;
            SaveToDisk();

            _musicVolumeRtpc.SetGlobalValue((val / 100) * MaxRtpcVolume);
        }

        private void OnSfxVolumeChanged(float val)
        {
            ActiveSettings.SfxVolumePercent = val;
            SaveToDisk();

            _sfxVolumeRtpc.SetGlobalValue((val / 100) * MaxRtpcVolume);
        }

        private void OnPttToggleChanged(bool val)
        {
            ActiveSettings.PushToTalk = val;
            SaveToDisk();
        }

        private void OnNoiseSuppressionToggleChanged(bool val)
        {
            ActiveSettings.NoiseSuppression = val;
            SaveToDisk();
        }

        private void OnInputDeviceChanged(int val)
        {
            string uiText = _inputDeviceDropdown.options[val].text;
            SetInputDevice(uiText == "None" ? null : uiText);
            SaveToDisk();
        }

        private void ResetSettings()
        {
            Tween.Scale(_resetButton.transform, Vector3.one * 0.95f, 0.1f, Ease.OutCubic, 2, CycleMode.Yoyo);

            if (System.IO.File.Exists(SettingsFilePath))
            {
                System.IO.File.Delete(SettingsFilePath);
            }

            LoadFromDisk();
        }

        public static void SaveToDisk()
        {
            var json = JsonConvert.SerializeObject(ActiveSettings, Formatting.Indented);
            System.IO.File.WriteAllText(SettingsFilePath, json);
        }

        public void LoadFromDisk()
        {
            if (System.IO.File.Exists(SettingsFilePath))
            {
                var json = System.IO.File.ReadAllText(SettingsFilePath);
                ActiveSettings = JsonConvert.DeserializeObject<UserSettings>(json);
            }
            else
            {
                Debug.Log("Tried loading settings but no file found, using defaults");
                ActiveSettings = new UserSettings();
                SaveToDisk();
            }

            if (!Microphone.devices.Contains(ActiveSettings.InputDevice))
            {
                ActiveSettings.InputDevice = null;
            }

            SetInputDevice(ActiveSettings.InputDevice);

            ApplySettings();
        }

        private void ApplySettings()
        {
            // Game
            _playerNameText.text = ActiveSettings.PlayerName;
            _firstPersonFovSlider.value = ActiveSettings.FirstPersonFov;
            _firstPersonSensitivitySlider.value = ActiveSettings.FirstPersonSensPercent;

            if (CameraZoomController.FirstPerson && Camera.main)
            {
                Camera.main.fieldOfView = ActiveSettings.FirstPersonFov;
            }

            // Audio
            _musicVolumeSlider.value = ActiveSettings.MusicVolumePercent;
            _sfxVolumeSlider.value = ActiveSettings.SfxVolumePercent;

            _noiseSuppressionToggle.isOn = ActiveSettings.NoiseSuppression;
            _pttToggle.isOn = ActiveSettings.PushToTalk;

            _inputDeviceDropdown.ClearOptions();
            _inputDeviceDropdown.AddOptions(new List<string> { "None" });
            _inputDeviceDropdown.AddOptions(Microphone.devices.ToList());
            _inputDeviceDropdown.value = (Microphone.devices.Length == 0) ? 0 : Microphone.devices.ToList().IndexOf(ActiveSettings.InputDevice) + 1;

            _masterVolumeRtpc.SetGlobalValue((ActiveSettings.MasterVolumePercent / 100) * MaxRtpcVolume);
            _musicVolumeRtpc.SetGlobalValue((ActiveSettings.MusicVolumePercent / 100) * MaxRtpcVolume);
            _sfxVolumeRtpc.SetGlobalValue((ActiveSettings.SfxVolumePercent / 100) * MaxRtpcVolume);
        }
    }
}