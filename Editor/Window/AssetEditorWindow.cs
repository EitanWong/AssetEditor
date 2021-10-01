#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AssetEditor.Editor.Window;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using UnityEngine.Playables;
using UnityEngine.UIElements;
using UnityEngine.Video;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace
namespace AssetEditor.Editor
{
    public class AssetEditorWindow : EditorWindow
    {
        public static readonly List<AssetEditorWindow> Windows = new List<AssetEditorWindow>();
        public string[] assetFilterNames;
        public AssetEditorWindow Instance => this;
        const string Title = "AssetEditor";
        private const char SplitChar = '.';
        private IMGUIContainer _imguiAssetInspectorContainer;
        private ScrollView _imguiAssetInspectorScrollView;
        private VisualElement _assetList;

        private string _currentFilterName;
        private string[] _currentAssetsPath;
        private Type _currentAssetType;

        private string _searchFilterName;
        private Object _selectAssetObject;
        private Button _selectClickButton;
        private string _selectAssetGuid;

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
            var sp = titleContent.text.Split(SplitChar);
            if (sp.Length > 1)
            {
                if (sp.Length > 2)
                {
                    OnFilterSelected(sp[1], sp[2]);
                }
                else
                {
                    OnFilterSelected(sp[1]);
                }
            }
            else
            {
                DrawAssetsFilterListUIElement();
            }
        }

        #endregion

        private void DrawAssetsFilterListUIElement()
        {
            rootVisualElement.Clear();
            titleContent = new GUIContent(Title);
            ScrollView filterScrollView = new ScrollView();
            for (int i = 0; i < assetFilterNames.Length; i++)
            {
                var filterName = assetFilterNames[i];
                Button filterSelectButton = new Button { text = filterName };
                filterSelectButton.Add(new Image()
                {
                    image = AssetPreview.GetMiniTypeThumbnail(GetBuildinAssetTypeByName(filterName)),
                    style =
                    {
                        maxHeight = 25,
                        maxWidth = 25,
                        minHeight = 25,
                        minWidth = 25,
                    }
                });
                filterSelectButton.RegisterCallback<ClickEvent>(evt => OnFilterSelected(filterName));
                filterScrollView.Add(filterSelectButton);
            }

            rootVisualElement.Add(filterScrollView);
        }

