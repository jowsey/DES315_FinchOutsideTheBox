using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AK.Wwise;
using Mirror;
using Newtonsoft.Json;
using PrimeTween;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
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
        public bool HideTutorialPrompts = false;

        // Audio
        public float MasterVolumePercent = 100.0f;
        public float MusicVolumePercent = 75.0f;
        public float SfxVolumePercent = 75.0f;
        public bool PushToTalk = true;
        public bool NoiseSuppression = true;
        public bool PushToTalkSfx = true;
        public string InputDevice = null;
        
        // Video
        public bool Fullscreen = true;
        public bool VerticalSync = true;
        public float UpscalingPercent = 100.0f;

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
            "Buttons", "Jupiter", "Misha", "Squid", // Becca's
            "Avocado", "Kato", // Paolo's
            "Ekko", // Joshua's
            "Marley", "Mittens", "Chez", "Batman", "Bella", // Jowsey's
            "Felix", "Mollie", "Luna", "Kylo", "Padmé", "Clyde", "Julita", // Ellis'
            "Zak", // Zo's
        };

        public static string GetRandomName() => DefaultPlayerNames[Random.Range(0, DefaultPlayerNames.Length)];

        public static string SettingsFilePath => $"{Application.persistentDataPath}/settings{(Application.isEditor ? "-editor" : "")}.json";

        public static UserSettings ActiveSettings { get; private set; }

        [SerializeField] [Required] private Button _resetButton;

        // Game
        [SerializeField] [Required] private TMP_InputField _playerNameText;
        [SerializeField] [Required] private Slider _firstPersonFovSlider;
        [SerializeField] [Required] private Slider _firstPersonSensitivitySlider;
        [SerializeField] [Required] private Toggle _showTutorialPromptsToggle;
        
        // Audio
        [SerializeField] [Required] private Slider _masterVolumeSlider;
        [SerializeField] [Required] private Slider _musicVolumeSlider;
        [SerializeField] [Required] private Slider _sfxVolumeSlider;
        [SerializeField] [Required] private Toggle _pttToggle;
        [SerializeField] [Required] private Toggle _pttSfxToggle;
        [SerializeField] [Required] private Toggle _noiseSuppressionToggle;
        [SerializeField] [Required] private TMP_Dropdown _inputDeviceDropdown;

        private string[] _oldInputDevices; //checked against Microphone.devices every frame for changes in the list

        [SerializeField] [Required] private RTPC _masterVolumeRtpc;
        [SerializeField] [Required] private RTPC _musicVolumeRtpc;
        [SerializeField] [Required] private RTPC _sfxVolumeRtpc;
        
        // Video
        [SerializeField] [Required] private TMP_Dropdown _windowModeDropdown;
        [SerializeField] [Required] private Toggle _vsyncToggle;
        [SerializeField] [Required] private Slider _upscalerPercentSlider;

        private static string _sessionRandomName;

        private static CancellationTokenSource _saveCts;

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
            _showTutorialPromptsToggle.onValueChanged.AddListener(OnShowTutorialPromptsChanged);

            // Audio
            _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            _musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            _sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

            _pttToggle.onValueChanged.AddListener(OnPttToggleChanged);
            _pttSfxToggle.onValueChanged.AddListener(OnPttSfxToggleChanged);
            _noiseSuppressionToggle.onValueChanged.AddListener(OnNoiseSuppressionToggleChanged);
            _inputDeviceDropdown.onValueChanged.AddListener(OnInputDeviceChanged);
            
            // Video
            _windowModeDropdown.onValueChanged.AddListener(OnWindowModeChanged);
            _vsyncToggle.onValueChanged.AddListener(OnVsyncToggleChanged);
            _upscalerPercentSlider.onValueChanged.AddListener(OnUpscalerPercentChanged);
        }

        private void OnDisable()
        {
            _resetButton.onClick.RemoveListener(ResetSettings);

            // Game
            _playerNameText.onValueChanged.RemoveListener(OnPlayerNameChanged);
            _firstPersonFovSlider.onValueChanged.RemoveListener(OnFirstPersonFovChanged);
            _firstPersonSensitivitySlider.onValueChanged.RemoveListener(OnFirstPersonSensitivityChanged);
            _showTutorialPromptsToggle.onValueChanged.RemoveListener(OnShowTutorialPromptsChanged);

            // Audio
            _masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            _musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            _sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);

            _pttToggle.onValueChanged.RemoveListener(OnPttToggleChanged);
            _pttSfxToggle.onValueChanged.RemoveListener(OnPttSfxToggleChanged);
            _noiseSuppressionToggle.onValueChanged.RemoveListener(OnNoiseSuppressionToggleChanged);
            _inputDeviceDropdown.onValueChanged.RemoveListener(OnInputDeviceChanged);
            _inputDeviceDropdown.ClearOptions();
            
            // Video
            _windowModeDropdown.onValueChanged.RemoveListener(OnWindowModeChanged);
            _vsyncToggle.onValueChanged.RemoveListener(OnVsyncToggleChanged);
            _upscalerPercentSlider.onValueChanged.RemoveListener(OnUpscalerPercentChanged);
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
            QueueSaveToDisk();

            // If currently in a game, sync name
            if (NetworkClient.active && PlayerController.LocalPlayer)
            {
                PlayerController.LocalPlayer.GetComponent<PlayerController>().PlayerName = val;
            }
        }

        private void OnFirstPersonFovChanged(float val)
        {
            ActiveSettings.FirstPersonFov = val;
            QueueSaveToDisk();

            if (CameraZoomController.FirstPerson && Camera.main)
            {
                Camera.main.fieldOfView = val;
            }
        }

        private void OnFirstPersonSensitivityChanged(float val)
        {
            ActiveSettings.FirstPersonSensPercent = val;
            QueueSaveToDisk();
        }

        private void OnShowTutorialPromptsChanged(bool val)
        {
            ActiveSettings.HideTutorialPrompts = val;
            QueueSaveToDisk();
        }

        private void OnMasterVolumeChanged(float val)
        {
            ActiveSettings.MasterVolumePercent = val;
            QueueSaveToDisk();

            _masterVolumeRtpc.SetGlobalValue((val / 100) * MaxRtpcVolume);
        }

        private void OnMusicVolumeChanged(float val)
        {
            ActiveSettings.MusicVolumePercent = val;
            QueueSaveToDisk();

            _musicVolumeRtpc.SetGlobalValue((val / 100) * MaxRtpcVolume);
        }

        private void OnSfxVolumeChanged(float val)
        {
            ActiveSettings.SfxVolumePercent = val;
            QueueSaveToDisk();

            _sfxVolumeRtpc.SetGlobalValue((val / 100) * MaxRtpcVolume);
        }

        private void OnPttToggleChanged(bool val)
        {
            ActiveSettings.PushToTalk = val;
            QueueSaveToDisk();
        }

        private void OnPttSfxToggleChanged(bool val)
        {
            ActiveSettings.PushToTalkSfx = val;
            QueueSaveToDisk();
        }

        private void OnNoiseSuppressionToggleChanged(bool val)
        {
            ActiveSettings.NoiseSuppression = val;
            QueueSaveToDisk();
        }

        private void OnInputDeviceChanged(int val)
        {
            string uiText = _inputDeviceDropdown.options[val].text;
            SetInputDevice(uiText == "None" ? null : uiText);
            QueueSaveToDisk();
        }

        private void OnWindowModeChanged(int val)
        {
            ActiveSettings.Fullscreen = val == 0;
            QueueSaveToDisk();

            Screen.fullScreenMode = val == 0 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        private void OnVsyncToggleChanged(bool val)
        {
            ActiveSettings.VerticalSync = val;
            QueueSaveToDisk();

            QualitySettings.vSyncCount = val ? 1 : 0;
        }

        private void OnUpscalerPercentChanged(float val)
        {
            ActiveSettings.UpscalingPercent = val;
            QueueSaveToDisk();

            DynamicResolutionHandler.SetDynamicResScaler(() => val, DynamicResScalePolicyType.ReturnsPercentage);
        }

        private void ResetSettings()
        {
            Tween.Scale(_resetButton.transform, Vector3.one * 0.95f, 0.1f, Ease.OutCubic, 2, CycleMode.Yoyo);

            if (File.Exists(SettingsFilePath))
            {
                File.Delete(SettingsFilePath);
            }

            LoadFromDisk();
        }

        public static void QueueSaveToDisk()
        {
            _saveCts?.Cancel();
            _saveCts?.Dispose();

            _saveCts = new CancellationTokenSource();
            _ = SaveDebounced(_saveCts.Token);
        }

        private static async Awaitable SaveDebounced(CancellationToken token)
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(0.5f, token);

                var json = JsonConvert.SerializeObject(ActiveSettings, Formatting.Indented);
                await File.WriteAllTextAsync(SettingsFilePath, json, token);
            }
            catch (OperationCanceledException)
            {
                // another queued call
            }
        }

        public void LoadFromDisk()
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                ActiveSettings = JsonConvert.DeserializeObject<UserSettings>(json);
            }
            else
            {
                Debug.Log("Tried loading settings but no file found, using defaults");
                ActiveSettings = new UserSettings();
                QueueSaveToDisk();
            }
            
            // Game
            _playerNameText.text = ActiveSettings.PlayerName;
            _firstPersonFovSlider.value = ActiveSettings.FirstPersonFov;
            _firstPersonSensitivitySlider.value = ActiveSettings.FirstPersonSensPercent;
            _showTutorialPromptsToggle.isOn = ActiveSettings.HideTutorialPrompts;

            if (CameraZoomController.FirstPerson && Camera.main)
            {
                Camera.main.fieldOfView = ActiveSettings.FirstPersonFov;
            }

            // Audio
            _masterVolumeSlider.value = ActiveSettings.MasterVolumePercent;
            _musicVolumeSlider.value = ActiveSettings.MusicVolumePercent;
            _sfxVolumeSlider.value = ActiveSettings.SfxVolumePercent;

            _noiseSuppressionToggle.isOn = ActiveSettings.NoiseSuppression;
            _pttToggle.isOn = ActiveSettings.PushToTalk;
            _pttSfxToggle.isOn = ActiveSettings.PushToTalkSfx;

            if (!Microphone.devices.Contains(ActiveSettings.InputDevice)) ActiveSettings.InputDevice = null;
            SetInputDevice(ActiveSettings.InputDevice);

            _inputDeviceDropdown.ClearOptions();
            _inputDeviceDropdown.AddOptions(new List<string> { "None" });
            _inputDeviceDropdown.AddOptions(Microphone.devices.ToList());
            _inputDeviceDropdown.value = (Microphone.devices.Length == 0) ? 0 : Microphone.devices.ToList().IndexOf(ActiveSettings.InputDevice) + 1;

            _masterVolumeRtpc.SetGlobalValue((ActiveSettings.MasterVolumePercent / 100) * MaxRtpcVolume);
            _musicVolumeRtpc.SetGlobalValue((ActiveSettings.MusicVolumePercent / 100) * MaxRtpcVolume);
            _sfxVolumeRtpc.SetGlobalValue((ActiveSettings.SfxVolumePercent / 100) * MaxRtpcVolume);
            
            // Video
            _windowModeDropdown.value = ActiveSettings.Fullscreen ? 0 : 1;
            _vsyncToggle.isOn = ActiveSettings.VerticalSync;
            _upscalerPercentSlider.value = ActiveSettings.UpscalingPercent;
        }
    }
}