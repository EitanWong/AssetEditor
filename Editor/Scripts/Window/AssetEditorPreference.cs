#if UNITY_EDITOR
// ReSharper disable once CheckNamespace
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace AssetEditor.Editor.Window
{
    public class AssetEditorPreference : UnityEditor.Editor
    {
        private static AssetEditorConfig _config= ScriptableObject.CreateInstance<AssetEditorConfig>();

        public static AssetEditorConfig Config
        {
            get
            {
                if (!_config)
                    _config = ScriptableObject.CreateInstance<AssetEditorConfig>();
                var configData = EditorPrefs.GetString(SaveKey,
                    EditorJsonUtility.ToJson(_config));
                EditorJsonUtility.FromJsonOverwrite(configData, _config);
                return _config;
            }
        }

        private const string SaveKey = "AssetEditorPreferenceConfig";
#pragma warning disable 618
        [PreferenceItem("AssetEditor")]
#pragma warning restore 618
        public static void PreferencesGUI()
        {
            var editor = UnityEditor.Editor.CreateEditor(_config);
            editor.OnInspectorGUI();
            if (GUI.changed)
            {
                EditorPrefs.SetString(SaveKey, EditorJsonUtility.ToJson(_config));
            }
        }


        [Serializable]
        public class AssetEditorConfig : ScriptableObject
        {
            [SerializeField] private List<string> assetFilterNames = new List<string>();
            public List<string> AssetFilterNames => assetFilterNames;
        }
    }
}
#endif