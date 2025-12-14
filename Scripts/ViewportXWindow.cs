#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.UIElements;

using Button = UnityEngine.UIElements.Button;
using Toggle = UnityEngine.UIElements.Toggle;

namespace PrefabPreviewer
{
    public class ViewportXWindow : EditorWindow
    {
        private const string MenuPath = "Tools/ViewportX";
        private const string UxmlGuid = "da9ded1f94a3b464abaefea0b9f7c365";
        private const string UssGuid = "784c8e8d65cde5540a8e61ae40dfa81a";
        private const float PerspectiveFieldOfView = 60f;

        private enum PreviewContentType
        {
            None,
            Model,
            Particle,
            UGUI
        }

        private enum UiLanguage
        {
            English,
            Chinese
        }

        [Serializable]
        private sealed class PrefabPreviewerConfig
        {
            public int uiLanguage = (int)UiLanguage.Chinese;
            public int viewAxis = (int)ViewAxis.Z;
            public bool gridVisible = true;
            public bool lightingEnabled = true;
        }

        private void UpdateViewButtonsState()
        {
            void SetButtonState(Button button, ViewAxis axis)
            {
                if (button == null)
                {
                    return;
                }

                if (_currentViewAxis == axis)
                {
                    button.AddToClassList("view-button--active");
                }
                else
                {
                    button.RemoveFromClassList("view-button--active");
                }
            }

            SetButtonState(_viewXButton, ViewAxis.X);
            SetButtonState(_viewYButton, ViewAxis.Y);
            SetButtonState(_viewZButton, ViewAxis.Z);
        }

        private void PersistViewAxis()
        {
            if (_config == null)
            {
                return;
            }

            _config.viewAxis = (int)_currentViewAxis;
            SaveConfig();
        }

        private void LoadPersistedViewAxis()
        {
            if (_config == null)
            {
                _currentViewAxis = ViewAxis.Z;
                return;
            }

            _currentViewAxis = (ViewAxis)_config.viewAxis;
        }

        private void ToggleGridVisibility(bool visible)
        {
            _gridVisible = visible;
            _gridToggle?.SetValueWithoutNotify(visible);
            if (_config != null)
            {
                _config.gridVisible = visible;
                SaveConfig();
            }
            UpdateGridState();
        }

        private void ToggleLighting(bool enabled)
        {
            _lightingEnabled = enabled;
            _lightingToggle?.SetValueWithoutNotify(enabled);
            if (_config != null)
            {
                _config.lightingEnabled = enabled;
                SaveConfig();
            }
            ApplyLightingState();
            _previewSurface?.MarkDirtyRepaint();
        }

        private void LoadPersistedGridVisibility()
        {
            _gridVisible = _config?.gridVisible ?? true;
        }

        private void LoadPersistedLightingEnabled()
        {
            _lightingEnabled = _config?.lightingEnabled ?? true;
        }

        private void ApplyLightingState()
        {
            if (_previewUtility == null)
            {
                return;
            }

            var lights = _previewUtility.lights;
            if (lights == null)
            {
                return;
            }

            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null)
                {
                    continue;
                }

