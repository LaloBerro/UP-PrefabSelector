using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Object = UnityEngine.Object;

public class PrefabFolderViewer : EditorWindow
{
    private Vector2 _scrollPosition;
    private string _folderPath;
    private Dictionary<string, List<GameObject>> _prefabsByFolder = new Dictionary<string, List<GameObject>>();
    private Dictionary<string, bool> _hasToShowFolderValues = new Dictionary<string, bool>();
    private Dictionary<string, Texture2D> _previews = new Dictionary<string, Texture2D>();
    private int _totalPrefabs;
    private bool _hasToDisplayAsList;

    private bool _mustSetSelectedAsParent;
    private Transform _parentTransForm;
    private int _modeIndex;
    const string PrefabSelectorMode = "Selector Mode";
    const string PrefabReplacerMode = "Replacer Mode";
    private string[] _modeOptions = new string[] { PrefabSelectorMode, PrefabReplacerMode };

    private const int ThumbSize = 70;
    private const int Padding = 8;

    [MenuItem("Tools/Prefab Selector")]
    public static void OpenWindow()
    {
        var w = GetWindow<PrefabFolderViewer>("Prefab Selector");
        w.minSize = new Vector2(300, 200);
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.update += UpdatePreviews;
        
        RefreshIfSelectionIsFolder();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
        EditorApplication.update -= UpdatePreviews;
    }

    private void OnSelectionChanged()
    {
        if (_modeOptions[_modeIndex] == PrefabSelectorMode && _mustSetSelectedAsParent)
        {
            _parentTransForm = Selection.activeTransform;
        }
        
        RefreshIfSelectionIsFolder();
        Repaint();
    }

    private void RefreshIfSelectionIsFolder()
    {
        Object selectedGameObject = Selection.activeObject;
        bool hasSavedPath = PlayerPrefs.HasKey("PrefabFolder_FolderPath");
        if (selectedGameObject == null && !hasSavedPath)
            return;
        
        string path = "";
        if(selectedGameObject != null)
            path = AssetDatabase.GetAssetPath(selectedGameObject);
        else if(hasSavedPath)
            path = PlayerPrefs.GetString("PrefabFolder_FolderPath");
        
        if (string.IsNullOrEmpty(path)) 
            return;

        if (!AssetDatabase.IsValidFolder(path)) 
            return;
        
        _folderPath = path;
        LoadPrefabsFromFolder(path);
        
        PlayerPrefs.SetString("PrefabFolder_FolderPath", path);
    }

    private void LoadPrefabsFromFolder(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        
        _prefabsByFolder.Clear();
        _hasToShowFolderValues.Clear();
        _previews.Clear();
        _totalPrefabs = 0;
        
        string filter = "t:Prefab";
        string[] guids;
        
        guids = AssetDatabase.FindAssets(filter, new[] { path });
        
        if (guids == null || guids.Length == 0)
            return;
        
        foreach (var guid in guids)
        {
            AddPrefabByGUID(guid);
        }
    }

    private void AddPrefabByGUID(string guid)
    {
        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
        GameObject gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

        if (gameObject == null) 
            return;
            
        _totalPrefabs++;
                
        string assetFolderPath = System.IO.Path.GetDirectoryName(assetPath);
        if (_prefabsByFolder.ContainsKey(assetFolderPath))
            _prefabsByFolder[assetFolderPath].Add(gameObject);
        else
        {
            _prefabsByFolder.Add(assetFolderPath, new List<GameObject>() { gameObject });
            _hasToShowFolderValues.Add(assetFolderPath, true);
        }
    }

