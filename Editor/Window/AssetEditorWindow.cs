#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AssetEditor.Editor.Window;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using UnityEngine.Playables;
using UnityEngine.Video;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace
namespace AssetEditor.Editor
{
    public class AssetEditorWindow : EditorWindow
    {
        public static readonly List<AssetEditorWindow> Windows = new List<AssetEditorWindow>();
        public string[] assetFilterNames;
        private int _filterSelectIdx;
        private int _assetSelectIdx;

        private bool _filterSelected;
        private string[] _currentAssetsPath;

        private Object _selectAssetObject;
        private Type _currentAssetType;

        private Vector2 _inspectorScrollPos;
        private Vector2 _listScrollPos;

        private string _searchFilterName;
        public AssetEditorWindow Instance => this;

        const string Title = "AssetEditor";

        [MenuItem("Window/AssetEditor")]
        public static void GetWindow()
        {
            if (AssetEditorPreference.Config.AssetFilterNames.Count <= 0)
            {
                SettingsService.OpenUserPreferences("Preferences/AssetEditor");
                return;
            }

            var instance = CreateWindow<AssetEditorWindow>();
            instance.titleContent = new GUIContent(Title);
            if (!Windows.Contains(instance))
                Windows.Add(instance);
        }

        #region EditorBehaviour

        private void OnEnable()
        {
            OnFocus();
        }

        private void OnDestroy()
        {
            if (Windows.Contains(this))
                Windows.Remove(this);
        }

        private void OnFocus()
        {
            assetFilterNames = AssetEditorPreference.Config.AssetFilterNames.ToArray();
        }

        private void OnGUI()
        {
            if (!Instance || assetFilterNames == null) return;
            DrawAssetsFilterListGUI();
            DrawCurrentSelectAssetsListGUI();
            DrawSelectAssetInspector();
        }

        #endregion

        #region DrawGUIMethod

        private void DrawAssetsFilterListGUI()
        {
            if (!Instance || _filterSelected) return;
            var rect = Instance.position;
            DrawListGUI(new Rect(0, 0, rect.width, rect.height), assetFilterNames, value =>
            {
                _filterSelectIdx = value;
                _filterSelected = true;
                titleContent = new GUIContent(string.Format("{0}:{1}", Title, assetFilterNames[_filterSelectIdx]));
            });
        }


