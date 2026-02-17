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

namespace UI
{
    [Serializable]
    public class UserSettings
    {
        public bool PushToTalk = true;
        public string InputDevice = null;
    }

    public class SettingsManager : MonoBehaviour
    {
        public static string SettingsFilePath => Application.persistentDataPath + "/settings.json";

        public static UserSettings ActiveSettings { get; private set; }

        [SerializeField] [Required] private Toggle _pttToggle;
        [SerializeField] [Required] private TMP_Dropdown _inputDeviceDropdown;
        private string[] _oldInputDevices; //checked against Microphone.devices every frame for changes in the list

        private void OnEnable()
        {
            LoadFromDisk();

            _pttToggle.onValueChanged.AddListener(OnPttToggleChanged);
            _inputDeviceDropdown.onValueChanged.AddListener(OnInputDeviceChanged);
        }

        private void OnDisable()
        {
            _pttToggle.onValueChanged.RemoveListener(OnPttToggleChanged);
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
            if (NetworkClient.localPlayer)
            {
                NetworkClient.localPlayer.GetComponent<VoipClient>().SetDevice(ActiveSettings.InputDevice);
            }
        }

        private void AlignUIWithSettings()
        {
            _pttToggle.isOn = ActiveSettings.PushToTalk;
            _inputDeviceDropdown.ClearOptions();
            _inputDeviceDropdown.AddOptions(new List<string> { "None" });
            _inputDeviceDropdown.AddOptions(Microphone.devices.ToList());
            _inputDeviceDropdown.value = (Microphone.devices.Length == 0) ? 0 : Microphone.devices.ToList().IndexOf(ActiveSettings.InputDevice) + 1;
        }

        private void OnPttToggleChanged(bool isOn)
        {
            ActiveSettings.PushToTalk = isOn;
            SaveToDisk();
        }

        private void OnInputDeviceChanged(int _)
        {
            string uiText = _inputDeviceDropdown.options[_inputDeviceDropdown.value].text;
            SetInputDevice(uiText == "None" ? null : uiText);
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
            }

            AlignUIWithSettings();
        }
    }
}