                lights[i].enabled = _lightingEnabled;
            }
        }

        private void UpdateGridState()
        {
            if (_gridObject != null)
            {
                _gridObject.SetActive(false);
            }

            if (!_gridVisible || _displayMode != PreviewDisplayMode.PrefabScene || _previewUtility == null)
            {
                return;
            }

            EnsureGridResources();
            if (_gridObject == null)
            {
                return;
            }

            var extent = Mathf.Max(_contentBounds.extents.magnitude, 1f);
            var spacing = Mathf.Clamp(extent / 8f, 0.1f, 2f);
            UpdateGridMesh(extent, spacing);

            var position = _contentBounds.center;
            position.y = _contentBounds.min.y;
            _gridObject.transform.position = position;
            _gridObject.transform.rotation = Quaternion.identity;
            _gridObject.transform.localScale = Vector3.one;
            _gridObject.SetActive(true);
        }

        private void EnsureGridResources()
        {
            if (_gridMesh == null)
            {
                _gridMesh = new Mesh
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "PrefabPreviewGridMesh"
                };
            }

            if (_gridMaterial == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                _gridMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _gridMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                _gridMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                _gridMaterial.SetInt("_Cull", (int)CullMode.Off);
                _gridMaterial.SetInt("_ZWrite", 0);
                _gridMaterial.color = new Color(1f, 1f, 1f, 0.35f);
            }

            if (_gridObject == null)
            {
                _gridObject = EditorUtility.CreateGameObjectWithHideFlags(
                    "_PrefabPreviewGrid",
                    HideFlags.HideAndDontSave,
                    typeof(MeshFilter),
                    typeof(MeshRenderer));

                var renderer = _gridObject.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.allowOcclusionWhenDynamic = false;
                renderer.sharedMaterial = _gridMaterial;
            }

            if (_previewUtility != null && _gridObject.scene != _previewUtility.camera.scene)
            {
                SceneManager.MoveGameObjectToScene(_gridObject, _previewUtility.camera.scene);
            }

            var filter = _gridObject.GetComponent<MeshFilter>();
            filter.sharedMesh = _gridMesh;
        }

        private void UpdateGridMesh(float halfExtent, float spacing)
        {
            if (_gridMesh == null)
            {
                return;
            }

            var extent = Mathf.Ceil(halfExtent / spacing) * spacing;
            var lineCount = Mathf.Clamp(Mathf.CeilToInt(extent / spacing), 1, 200);

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();
            var color = new Color(1f, 1f, 1f, 0.25f);

            void AddLine(Vector3 a, Vector3 b)
            {
                var index = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                colors.Add(color);
                colors.Add(color);
                indices.Add(index);
                indices.Add(index + 1);
            }

            for (int i = -lineCount; i <= lineCount; i++)
            {
                var offset = i * spacing;
                AddLine(new Vector3(-extent, 0f, offset), new Vector3(extent, 0f, offset));
                AddLine(new Vector3(offset, 0f, -extent), new Vector3(offset, 0f, extent));
            }

            _gridMesh.Clear();
            _gridMesh.SetVertices(vertices);
            _gridMesh.SetColors(colors);
            _gridMesh.SetIndices(indices, MeshTopology.Lines, 0);
        }

        private void DisposeGridResources()
        {
            if (_gridMesh != null)
            {
                DestroyImmediate(_gridMesh);
                _gridMesh = null;
            }

            if (_gridMaterial != null)
            {
                DestroyImmediate(_gridMaterial);
                _gridMaterial = null;
            }

            if (_gridObject != null)
            {
                DestroyImmediate(_gridObject);
                _gridObject = null;
            }
        }

        private enum PreviewDisplayMode
        {
            None,
            PrefabScene,
            Texture,
            AssetPreview
        }

        private enum ViewAxis
        {
            X,
            Y,
            Z
        }

        private VisualElement _previewHost;
        private PreviewSurfaceElement _previewSurface;
        private Label _selectionLabel;
        private Label _statusLabel;
        private Toggle _autoRotateToggle;
        private Toggle _gridToggle;
        private Toggle _lightingToggle;
        private Button _playButton;
        private Button _restartButton;
        private Button _frameButton;
        private Button _resetButton;
        private Button _refreshButton;
        private Button _settingsButton;
        private Button _viewXButton;
        private Button _viewYButton;
        private Button _viewZButton;
        private VisualElement _settingsContainer;
        private VisualElement _settingsWindow;
        private Label _aboutVersionLabel;
        private Label _aboutAuthorLabel;
        private Button _aboutAuthorLinkButton;
        private Button _aboutDocumentLinkButton;
        private Label _aboutLanguageLabel;
        private DropdownField _aboutLanguageDropdown;
        private Label _configTitleLabel;
        private Label _configPathLabel;
        private Label _toolsLabel;
        private Label _toolIntroductionLabel;
        private Vector2 _previewSize;
        private bool _isDragging;
        private bool _isPanning;
        private Vector2 _lastPointerPos;
        private Vector2 _lastPanPointerPos;

        private const string AboutVersion = "1.0.0";
        private const string AboutAuthor = "T·L";
        private const string AboutAuthorLink = "https://jxaytl13.github.io";
        private const string AboutDocumentLink = "https://f9p2icbxkc.feishu.cn/wiki/space/7448180894793728019?ccm_open_type=lark_wiki_spaceLink&open_tab_from=wiki_home";
        private const string LanguagePrefsKey = "PrefabPreviewer_UiLanguage";
        private UiLanguage _uiLanguage = UiLanguage.Chinese;

        private const string ConfigDirectoryPath = "Library/ViewportX";
        private const string ConfigFileName = "ViewportXConfig.json";
        private const string LegacyConfigDirectoryPath = "Library/PrefabPreviewer";
        private const string LegacyConfigFileName = "PrefabPreviewerConfig.json";
        private PrefabPreviewerConfig _config;

        private static string GetConfigFilePath()
        {
            return Path.Combine(ConfigDirectoryPath, ConfigFileName);
        }

        private static string GetLegacyConfigFilePath()
        {
            return Path.Combine(LegacyConfigDirectoryPath, LegacyConfigFileName);
        }

        private void LoadOrCreateConfig()
        {
            var path = GetConfigFilePath();
            var legacyPath = GetLegacyConfigFilePath();
            try
            {
                if (!Directory.Exists(ConfigDirectoryPath))
                {
                    Directory.CreateDirectory(ConfigDirectoryPath);
                }

                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var loaded = JsonUtility.FromJson<PrefabPreviewerConfig>(json);
                    _config = loaded ?? new PrefabPreviewerConfig();
                    return;
                }

                if (File.Exists(legacyPath))
                {
                    var json = File.ReadAllText(legacyPath);
                    var loaded = JsonUtility.FromJson<PrefabPreviewerConfig>(json);
                    _config = loaded ?? new PrefabPreviewerConfig();
                    SaveConfig();
                    return;
                }

                _config = new PrefabPreviewerConfig
                {
                    uiLanguage = EditorPrefs.GetInt(LanguagePrefsKey, (int)UiLanguage.Chinese),
                    gridVisible = EditorPrefs.GetInt(GridPrefsKey, 1) == 1,
                    lightingEnabled = EditorPrefs.GetInt(LightingPrefsKey, 1) == 1,
                    viewAxis = EditorPrefs.HasKey(ViewAxisPrefsKey) ? EditorPrefs.GetInt(ViewAxisPrefsKey) : (int)ViewAxis.Z
                };
                SaveConfig();
            }
            catch
            {
                _config = new PrefabPreviewerConfig();
            }
        }

        private void SaveConfig()
        {
            if (_config == null)
            {
                return;
            }

            var path = GetConfigFilePath();
            try
            {
                if (!Directory.Exists(ConfigDirectoryPath))
                {
                    Directory.CreateDirectory(ConfigDirectoryPath);
                }

                var json = JsonUtility.ToJson(_config, true);
                File.WriteAllText(path, json);
            }
            catch
            {
            }
        }

        private PreviewRenderUtility _previewUtility;
        private GameObject _previewInstance;
        private GameObject _uiCanvasRoot;
        private readonly List<ParticleSystem> _particleSystems = new();
        private PreviewContentType _contentType = PreviewContentType.None;
        private UnityEngine.Object _currentAsset;
        private Bounds _contentBounds;

        private Vector2 _orbitAngles = new(15f, -120f);
        private float _distance = 5f;
        private bool _autoRotate;
        private bool _particlePlaying = true;
        private double _lastUpdateTime;
        private PreviewDisplayMode _displayMode = PreviewDisplayMode.None;
        private Texture _texturePreview;
        private Rect _textureUv;
        private bool _textureHasCustomUv;
        private Texture _assetPreviewTexture;
        private UnityEngine.Object _assetPreviewSource;
        private Vector3 _panOffset = Vector3.zero;
        private ViewAxis _currentViewAxis = ViewAxis.Z;
        private const string ViewAxisPrefsKey = "PrefabPreviewer_ViewAxis";
        private const string GridPrefsKey = "PrefabPreviewer_ShowGrid";
        private const string LightingPrefsKey = "PrefabPreviewer_Lighting";
        private bool _gridVisible = true;
        private bool _lightingEnabled = true;
        private GameObject _gridObject;
        private Mesh _gridMesh;
        private Material _gridMaterial;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<ViewportXWindow>();
            window.titleContent = new GUIContent("ViewportX");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            LoadOrCreateConfig();
            LoadPersistedLightingEnabled();
            CreatePreviewUtility();
            LoadPersistedGridVisibility();
            LoadPersistedLanguage();
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += OnEditorUpdate;
            _lastUpdateTime = EditorApplication.timeSinceStartup;
        }

        private void LoadPersistedLanguage()
        {
            if (_config == null)
            {
                _uiLanguage = UiLanguage.Chinese;
                return;
            }

            _uiLanguage = (UiLanguage)_config.uiLanguage;
        }

        private void PersistLanguage()
        {
            if (_config == null)
            {
                return;
            }

            _config.uiLanguage = (int)_uiLanguage;
            SaveConfig();
        }

        private void ApplyAboutLanguage()
        {
            var configFilePath = GetConfigFilePath();

            if (_uiLanguage == UiLanguage.Chinese)
            {
                if (_aboutVersionLabel != null) _aboutVersionLabel.text = $"版本: {AboutVersion}";
                if (_aboutAuthorLabel != null) _aboutAuthorLabel.text = $"作者: {AboutAuthor}";
                if (_aboutLanguageLabel != null) _aboutLanguageLabel.text = "语言:";
                if (_aboutAuthorLinkButton != null) _aboutAuthorLinkButton.text = "访问作者主页";
                if (_aboutDocumentLinkButton != null) _aboutDocumentLinkButton.text = "文档";
                if (_configTitleLabel != null) _configTitleLabel.text = "配置";
                if (_configPathLabel != null) _configPathLabel.text = $"存储路径: {configFilePath}";
                if (_toolsLabel != null) _toolsLabel.text = "工具说明";
                if (_toolIntroductionLabel != null) _toolIntroductionLabel.text = "ViewportX：用于在编辑器中预览 Prefab/模型/粒子/UGUI。支持旋转、缩放、平移、视图切换、网格与灯光等。";
            }
            else
            {
                if (_aboutVersionLabel != null) _aboutVersionLabel.text = $"Version: {AboutVersion}";
                if (_aboutAuthorLabel != null) _aboutAuthorLabel.text = $"Author: {AboutAuthor}";
                if (_aboutLanguageLabel != null) _aboutLanguageLabel.text = "Language:";
                if (_aboutAuthorLinkButton != null) _aboutAuthorLinkButton.text = "Visit Author's Homepage";
                if (_aboutDocumentLinkButton != null) _aboutDocumentLinkButton.text = "Documentation";
                if (_configTitleLabel != null) _configTitleLabel.text = "Configuration";
                if (_configPathLabel != null) _configPathLabel.text = $"Storage: {configFilePath}";
                if (_toolsLabel != null) _toolsLabel.text = "About";
                if (_toolIntroductionLabel != null) _toolIntroductionLabel.text = "ViewportX is a prefab preview tool for viewing Prefabs/models/particles/UGUI in the Editor. Supports orbit, zoom, pan, view axes, grid and lighting.";
            }

            if (_aboutLanguageDropdown != null)
            {
                _aboutLanguageDropdown.SetValueWithoutNotify(_uiLanguage == UiLanguage.Chinese ? "中文" : "English");
            }
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= OnEditorUpdate;
            CleanupPreview();
            DisposePreviewUtility();
            DisposeGridResources();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();

            var uxmlPath = ResolveAssetPath(UxmlGuid, "ViewportXWindow.uxml", "VisualTreeAsset");
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (tree == null)
            {
                rootVisualElement.Add(new Label($"找不到 UXML：{uxmlPath}"));
                return;
            }

            tree.CloneTree(rootVisualElement);
            var ussPath = ResolveAssetPath(UssGuid, "ViewportXWindow.uss", "StyleSheet");
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            if (sheet != null)
            {
                rootVisualElement.styleSheets.Add(sheet);
            }

            _previewHost = rootVisualElement.Q<VisualElement>("preview-image");
            _previewHost?.Clear();
            _previewSurface = new PreviewSurfaceElement();
            _previewSurface.AddToClassList("preview-surface");
            if (_previewHost != null)
            {
                _previewHost.focusable = true;
                _previewHost.tabIndex = 0;
                _previewHost.pickingMode = PickingMode.Position;
                _previewHost.Add(_previewSurface);
            }
            _previewSurface.drawHandler = DrawPreview;

            _selectionLabel = rootVisualElement.Q<Label>("selection-label");
            _statusLabel = rootVisualElement.Q<Label>("status-label");
            _autoRotateToggle = rootVisualElement.Q<Toggle>("tgl-auto-rotate");
            _gridToggle = rootVisualElement.Q<Toggle>("tgl-grid");
            _lightingToggle = rootVisualElement.Q<Toggle>("tgl-lighting");
            _playButton = rootVisualElement.Q<Button>("btn-play");
            _restartButton = rootVisualElement.Q<Button>("btn-restart");
            _frameButton = rootVisualElement.Q<Button>("btn-frame");
            _resetButton = rootVisualElement.Q<Button>("btn-reset");
            _refreshButton = rootVisualElement.Q<Button>("btn-refresh");
            _settingsButton = rootVisualElement.Q<Button>("btn-settings");
            _viewXButton = rootVisualElement.Q<Button>("btn-view-x");
            _viewYButton = rootVisualElement.Q<Button>("btn-view-y");
            _viewZButton = rootVisualElement.Q<Button>("btn-view-z");
            _settingsContainer = rootVisualElement.Q<VisualElement>("settings-container");
            _settingsWindow = rootVisualElement.Q<VisualElement>("settings-window");
            _aboutVersionLabel = rootVisualElement.Q<Label>("version-label");
            _aboutAuthorLabel = rootVisualElement.Q<Label>("author-label");
            _aboutAuthorLinkButton = rootVisualElement.Q<Button>("author-link-button");
            _aboutDocumentLinkButton = rootVisualElement.Q<Button>("document-link-button");
            _aboutLanguageLabel = rootVisualElement.Q<Label>("language-label");
            _aboutLanguageDropdown = rootVisualElement.Q<DropdownField>("language-dropdown");
            _configTitleLabel = rootVisualElement.Q<Label>("config-title-label");
            _configPathLabel = rootVisualElement.Q<Label>("config-path-label");
            _toolsLabel = rootVisualElement.Q<Label>("tools-label");
            _toolIntroductionLabel = rootVisualElement.Q<Label>("tool-introduction-label");

            _refreshButton?.RegisterCallback<ClickEvent>(_ => RefreshSelection());
            _settingsButton?.RegisterCallback<ClickEvent>(_ => ShowSettings());
            _frameButton?.RegisterCallback<ClickEvent>(_ => FrameContent(true));
            _resetButton?.RegisterCallback<ClickEvent>(_ => ResetView());
            _restartButton?.RegisterCallback<ClickEvent>(_ => RestartParticles());
            _playButton?.RegisterCallback<ClickEvent>(_ => ToggleParticlePlay());
            _autoRotateToggle?.RegisterCallback<ChangeEvent<bool>>(evt => _autoRotate = evt.newValue);
            if (_gridToggle != null)
            {
                _gridToggle.value = _gridVisible;
                _gridToggle.RegisterValueChangedCallback(evt => ToggleGridVisibility(evt.newValue));
            }
            if (_lightingToggle != null)
            {
                _lightingToggle.value = _lightingEnabled;
                _lightingToggle.RegisterValueChangedCallback(evt => ToggleLighting(evt.newValue));
            }

            if (_aboutAuthorLinkButton != null)
            {
                _aboutAuthorLinkButton.clicked += () => Application.OpenURL(AboutAuthorLink);
            }
            if (_aboutDocumentLinkButton != null)
            {
                _aboutDocumentLinkButton.clicked += () => Application.OpenURL(AboutDocumentLink);
            }

            if (_aboutLanguageDropdown != null)
            {
                _aboutLanguageDropdown.choices = new List<string> { "English", "中文" };
                _aboutLanguageDropdown.RegisterValueChangedCallback(evt =>
                {
                    _uiLanguage = evt.newValue == "中文" ? UiLanguage.Chinese : UiLanguage.English;
                    PersistLanguage();
                    ApplyAboutLanguage();
                });
            }

            ApplyAboutLanguage();

            if (_settingsContainer != null)
            {
                _settingsContainer.RegisterCallback<MouseDownEvent>(OnSettingsContainerClick);
            }
            _viewXButton?.RegisterCallback<ClickEvent>(_ => SnapViewToAxis(ViewAxis.X));
            _viewYButton?.RegisterCallback<ClickEvent>(_ => SnapViewToAxis(ViewAxis.Y));
            _viewZButton?.RegisterCallback<ClickEvent>(_ => SnapViewToAxis(ViewAxis.Z));

            RegisterPointerEvents();

            _previewHost?.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                _previewSize = evt.newRect.size;
            });

            UpdateSelectionLabel();
            LoadPersistedViewAxis();
            UpdateGridState();
            UpdateViewButtonsState();
            RefreshSelection();
        }

        private void RegisterPointerEvents()
        {
            if (_previewHost == null)
            {
                return;
            }

            _previewHost.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (_displayMode != PreviewDisplayMode.PrefabScene)
                {
                    return;
                }

                if (evt.button == 0)
                {
                    _isDragging = true;
                    _lastPointerPos = evt.position;
                    _previewHost.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                }
                else if (evt.button == 2)
                {
                    _isPanning = true;
                    _lastPanPointerPos = evt.position;
                    _previewHost.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            _previewHost.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_isDragging)
                {
                    var currentPos = (Vector2)evt.position;
                    var delta = currentPos - _lastPointerPos;
                    _lastPointerPos = currentPos;
                    Orbit(delta);
                    evt.StopPropagation();
                }
                else if (_isPanning)
                {
                    var currentPos = (Vector2)evt.position;
                    var delta = currentPos - _lastPanPointerPos;
                    _lastPanPointerPos = currentPos;
                    Pan(delta);
                    evt.StopPropagation();
                }
            });

            _previewHost.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_isDragging && evt.button == 0)
                {
                    _isDragging = false;
                    _previewHost.ReleasePointer(evt.pointerId);
                    evt.StopPropagation();
                }
                else if (_isPanning && evt.button == 2)
                {
                    _isPanning = false;
                    _previewHost.ReleasePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            _previewHost.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _isDragging = false;
                _isPanning = false;
            });

            _previewHost.RegisterCallback<WheelEvent>(evt =>
            {
                if (_displayMode != PreviewDisplayMode.PrefabScene)
                {
                    return;
                }

                var delta = Mathf.Clamp(evt.delta.y * 0.08f, -0.25f, 0.25f);
                if (Mathf.Approximately(delta, 0f))
                {
                    delta = evt.delta.y > 0f ? 0.15f : -0.15f;
                }

                Zoom(delta);
                evt.StopPropagation();
            });

            _previewHost.RegisterCallback<KeyDownEvent>(evt =>
            {
                switch (evt.keyCode)
                {
                    case KeyCode.F:
                        FrameContent(true);
                        evt.StopPropagation();
                        break;
                    case KeyCode.A:
                        ResetView();
                        evt.StopPropagation();
                        break;
                }
            });
        }

        private void ShowSettings()
        {
            if (_settingsContainer == null)
            {
                return;
            }

            _settingsContainer.style.display = DisplayStyle.Flex;
            _settingsContainer.Focus();
        }

        private void OnSettingsContainerClick(MouseDownEvent evt)
        {
            if (_settingsContainer == null || _settingsWindow == null)
            {
                return;
            }

            var localMousePosition = _settingsWindow.WorldToLocal(evt.mousePosition);
            var windowRect = _settingsWindow.contentRect;
            if (!windowRect.Contains(localMousePosition))
            {
                _settingsContainer.style.display = DisplayStyle.None;
                evt.StopPropagation();
            }
        }

        private void CreatePreviewUtility()
        {
            if (_previewUtility != null)
            {
                return;
            }

            _previewUtility = new PreviewRenderUtility(true);
            _previewUtility.camera.nearClipPlane = 0.01f;
            _previewUtility.camera.farClipPlane = 500f;
            _previewUtility.camera.fieldOfView = PerspectiveFieldOfView;
            _previewUtility.camera.clearFlags = CameraClearFlags.Color;
            _previewUtility.camera.backgroundColor = new Color(0.13f, 0.13f, 0.13f, 1f);
            _previewUtility.camera.transform.position = new Vector3(0, 0, -5f);
            _previewUtility.camera.transform.rotation = Quaternion.identity;
            _previewUtility.lights[0].intensity = 1.3f;
            _previewUtility.lights[0].transform.rotation = Quaternion.Euler(50f, 50f, 0f);
            _previewUtility.lights[1].intensity = 0.8f;
            _previewUtility.lights[1].transform.rotation = Quaternion.Euler(340f, 218f, 177f);
            ApplyLightingState();
        }

        private void DisposePreviewUtility()
        {
            _previewUtility?.Cleanup();
            _previewUtility = null;
        }

        private void RefreshSelection()
        {
            var asset = Selection.activeObject;
            LoadAsset(asset);
        }

        private void OnSelectionChanged()
        {
            UpdateSelectionLabel();
            if (!hasFocus)
            {
                return;
            }

            RefreshSelection();
        }

        private void LoadAsset(UnityEngine.Object asset)
        {
            CleanupPreview();
            ResetPreviewTextures();
            _currentAsset = asset;
            _particlePlaying = true;
            _displayMode = PreviewDisplayMode.None;
            _contentType = PreviewContentType.None;
            _panOffset = Vector3.zero;

            if (asset == null)
            {
                SetStatusText("状态：未选中资产");
                ToggleParticleControlsVisibility(forceDisable: true);
                UpdateControlStates();
                _previewSurface?.MarkDirtyRepaint();
                return;
            }

            if (asset is GameObject prefab)
            {
                if (_previewUtility == null)
                {
                    CreatePreviewUtility();
                }

                if (_previewUtility == null)
                {
                    SetStatusText("状态：无法初始化预览渲染器");
                    ToggleParticleControlsVisibility(forceDisable: true);
                    UpdateControlStates();
                    return;
                }

                _previewInstance = _previewUtility.InstantiatePrefabInScene(prefab);
                if (_previewInstance == null)
                {
                    SetStatusText("状态：无法实例化预制体");
                    ToggleParticleControlsVisibility(forceDisable: true);
                    UpdateControlStates();
                    _previewSurface?.MarkDirtyRepaint();
                    return;
                }

                _previewInstance.name = prefab.name;
                _contentType = DetermineContentType(_previewInstance);
                _displayMode = PreviewDisplayMode.PrefabScene;

                switch (_contentType)
                {
                    case PreviewContentType.UGUI:
                        SetupUiPreview();
                        break;
                    default:
                        SetupDefaultPreview();
                        break;
                }

                CacheParticleSystems();
                if (_contentType == PreviewContentType.Particle)
                {
                    WarmUpParticlesForBounds();
                }

                FrameContent(true);
                SetOrbitAnglesForAxis(_currentViewAxis);
                RestartParticles();
                UpdateStatusText();
                ToggleParticleControlsVisibility();
                UpdateControlStates();
                _previewSurface?.MarkDirtyRepaint();
                UpdateGridState();
                return;
            }

            ToggleParticleControlsVisibility(forceDisable: true);

            switch (asset)
            {
                case Sprite sprite:
                    SetupSpritePreview(sprite);
                    break;
                case Texture texture:
                    SetupTexturePreview(texture, "纹理");
                    break;
                case Material material:
                    SetupAssetPreview(material, "材质");
                    break;
                case Mesh mesh:
                    SetupAssetPreview(mesh, "网格");
                    break;
                default:
                    SetupAssetPreview(asset, asset.GetType().Name);
                    break;
            }

            UpdateControlStates();
            _previewSurface?.MarkDirtyRepaint();
        }

        private PreviewContentType DetermineContentType(GameObject root)
        {
            if (root == null)
            {
                return PreviewContentType.None;
            }

            if (root.GetComponentInChildren<Canvas>(true) != null || root.GetComponentInChildren<Graphic>(true) != null)
            {
                return PreviewContentType.UGUI;
            }

            if (root.GetComponentInChildren<ParticleSystem>(true) != null)
            {
                return PreviewContentType.Particle;
            }

            return PreviewContentType.Model;
        }

        private void SetupDefaultPreview()
        {
            _contentBounds = CalculateRendererBounds(_previewInstance);
            _uiCanvasRoot = null;
            _previewUtility.camera.orthographic = false;
        }

        private void SetupUiPreview()
        {
            CleanupUiRoot();

            var canvasGo = EditorUtility.CreateGameObjectWithHideFlags(
                "_UIPreviewCanvas",
                HideFlags.HideAndDontSave,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            if (_previewUtility != null)
            {
                SceneManager.MoveGameObjectToScene(canvasGo, _previewUtility.camera.scene);
            }
            _uiCanvasRoot = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _previewUtility.camera;
            canvas.planeDistance = 1f;

            var rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1920f, 1080f);
            rect.localScale = Vector3.one * 0.0025f;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (_previewInstance != null)
            {
                _previewInstance.transform.SetParent(canvas.transform, false);
            }

            _contentBounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.1f));
            _previewUtility.camera.orthographic = true;
            _previewUtility.camera.orthographicSize = 1.2f;
            _previewUtility.camera.transform.position = new Vector3(0f, 0f, -4f);
            _previewUtility.camera.transform.rotation = Quaternion.identity;
            UpdateCameraClipPlanes();
        }

        private void CleanupUiRoot()
        {
            DestroyPreviewObject(ref _uiCanvasRoot);
        }

        private void CleanupPreview()
        {
            DestroyPreviewObject(ref _previewInstance);
            CleanupUiRoot();
            _particleSystems.Clear();
            _contentType = PreviewContentType.None;
            _displayMode = PreviewDisplayMode.None;
            _particlePlaying = true;
            UpdateGridState();
        }

        private void OnEditorUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            var delta = (float)(now - _lastUpdateTime);
            _lastUpdateTime = now;

            switch (_displayMode)
            {
                case PreviewDisplayMode.PrefabScene when _previewUtility != null:
                    if (_autoRotate && _contentType != PreviewContentType.UGUI)
                    {
                        _orbitAngles.y += delta * 15f;
                    }

                    if (_contentType == PreviewContentType.Particle && _particlePlaying)
                    {
                        foreach (var ps in _particleSystems)
                        {
                            ps.Simulate(delta, true, false, true);
                        }
                    }

                    _previewSurface?.MarkDirtyRepaint();
                    break;
                case PreviewDisplayMode.AssetPreview:
                    if (UpdateAssetPreviewTexture())
                    {
                        _previewSurface?.MarkDirtyRepaint();
                    }
                    break;
            }
        }

        private void DrawPreview(Rect rect)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            switch (_displayMode)
            {
                case PreviewDisplayMode.PrefabScene:
                    DrawPrefabScene(rect);
                    break;
                case PreviewDisplayMode.Texture:
                    DrawTexturePreview(rect);
                    break;
                case PreviewDisplayMode.AssetPreview:
                    DrawAssetPreview(rect);
                    break;
                default:
                    DrawInfoMessage(rect, "请选择一个可预览的资产");
                    break;
            }
        }

        private void ConfigureCamera()
        {
            if (_contentType == PreviewContentType.UGUI)
            {
                _previewUtility.camera.orthographic = true;
                _previewUtility.camera.orthographicSize = 1.2f;
                _previewUtility.camera.transform.position = new Vector3(0f, 0f, -4f) + _panOffset;
                _previewUtility.camera.transform.rotation = Quaternion.identity;
                return;
            }

            var target = _contentBounds.center + _panOffset;
            var rotation = Quaternion.Euler(_orbitAngles.x, _orbitAngles.y, 0f);
            var direction = rotation * Vector3.forward;
            var distance = Mathf.Max(_distance, 0.1f);
            var position = target - direction * distance;

            _previewUtility.camera.orthographic = false;
            _previewUtility.camera.fieldOfView = PerspectiveFieldOfView;
            _previewUtility.camera.transform.position = position;
            _previewUtility.camera.transform.LookAt(target);
        }

        private void FrameContent(bool recenter = false)
        {
            if (_previewInstance == null)
            {
                return;
            }

            _contentBounds = _contentType == PreviewContentType.UGUI
                ? new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.1f))
                : CalculateRendererBounds(_previewInstance);

            if (recenter)
            {
                _panOffset = Vector3.zero;
            }

            var radius = Mathf.Max(_contentBounds.extents.magnitude, 0.001f);
            var cam = _previewUtility?.camera;
            if (cam != null)
            {
                var verticalFovRad = Mathf.Max(0.1f, cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
                var aspect = _previewSize.y > 0f ? _previewSize.x / _previewSize.y : 1f;
                var horizontalFovRad = Mathf.Max(0.1f, Camera.VerticalToHorizontalFieldOfView(cam.fieldOfView, aspect) * Mathf.Deg2Rad * 0.5f);

                var distanceVertical = radius / Mathf.Sin(verticalFovRad);
                var distanceHorizontal = radius / Mathf.Sin(horizontalFovRad);
                var targetDistance = Mathf.Max(distanceVertical, distanceHorizontal) * 1.05f;
                _distance = Mathf.Clamp(targetDistance, 0.05f, 20000f);
            }
            else
            {
                _distance = Mathf.Clamp(radius * 2.2f, 0.05f, 20000f);
            }
            UpdateCameraClipPlanes();
            _previewSurface?.MarkDirtyRepaint();
        }

        private void ResetView()
        {
            _orbitAngles = new Vector2(15f, -120f);
            FrameContent(true);
        }

        private void Orbit(Vector2 delta)
        {
            if (_displayMode != PreviewDisplayMode.PrefabScene || _contentType == PreviewContentType.UGUI)
            {
                return;
            }

            _orbitAngles.x = Mathf.Clamp(_orbitAngles.x + delta.y * 0.2f, -80f, 80f);
            _orbitAngles.y += delta.x * 0.2f;
            _previewSurface?.MarkDirtyRepaint();
        }

        private void Zoom(float delta)
        {
            if (_displayMode != PreviewDisplayMode.PrefabScene)
            {
                return;
            }

            if (_contentType == PreviewContentType.UGUI)
            {
                var scale = _uiCanvasRoot != null ? _uiCanvasRoot.transform.localScale : Vector3.one * 0.0025f;
                var factor = 1f + delta;
                factor = Mathf.Clamp(factor, 0.5f, 1.5f);
                scale *= factor;
                var clamped = Mathf.Clamp(scale.x, 0.0005f, 0.02f);
                if (_uiCanvasRoot != null)
                {
                    _uiCanvasRoot.transform.localScale = Vector3.one * clamped;
                }
            }
            else
            {
                _distance *= 1f + delta;
                _distance = Mathf.Clamp(_distance, 0.05f, 20000f);
            }

            _previewSurface?.MarkDirtyRepaint();
            UpdateCameraClipPlanes();
        }

        private void CacheParticleSystems()
        {
            _particleSystems.Clear();
            if (_previewInstance == null)
            {
                return;
            }

            _previewInstance.GetComponentsInChildren(_particleSystems);
        }

        private void RestartParticles()
        {
            if (_contentType != PreviewContentType.Particle)
            {
                return;
            }

            foreach (var ps in _particleSystems)
            {
                ps.Simulate(0f, true, true, true);
                ps.Play(true);
            }

            _particlePlaying = true;
            UpdatePlayButtonLabel();
        }

        private void WarmUpParticlesForBounds(float totalTime = 0.8f, int steps = 6)
        {
            if (_particleSystems.Count == 0)
            {
                return;
            }

            foreach (var ps in _particleSystems)
            {
                ps.Simulate(0f, true, true, true);
                ps.Play(true);
            }

            var step = steps > 0 ? Mathf.Max(totalTime / steps, 0.01f) : totalTime;
            if (step <= 0f)
            {
                step = 0.05f;
            }

            for (var i = 0; i < steps; i++)
            {
                foreach (var ps in _particleSystems)
                {
                    ps.Simulate(step, true, false, true);
                }
            }

            foreach (var ps in _particleSystems)
            {
                ps.Pause(true);
            }
        }

        private void ToggleParticlePlay()
        {
            if (_contentType != PreviewContentType.Particle)
            {
                return;
            }

            _particlePlaying = !_particlePlaying;
            if (!_particlePlaying)
            {
                foreach (var ps in _particleSystems)
                {
                    ps.Pause(true);
                }
            }
            else
            {
                foreach (var ps in _particleSystems)
                {
                    ps.Play(true);
                }
            }

            UpdatePlayButtonLabel();
        }

        private void UpdatePlayButtonLabel()
        {
            if (_playButton == null)
            {
                return;
            }

            _playButton.text = _particlePlaying ? "暂停" : "播放";
        }

        private Bounds CalculateRendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(go.transform.position, Vector3.one);
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return new Bounds(bounds.center, bounds.size);
        }

        private void UpdateSelectionLabel()
        {
            if (_selectionLabel == null)
            {
                return;
            }

            var obj = Selection.activeObject;
            _selectionLabel.text = obj == null ? "未选中任何资产" : $"选中：{obj.name}";
        }

        private void SnapViewToAxis(ViewAxis axis)
        {
            if (_displayMode != PreviewDisplayMode.PrefabScene || _previewUtility == null)
            {
                _currentViewAxis = axis;
                PersistViewAxis();
                UpdateViewButtonsState();
                return;
            }

            if (_autoRotateToggle != null)
            {
                _autoRotateToggle.value = false;
            }
            _autoRotate = false;

            _panOffset = Vector3.zero;
            SetOrbitAnglesForAxis(axis);

            _currentViewAxis = axis;
            PersistViewAxis();
            UpdateViewButtonsState();
            _previewSurface?.MarkDirtyRepaint();
        }

        private void SetOrbitAnglesForAxis(ViewAxis axis)
        {
            switch (axis)
            {
                case ViewAxis.X:
                    _orbitAngles = new Vector2(0f, -90f);
                    break;
                case ViewAxis.Y:
                    _orbitAngles = new Vector2(-90f, 0f);
                    break;
                case ViewAxis.Z:
                    _orbitAngles = new Vector2(0f, 0f);
                    break;
            }
        }

        private void Pan(Vector2 screenDelta)
        {
            if (_displayMode != PreviewDisplayMode.PrefabScene || _previewUtility == null || _previewSize.x <= 0f || _previewSize.y <= 0f)
            {
                return;
            }

            var cam = _previewUtility.camera;
            if (cam == null)
            {
                return;
            }

            if (_contentType == PreviewContentType.UGUI)
            {
                // 对于 UGUI，沿平面平移
                var factor = 0.0025f;
                _panOffset += new Vector3(-screenDelta.x * factor, screenDelta.y * factor, 0f);
            }
            else
            {
                var distance = Mathf.Max(_distance, 0.1f);
                var viewHeight = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance;
                if (cam.orthographic)
                {
                    viewHeight = cam.orthographicSize * 2f;
                }

                var viewWidth = viewHeight * cam.aspect;
                var right = cam.transform.right;
                var up = cam.transform.up;

                var offset = (-screenDelta.x / _previewSize.x) * viewWidth * right
                             + (screenDelta.y / _previewSize.y) * viewHeight * up;
                _panOffset += offset;
            }

            _previewSurface?.MarkDirtyRepaint();
        }

        private void DrawPrefabScene(Rect rect)
        {
            if (_previewUtility == null)
            {
                DrawInfoMessage(rect, "预览渲染器不可用");
                return;
            }

            if (_previewInstance == null)
            {
                DrawInfoMessage(rect, "请选择一个预制体");
                return;
            }

            ConfigureCamera();
            _previewUtility.BeginPreview(rect, GUIStyle.none);
            _previewUtility.camera.Render();
            var tex = _previewUtility.EndPreview();
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
        }

        private void DrawTexturePreview(Rect rect)
        {
            if (_texturePreview == null)
            {
                DrawInfoMessage(rect, "纹理不可用");
                return;
            }

            var uv = _textureHasCustomUv ? _textureUv : new Rect(0f, 0f, 1f, 1f);
            GUI.DrawTextureWithTexCoords(rect, _texturePreview, uv, true);
        }

        private void DrawAssetPreview(Rect rect)
        {
            if (_assetPreviewTexture == null)
            {
                DrawInfoMessage(rect, "正在生成预览...");
                return;
            }

            GUI.DrawTexture(rect, _assetPreviewTexture, ScaleMode.ScaleToFit, true);
        }

        private static void DrawInfoMessage(Rect rect, string message)
        {
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));
            EditorGUI.LabelField(rect, message, EditorStyles.centeredGreyMiniLabel);
        }

        private void SetupSpritePreview(Sprite sprite)
        {
            if (sprite == null)
            {
                SetupTexturePreview(null, "Sprite");
                return;
            }

            SetupTexturePreview(sprite.texture, "Sprite");
            if (sprite.texture != null)
            {
                var tex = sprite.texture;
                var texRect = sprite.textureRect;
                _textureUv = new Rect(
                    texRect.x / tex.width,
                    texRect.y / tex.height,
                    texRect.width / tex.width,
                    texRect.height / tex.height);
                _textureHasCustomUv = true;
            }
        }

        private void SetupTexturePreview(Texture texture, string typeName)
        {
            _displayMode = PreviewDisplayMode.Texture;
            _texturePreview = texture;
            _textureHasCustomUv = false;
            _textureUv = new Rect(0f, 0f, 1f, 1f);
            SetStatusText(texture == null ? $"状态：{typeName}不可用" : $"状态：{typeName}");
        }

        private void SetupAssetPreview(UnityEngine.Object target, string label)
        {
            _displayMode = PreviewDisplayMode.AssetPreview;
            _assetPreviewSource = target;
            _assetPreviewTexture = AssetPreview.GetAssetPreview(target) ?? AssetPreview.GetMiniThumbnail(target);
            SetStatusText($"状态：{label}");
        }

        private void ResetPreviewTextures()
        {
            _texturePreview = null;
            _textureHasCustomUv = false;
            _textureUv = new Rect(0f, 0f, 1f, 1f);
            _assetPreviewTexture = null;
            _assetPreviewSource = null;
        }

        private void SetStatusText(string text)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = text;
            }
        }

        private bool UpdateAssetPreviewTexture()
        {
            if (_assetPreviewSource == null)
            {
                return false;
            }

            var preview = AssetPreview.GetAssetPreview(_assetPreviewSource);
            if (preview == null)
            {
                preview = AssetPreview.GetMiniThumbnail(_assetPreviewSource);
            }

            if (preview == null)
            {
                return AssetPreview.IsLoadingAssetPreview(_assetPreviewSource.GetInstanceID());
            }

            if (_assetPreviewTexture == preview)
            {
                return false;
            }

            _assetPreviewTexture = preview;
            return true;
        }

        private void UpdateControlStates()
        {
            var prefabMode = _displayMode == PreviewDisplayMode.PrefabScene;
            _frameButton?.SetEnabled(prefabMode);
            _resetButton?.SetEnabled(prefabMode);
            var allowAutoRotate = prefabMode && _contentType != PreviewContentType.UGUI;
            _autoRotateToggle?.SetEnabled(allowAutoRotate);
            if (!allowAutoRotate && _autoRotateToggle != null)
            {
                _autoRotateToggle.value = false;
                _autoRotate = false;
            }
            var particleControls = prefabMode && _contentType == PreviewContentType.Particle;
            _restartButton?.SetEnabled(particleControls);
            _playButton?.SetEnabled(particleControls);
        }

        private void ToggleParticleControlsVisibility()
        {
            ToggleParticleControlsVisibility(false);
        }

        private void ToggleParticleControlsVisibility(bool forceDisable)
        {
            var show = !forceDisable && _displayMode == PreviewDisplayMode.PrefabScene && _contentType == PreviewContentType.Particle;
            _playButton?.SetEnabled(show);
            _restartButton?.SetEnabled(show);
        }

        private void UpdateStatusText()
        {
            if (_statusLabel == null)
            {
                return;
            }

            var status = _contentType switch
            {
                PreviewContentType.Model => "模型",
                PreviewContentType.Particle => "粒子",
                PreviewContentType.UGUI => "UGUI",
                _ => "-"
            };
            _statusLabel.text = $"状态：{status}";
        }

        private void OnInspectorUpdate()
        {
            _previewSurface?.MarkDirtyRepaint();
        }

        private void DestroyPreviewObject(ref GameObject go)
        {
            if (go == null)
            {
                return;
            }

            DestroyImmediate(go);
            go = null;
        }

        private void UpdateCameraClipPlanes()
        {
            if (_previewUtility == null)
            {
                return;
            }

            var cam = _previewUtility.camera;
            if (cam == null)
            {
                return;
            }

            var target = _contentBounds.center + _panOffset;
            var centerDistance = _contentType == PreviewContentType.UGUI
                ? Vector3.Distance(cam.transform.position, target)
                : Mathf.Max(_distance, 0.1f);
            var radius = Mathf.Max(_contentBounds.extents.magnitude, 0.01f);

            var paddingMultiplier = _contentType == PreviewContentType.Particle ? 2f : 1.25f;
            var padding = Mathf.Max(radius * paddingMultiplier, 0.1f);

            var nearPlane = Mathf.Max(0.01f, centerDistance - padding);
            var farPlane = Mathf.Max(nearPlane + 0.1f, centerDistance + padding);

            cam.nearClipPlane = nearPlane;
            cam.farClipPlane = farPlane;
        }

        private static string ResolveAssetPath(string guid, string fileName, string typeFilter)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            var guids = AssetDatabase.FindAssets($"{System.IO.Path.GetFileNameWithoutExtension(fileName)} t:{typeFilter}");
            foreach (var foundGuid in guids)
            {
                var candidate = AssetDatabase.GUIDToAssetPath(foundGuid);
                if (candidate.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return fileName;
        }

        private sealed class PreviewSurfaceElement : ImmediateModeElement
        {
            public Action<Rect> drawHandler;

            protected override void ImmediateRepaint()
            {
                drawHandler?.Invoke(contentRect);
            }
        }
    }
}
#endif