        private void DrawCurrentSelectAssetsListGUI()
        {
            if (!Instance || !_filterSelected) return;
            if (assetFilterNames == null || assetFilterNames.Length <= 0 || _filterSelectIdx < 0 ||
                _filterSelectIdx >= assetFilterNames.Length)
            {
                _filterSelected = false;
                return;
            }

            var filterName = assetFilterNames[_filterSelectIdx];
            if (string.IsNullOrWhiteSpace(filterName))
            {
                _filterSelected = false;
                return;
            }

            var rect = Instance.position;

            GUILayout.BeginArea(new Rect(0, 0, rect.width, rect.height * .05f),
                AssetEditorStyles.FrameBoxstyle);
            GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            if (GUILayout.Button("Back", GUILayout.MaxWidth(rect.width * .25f)))
            {
                _filterSelected = false;
                _selectAssetObject = null;
                titleContent = new GUIContent(Title);
            }

            _searchFilterName = EditorGUILayout.TextField("", _searchFilterName, "SearchTextField",
                GUILayout.MaxWidth(rect.width * .75f));

            GUILayout.EndHorizontal();
            GUILayout.EndArea();


            _currentAssetsPath = GetAssetsPath(filterName);
            if (_currentAssetsPath == null || _currentAssetsPath.Length <= 0) return;

            Object[] assets = new Object[_currentAssetsPath.Length];
            for (int i = 0; i < _currentAssetsPath.Length; i++)
                assets[i] = AssetDatabase.LoadAssetAtPath(_currentAssetsPath[i], typeof(Object));

            _currentAssetType = AssetDatabase.LoadAssetAtPath(_currentAssetsPath[0], typeof(Object)).GetType();
            DrawAssetListGUI(new Rect(0, rect.height * .05f, rect.width * .25f, rect.height * .95f), assets,
                value =>
                {
                    _assetSelectIdx = value;
                    var assetPath = _currentAssetsPath[_assetSelectIdx];
                    _selectAssetObject = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object));
                }
                , ONContextClickHandler
            );
        }

        private void ONContextClickHandler(Event evt)
        {
            if (_currentAssetType == null) return;

            if (_currentAssetType.BaseType == typeof(UnityEngine.ScriptableObject))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Create New"), false, CreateScritableObject);
                //menu.AddSeparator("");
                menu.ShowAsContext();
                evt.Use();
            }
        }


        private void DrawSelectAssetInspector()
        {
            if (!Instance || !_filterSelected) return;
            if (_assetSelectIdx < 0 || _assetSelectIdx >= _currentAssetsPath.Length) return;
            if (!_selectAssetObject) return;
            var rect = Instance.position;


            GUILayout.BeginArea(new Rect(rect.width * .25f, rect.height * .05f, rect.width * .75f, rect.height * .95f),
                AssetEditorStyles.FrameBoxstyle);
            GUILayout.Label(string.Format("{0}: {1}", assetFilterNames[_filterSelectIdx], _selectAssetObject.name));
            _inspectorScrollPos = EditorGUILayout.BeginScrollView(_inspectorScrollPos);
            var editor = UnityEditor.Editor.CreateEditor(_selectAssetObject);

            if (editor)
            {
                // ReSharper disable once Unity.NoNullPropagation
                editor?.OnInspectorGUI();
                var previewRect = GUILayoutUtility.GetRect(rect.width * .5f, rect.height * .5f);
                // ReSharper disable once Unity.NoNullPropagation
                editor?.OnInteractivePreviewGUI(previewRect, EditorStyles.whiteLabel);
                GUILayout.BeginHorizontal();
                // ReSharper disable once Unity.NoNullPropagation
                editor?.OnPreviewSettings();
                GUILayout.EndHorizontal();
            }


            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal(AssetEditorStyles.FrameBoxstyle);
            if (GUILayout.Button("UnSelect"))
            {
                _assetSelectIdx = -1;
            }


            if (GUILayout.Button("Ping"))
            {
                //var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(_itemAssetGuids[_selectIdx]);
                Selection.activeObject = _selectAssetObject;
                EditorGUIUtility.PingObject(_selectAssetObject);
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawListGUI(Rect areaRect, string[] assetNames, Action<int> callBack = null,
            Action<Event> onContextClick = null)
        {
            if (!Instance || assetNames == null || assetNames.Length <= 0) return;
            GUILayout.BeginArea(areaRect,
                AssetEditorStyles.FrameBoxstyle);
            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos);

            var guiContent = new GUIContent();
            for (int i = 0; i < assetNames.Length; i++)
            {
                var assetName = assetNames[i];
                if (string.IsNullOrWhiteSpace(assetName)) continue;
                guiContent.text = assetName;
                var type = GetBuildinAssetTypeByName(assetName);
                guiContent.image = AssetPreview.GetMiniTypeThumbnail(type);
                if (GUILayout.Button(guiContent, GUILayout.MaxHeight(50),
                    GUILayout.ExpandWidth(true)))
                    callBack?.Invoke(i);
            }

            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.ContextClick:
                    onContextClick?.Invoke(evt);
                    break;
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawAssetListGUI(Rect areaRect, Object[] assets, Action<int> callBack = null,
            Action<Event> onContextClick = null)
        {
            if (!Instance || assets == null || assets.Length <= 0) return;
            GUILayout.BeginArea(areaRect,
                AssetEditorStyles.FrameBoxstyle);
            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos);
            var width = Instance.position.width * .25f;
            var guiContent = new GUIContent();

            for (int i = 0; i < assets.Length; i++)
            {
                var assetObj = assets[i];
                if (!assetObj) continue;
                guiContent.text = assetObj.name;
                guiContent.image = AssetPreview.GetMiniThumbnail(assetObj);
                if (GUILayout.Button(guiContent, GUILayout.MaxHeight(30),
                    GUILayout.MaxWidth(width)))
                    callBack?.Invoke(i);
            }

            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.ContextClick:
                    onContextClick?.Invoke(evt);
                    break;
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        #endregion

        #region PrivateMethod

        /// <summary>
        /// GetAssetsPath
        /// </summary>
        /// <param name="filterName"></param>
        /// <returns></returns>
        private string[] GetAssetsPath(string filterName)
        {
            var assetGuids = AssetDatabase.FindAssets(string.Format("t:{0} {1}", filterName, _searchFilterName));
            string[] result = new string[assetGuids.Length];
            for (int i = 0; i < assetGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                result[i] = path;
            }

            return result;
        }

        private void CreateScritableObject()
        {
            var assetType = _currentAssetType;
            if (assetType.BaseType == typeof(UnityEngine.ScriptableObject))
            {
                var key = string.Format("AssetEditor{0}SavePath", assetType);
                var savePath = EditorPrefs.GetString(key, "Assets");
                var nowSavePath = EditorUtility.SaveFilePanelInProject(string.Format("Save {0}", assetType),
                    string.Format("New {0}.asset", assetType.Name), "asset",
                    "Save At", savePath);
                if (string.IsNullOrWhiteSpace(nowSavePath))
                    return;
                savePath = nowSavePath;
                EditorPrefs.SetString(key, savePath);
                var scriptableObject = UnityEngine.ScriptableObject.CreateInstance(assetType);
                var fileName = Path.GetFileNameWithoutExtension(savePath);
                AssetDatabase.CreateAsset(scriptableObject, savePath);
                Repaint();
            }
        }

        private Type GetBuildinAssetTypeByName(string name)
        {
            name = name.ToLower();
            if (name == nameof(ScriptableObject).ToLower()) return typeof(ScriptableObject);
            if (name == nameof(Texture).ToLower()) return typeof(Texture);
            if (name == nameof(Material).ToLower()) return typeof(Material);
            if (name == nameof(GameObject).ToLower()) return typeof(GameObject);
            if (name == nameof(AudioClip).ToLower()) return typeof(AudioClip);
            if (name == nameof(VideoClip).ToLower()) return typeof(VideoClip);
            if (name == nameof(AudioMixer).ToLower()) return typeof(AudioMixer);
            if (name == nameof(Shader).ToLower()) return typeof(Shader);
            if (name == nameof(TextAsset).ToLower()) return typeof(TextAsset);
            if (name == nameof(MonoScript).ToLower()) return typeof(MonoScript);
            if (name == nameof(SceneAsset).ToLower()) return typeof(SceneAsset);
            if (name == nameof(DefaultAsset).ToLower()) return typeof(DefaultAsset);
            if (name == nameof(Font).ToLower()) return typeof(Font);
            if (name == nameof(Sprite).ToLower()) return typeof(Sprite);
            if (name == nameof(AnimationClip).ToLower()) return typeof(AnimationClip);
            if (name == nameof(Animation).ToLower()) return typeof(Animation);
            if (name == nameof(AnimatorController).ToLower()) return typeof(AnimatorController);
            if (name == nameof(AnimatorOverrideController).ToLower()) return typeof(AnimatorOverrideController);
            if (name == nameof(AvatarMask).ToLower()) return typeof(AvatarMask);
            if (name == nameof(PlayableAsset).ToLower()) return typeof(PlayableAsset);
            if (name == nameof(PhysicMaterial).ToLower()) return typeof(PhysicMaterial);
            if (name == nameof(LightingSettings).ToLower()) return typeof(LightingSettings);
            if (name == nameof(LightmapParameters).ToLower()) return typeof(LightmapParameters);
            if (name == nameof(NavMeshData).ToLower()) return typeof(NavMeshData);
            if (name == nameof(Mesh).ToLower()) return typeof(Mesh);
            if (name == nameof(GUISkin).ToLower()) return typeof(GUISkin);
            if (name == nameof(LensFlare).ToLower()) return typeof(LensFlare);
            if (name == "Prefab".ToLower()) return typeof(GameObject);
            if (name == "Script".ToLower()) return typeof(MonoScript);
            if (name == "Scene".ToLower()) return typeof(SceneAsset);

            var assembly = Assembly.Load("UnityEngine.CoreModule");
            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                if (type.Name.Contains(name))
                    return type;
            }

            return typeof(DefaultAsset);
        }

        #endregion
    }
}
#endif