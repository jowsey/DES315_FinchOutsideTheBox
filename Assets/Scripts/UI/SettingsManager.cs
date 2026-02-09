using System;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [Serializable]
    public class UserSettings
    {
        public bool PushToTalk = true;
    }

    public class SettingsManager : MonoBehaviour
    {
        public static string SettingsFilePath => Application.persistentDataPath + "/settings.json";

        public static UserSettings ActiveSettings { get; private set; }

        [SerializeField] [Required] private Toggle _pttToggle;

        private void OnEnable()
        {
            LoadFromDisk();

            _pttToggle.onValueChanged.AddListener(OnPttToggleChanged);
        }

        private void OnDisable()
        {
            _pttToggle.onValueChanged.RemoveListener(OnPttToggleChanged);
        }

        private void AlignUIWithSettings()
        {
            _pttToggle.isOn = ActiveSettings.PushToTalk;
        }

        private void OnPttToggleChanged(bool isOn)
        {
            ActiveSettings.PushToTalk = isOn;
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