        private void OnFilterSelected(string filterName, string assetGuid = null)
        {
            if (!string.IsNullOrWhiteSpace(assetGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (!string.IsNullOrWhiteSpace(path))
                    _selectAssetObject = AssetDatabase.LoadAssetAtPath<Object>(path);
                _selectAssetGuid = assetGuid;
            }

            DrawAssetEditorUIElement(filterName);
        }


        private void DrawAssetEditorUIElement(string filterName)
        {
            rootVisualElement.Clear();
            _currentFilterName = filterName.Trim();

            UpdateTitle();

            VisualElement toolbar = new VisualElement()
            {
                name = "ToolBar",
                style =
                {
                    minHeight = 20,
                    maxHeight = 25,
                    flexDirection = FlexDirection.Row
                }
            };

            Button backButton = new Button
            {
                text = "Back",
                style =
                {
                    width = new StyleLength(Length.Percent(10)),
                }
            };
            backButton.RegisterCallback<ClickEvent>(evt => DrawAssetsFilterListUIElement());
            //backButton.style.maxWidth = new StyleLength(Length.Percent(25));
            toolbar.Add(backButton);

            ToolbarSearchField searchTextField = new ToolbarSearchField()
            {
                style =
                {
                    width = new StyleLength(Length.Percent(90)),
                }
            };

            searchTextField.value = _searchFilterName;
            searchTextField.RegisterValueChangedCallback(evt =>
                {
                    _searchFilterName = evt.newValue;
                    DrawAssetButtonListUIElement();
                }
            );
            toolbar.Add(searchTextField);
            rootVisualElement.Add(toolbar);
            DrawAssetButtonListUIElement();
        }

        private void DrawAssetButtonListUIElement()
        {
            if (_assetList != null && rootVisualElement.Contains(_assetList))
            {
                _assetList.Clear();
                rootVisualElement.Remove(_assetList);
                _assetList = null;
            }

            _assetList = new VisualElement()
            {
                name = "AssetList",
                style =
                {
                    minHeight = new StyleLength(Length.Percent(95)),
                }
            };
            _assetList.style.flexDirection = FlexDirection.Row;
            ScrollView assetScrollView = new ScrollView()
            {
                style =
                {
                    backgroundColor = new Color(48f / 255, 48f / 255, 48f / 255),
                    minWidth = new StyleLength(Length.Percent(25)),
                    width = new StyleLength(Length.Percent(25))
                }
            };


            _assetList.Add(assetScrollView);
            _currentAssetsPath = GetAssetsPath(_currentFilterName);

            if (_currentAssetsPath != null && _currentAssetsPath.Length > 0)
            {
                var loadObj = AssetDatabase.LoadAssetAtPath(_currentAssetsPath[0], typeof(Object));
                _currentAssetType = loadObj.GetType();
            }

            if (_currentAssetsPath != null && _currentAssetsPath.Length > 0)
            {
                foreach (var path in _currentAssetsPath)
                {
                    var assetObj = AssetDatabase.LoadAssetAtPath(path, typeof(Object));
                    Button assetButton = new Button
                    {
                        text = assetObj.name,
                        userData = assetObj
                    };
                    assetButton.Add(new Image()
                    {
                        image = AssetPreview.GetMiniThumbnail(assetObj),
                        style =
                        {
                            maxHeight = 25,
                            maxWidth = 25,
                            minHeight = 25,
                            minWidth = 25,
                        }
                    });
                    assetButton.RegisterCallback<ClickEvent>(evt =>
                    {
                        _selectAssetObject = assetObj;
                        _selectAssetGuid = AssetDatabase.AssetPathToGUID(path);
                        SelectButton(assetButton);
                    });

                    if ((Object)assetButton.userData == _selectAssetObject)
                    {
                        SelectButton(assetButton);
                    }

                    assetButton.RegisterCallback<ContextClickEvent>(evt => ONContextClickHandler(evt));
                    assetScrollView.Add(assetButton);
                }
            }

            //_assetList.RegisterCallback<ContextClickEvent>(evt => { ONContextClickHandler(evt); });
            rootVisualElement.Add(_assetList);
        }

        private void SelectButton(Button assetButton)
        {
            if (_selectClickButton != null)
                ResetButtonStyle(_selectClickButton);
            _selectClickButton = assetButton;
            DrawCurrentClickButtonBorderEffect(assetButton);
            DrawAssetInspectorEditor(_selectAssetObject);
            UpdateTitle();
        }

        private void DrawCurrentClickButtonBorderEffect(Button button)
        {
            _selectClickButton = button;
            _selectClickButton.style.borderBottomColor = Color.cyan;
            _selectClickButton.style.borderLeftColor = Color.cyan;
            _selectClickButton.style.borderRightColor = Color.cyan;
            _selectClickButton.style.borderTopColor = Color.cyan;
        }


        private void ONContextClickHandler(ContextClickEvent evt)
        {
            if (_currentAssetType == null || evt.target == null) return;
            try
            {
                var menu = new GenericMenu();
                if (HasInheritType<Button>(evt.target.GetType()))
                {
                    var target = ((Button)evt.target);
                    var assetObj = (Object)target.userData;

                    menu.AddItem(new GUIContent("Ping"), false, PingObject, assetObj);

                    if (_selectClickButton != null)
                    {
                        var currentObj = (Object)_selectClickButton.userData;
                        if (currentObj == assetObj)
                            menu.AddItem(new GUIContent("UnSelect"), false, UnSelectCurrentAssetObject, assetObj);
                    }

                    menu.AddSeparator("");

                    if (HasInheritType<ScriptableObject>(assetObj.GetType()))
                        menu.AddItem(new GUIContent("Create New"), false, CreateScriptableObject, assetObj);
                    //menu.AddSeparator("");    
                }

                menu.ShowAsContext();
                evt.imguiEvent.Use();
            }
#pragma warning disable 168
            catch (Exception e)
#pragma warning restore 168
            {
                // ignored
            }
        }


        private void DrawAssetInspectorEditor(Object assetObj)
        {
            if (_assetList == null) return;
            RemoveAssetInspectorEditor(_selectAssetObject);
            _selectAssetObject = assetObj;
            _imguiAssetInspectorScrollView = new ScrollView()
            {
                contentViewport =
                {
                    style =
                    {
                        paddingTop = new StyleLength(Length.Percent(3)),
                        width = new StyleLength(Length.Percent(115)),
                        borderBottomLeftRadius = 12,
                        borderBottomRightRadius = 12,
                        borderTopLeftRadius = 12,
                        borderTopRightRadius = 12,
                    }
                },
                style =
                {
                    width = new StyleLength(Length.Percent(85)),
                    paddingRight = new StyleLength(Length.Percent(10)),
                    paddingLeft = new StyleLength(Length.Percent(1)),
                    borderBottomColor = Color.cyan,
                    borderTopColor = Color.cyan,
                    borderLeftColor = Color.cyan,
                    borderRightColor = Color.cyan,
                    borderBottomLeftRadius = 12,
                    borderBottomRightRadius = 12,
                    borderTopLeftRadius = 12,
                    borderTopRightRadius = 12,
                    borderBottomWidth = 1,
                    borderTopWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                }
            };


            Label assetInfoLabel = new Label()
            {
                style =
                {
                    paddingTop = 5,
                    paddingBottom = 5,
                }
            };
            assetInfoLabel.text = string.Format("{0}:{1}", assetObj.GetType().Name, assetObj.name);
            _imguiAssetInspectorScrollView.Add(assetInfoLabel);

            _imguiAssetInspectorContainer = new IMGUIContainer();
            //_imguiAssetInspectorContainer.style.minWidth= new StyleLength(Length.Percent(100));
            //_imguiAssetInspectorContainer.style.minWidth = position.width-250;
            //_imguiAssetInspectorContainer.style.maxWidth = new StyleLength(Length.Percent(100));
            _imguiAssetInspectorContainer.onGUIHandler = DrawIMGUIInspector;
            _imguiAssetInspectorScrollView.Add(_imguiAssetInspectorContainer);
            _assetList.Add(_imguiAssetInspectorScrollView);
        }


        private void DrawIMGUIInspector()
        {
            var editor = UnityEditor.Editor.CreateEditor(_selectAssetObject);
            if (editor)
            {
                var rect = Instance.position;
                // ReSharper disable once Unity.NoNullPropagation
                editor?.OnInspectorGUI();
                var previewRect = GUILayoutUtility.GetRect(rect.width * .5f, rect.height * .5f);
                // ReSharper disable once Unity.NoNullPropagation
                if (editor.HasPreviewGUI())
                    editor?.OnInteractivePreviewGUI(previewRect, EditorStyles.whiteLabel);
                GUILayout.BeginHorizontal();
                // ReSharper disable once Unity.NoNullPropagation
                editor?.OnPreviewSettings();
                GUILayout.EndHorizontal();
            }
        }


        #region PrivateMethod

        /// <summary>
        /// GetAssetsPath
        /// </summary>
        /// <param name="filterName"></param>
        /// <returns></returns>
        private string[] GetAssetsPath(string filterName)
        {
            if (string.IsNullOrWhiteSpace(_searchFilterName))
                _searchFilterName = String.Empty;
            var assetGuids = AssetDatabase.FindAssets(string.Format("t:{0} {1}", filterName, _searchFilterName));
            string[] result = new string[assetGuids.Length];
            for (int i = 0; i < assetGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                result[i] = path;
            }

            return result;
        }

        private void CreateScriptableObject(object userdata)
        {
            if (userdata == null) return;
            var targetType = userdata.GetType();
            if (HasInheritType<ScriptableObject>(targetType))
            {
                var key = string.Format("AssetEditor.SavePath.{0}", targetType);
                var savePath = EditorPrefs.GetString(key, "Assets");
                var nowSavePath = EditorUtility.SaveFilePanelInProject(string.Format("Save {0}", targetType),
                    string.Format("New {0}.asset", targetType.Name), "asset",
                    "Save At", savePath);
                if (string.IsNullOrWhiteSpace(nowSavePath))
                    return;
                savePath = nowSavePath;
                EditorPrefs.SetString(key, savePath);
                var scriptableObject = UnityEngine.ScriptableObject.CreateInstance(targetType);
                var fileName = Path.GetFileNameWithoutExtension(savePath);
                AssetDatabase.CreateAsset(scriptableObject, savePath);
                Repaint();
            }
        }

        private void PingObject(object userData)
        {
            var assetObj = (Object)userData;
            if (!assetObj) return;
            Selection.activeObject = assetObj;
            EditorGUIUtility.PingObject(assetObj);
        }

        private void ResetButtonStyle(Button button)
        {
            if (button != null)
            {
                var originButtonSample = new Button();
                button.style.borderBottomColor = originButtonSample.style.borderBottomColor;
                button.style.borderLeftColor = originButtonSample.style.borderLeftColor;
                button.style.borderRightColor = originButtonSample.style.borderRightColor;
                button.style.borderTopColor = originButtonSample.style.borderTopColor;
            }
        }

        private void UnSelectCurrentAssetObject(object userdata)
        {
            RemoveAssetInspectorEditor(userdata);
            ResetButtonStyle(_selectClickButton);
            _selectClickButton = null;
            _selectAssetObject = null;
            _selectAssetGuid = null;
            UpdateTitle();
        }

        private void RemoveAssetInspectorEditor(object userdata)
        {
            var assetObj = (Object)userdata;
            if (_selectClickButton == null) return;
            var currentObj = (Object)_selectClickButton.userData;
            if (currentObj != assetObj) return;
            if (_assetList == null) return;
            if (_imguiAssetInspectorScrollView != null && _assetList.Contains(_imguiAssetInspectorScrollView))
            {
                _assetList.Remove(_imguiAssetInspectorScrollView);
            }
        }


        private void UpdateTitle()
        {
            if (string.IsNullOrWhiteSpace(_selectAssetGuid))
            {
                titleContent = new GUIContent(string.Format("{0}.{1}", Title, _currentFilterName),
                    AssetPreview.GetMiniTypeThumbnail(GetBuildinAssetTypeByName(_currentFilterName)));
            }
            else
            {
                titleContent = new GUIContent(string.Format("{0}.{1}.{2}", Title, _currentFilterName, _selectAssetGuid),
                    AssetPreview.GetMiniTypeThumbnail(GetBuildinAssetTypeByName(_currentFilterName)));
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

        /// <summary>
        /// Check if class has inherit Type
        /// </summary>
        /// <param name="targetType"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private bool HasInheritType<T>(Type targetType)
        {
            Type type = targetType;
            while (type != null)
            {
                if (type == typeof(T))
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        #endregion
    }
}
#endif