    private void UpdatePreviews()
    {
        bool hasToRepaint = false;
        foreach (var prefabKV in _prefabsByFolder)
        {
            List<GameObject> prefabs = prefabKV.Value;
            foreach (var prefab in prefabs)
            {
                string path = AssetDatabase.GetAssetPath(prefab);
                if (_previews.ContainsKey(path) && _previews[path] != null) 
                    continue;
            
                Texture2D previewTexture2D = AssetPreview.GetAssetPreview(prefab);
                if (previewTexture2D == null)
                    previewTexture2D = AssetPreview.GetMiniThumbnail(prefab);
                
                _previews[path] = previewTexture2D;
                hasToRepaint = true;
            }
        }
        
        if (hasToRepaint)
            Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        _modeIndex = EditorGUILayout.Popup(_modeIndex, _modeOptions);
        
        _hasToDisplayAsList = GUILayout.Toggle(_hasToDisplayAsList, "Display as List", EditorStyles.toolbarButton);
        
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
        {
            if (!string.IsNullOrEmpty(_folderPath))
                LoadPrefabsFromFolder(_folderPath);
        }
        
        if (GUILayout.Button("Expand all", EditorStyles.toolbarButton))
        {
            List<string> keys = new List<string>(_hasToShowFolderValues.Keys);
            foreach (var key in keys)
            {
                _hasToShowFolderValues[key] = true;
            }
        }
        
        if (GUILayout.Button("Hide all", EditorStyles.toolbarButton))
        {
            List<string> keys = new List<string>(_hasToShowFolderValues.Keys);
            foreach (var key in keys)
            {
                _hasToShowFolderValues[key] = false;
            }
        }
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (_modeOptions[_modeIndex] == PrefabSelectorMode)
        {
            EditorGUILayout.BeginVertical("Box");
            
            _mustSetSelectedAsParent = EditorGUILayout.Toggle("Parent is the selected object", _mustSetSelectedAsParent);
            if (_mustSetSelectedAsParent)
            {
                _parentTransForm = Selection.activeTransform;
            }
            
            _parentTransForm = (Transform)EditorGUILayout.ObjectField("Parent", _parentTransForm, typeof(Transform));
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
        }

        if (_prefabsByFolder.Count == 0)
        {
            EditorGUILayout.HelpBox("Select a folder in the Project window that contains prefabs.", MessageType.Info);
            return;
        }

        int columnCount = Mathf.Max(1, Mathf.FloorToInt((position.width - Padding) / (ThumbSize + Padding)));
        int rows = Mathf.CeilToInt((float)_totalPrefabs / columnCount);

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        foreach (var prefabsKV in _prefabsByFolder)
        {
            DisplayPrefabsByFolder(prefabsKV.Key, prefabsKV.Value, columnCount, rows);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DisplayPrefabsByFolder(string folderPath, List<GameObject> prefabs, int columnCount, int rows)
    { 
        bool hasToShowFoldout = _hasToShowFolderValues[folderPath];
        string foldoutName = folderPath.Substring("Assets/".Length);
        hasToShowFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(hasToShowFoldout, foldoutName);
        _hasToShowFolderValues[folderPath] = hasToShowFoldout;

        if (!hasToShowFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        if (!_hasToDisplayAsList)
        {
            EditorGUILayout.BeginVertical("Box");
        
            int index = 0;
            int totalPrefabs = prefabs.Count;
            for (int row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int column = 0; column < columnCount; column++)
                {
                    if (index >= totalPrefabs) 
                        break;
                
                    DrawPrefabWithPreview(prefabs[index]);
                    index++;
                }
                EditorGUILayout.EndHorizontal();
            
                if (index >= totalPrefabs) 
                    break;
            } 
        
            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.BeginVertical("Box");
            
            foreach (var prefab in prefabs)
            {
                DrawPrefab(prefab);
                EditorGUILayout.Space(2f);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawPrefabWithPreview(GameObject prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        _previews.TryGetValue(path, out Texture2D previewTexture2D);

        EditorGUILayout.BeginVertical(GUILayout.Width(ThumbSize));

        if (previewTexture2D == null)
        {
            if (GUILayout.Button(prefab.name, GUILayout.Width(ThumbSize), GUILayout.Height(ThumbSize)))
                SelectPrefab(prefab);
        }
        else
        {
            if (GUILayout.Button(previewTexture2D, GUILayout.Width(ThumbSize), GUILayout.Height(ThumbSize)))
                SelectPrefab(prefab);
        }

        GUIStyle style = new GUIStyle(EditorStyles.label);
        style.wordWrap = true;
        if (GUILayout.Button(prefab.name, style, GUILayout.Width(ThumbSize)))
            EditorGUIUtility.PingObject(prefab);

        EditorGUILayout.EndVertical();
    }

    private void SelectPrefab(GameObject prefab)
    {
        if (_modeOptions[_modeIndex] == PrefabSelectorMode)

            SelectPrefabOnSelectorMode(prefab);
        else
            SelectPrefabOnReplaceMode(prefab);
    }

    private void SelectPrefabOnSelectorMode(GameObject prefab)
    {
        if (_parentTransForm == null)
        {
            Debug.LogError("Parent is null. Please select one.");
            return;
        }
        
        GameObject gameObject = Instantiate(prefab, _parentTransForm);
        gameObject.name = prefab.name;
        
        Undo.RegisterCreatedObjectUndo(gameObject, "Create " + gameObject.name);
        
        Selection.activeGameObject = gameObject;
        
        EditorUtility.SetDirty(_parentTransForm);
    }
    
    private void SelectPrefabOnReplaceMode(GameObject prefab)
    {
        GameObject selectedGameObject = Selection.activeGameObject;
        if (selectedGameObject == null)
            return;

        try
        {
            PrefabReplacingSettings prefabReplacingSettings = new PrefabReplacingSettings();
            prefabReplacingSettings.changeRootNameToAssetName = false;
            
            PrefabUtility.ReplacePrefabAssetOfPrefabInstance(selectedGameObject, prefab, prefabReplacingSettings,InteractionMode.UserAction);
        }
        catch (Exception e)
        {
            ConvertToPrefabInstanceSettings convertToPrefabInstanceSettings = new ConvertToPrefabInstanceSettings();
            convertToPrefabInstanceSettings.componentsNotMatchedBecomesOverride = true;
            convertToPrefabInstanceSettings.gameObjectsNotMatchedBecomesOverride = true;
            convertToPrefabInstanceSettings.changeRootNameToAssetName = false;
            convertToPrefabInstanceSettings.recordPropertyOverridesOfMatches = true;
            
            PrefabUtility.ConvertToPrefabInstance(selectedGameObject, prefab, convertToPrefabInstanceSettings,InteractionMode.UserAction);;
        }
    }
    
    private void DrawPrefab(GameObject prefab)
    {
        EditorGUILayout.BeginHorizontal();

        GUIContent content = EditorGUIUtility.IconContent("d_ViewToolOrbit On");
        if (GUILayout.Button(content, GUILayout.Width(20), GUILayout.Height(20)))
            EditorGUIUtility.PingObject(prefab);
        
        GUIStyle leftButton = new GUIStyle(GUI.skin.button);
        leftButton.alignment = TextAnchor.MiddleLeft;
        if (GUILayout.Button(prefab.name, leftButton))
            SelectPrefab(prefab);
        
        EditorGUILayout.EndHorizontal();
    }
}