#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace qtLib.Editor
{
    public sealed class PersistentJsonTreeWindow : EditorWindow
    {
        // Layout V7: add buttons only on expanded containers, delete buttons for child values, responsive fields, no foldout arrows, Boolean checkbox, zebra rows.
        private sealed class JsonFileData
        {
            public string FullPath;
            public string RelativePath;
            public JToken Root;
            public long Length;
            public DateTime LastWriteTimeUtc;
            public bool Expanded;
            public bool IsDirty;
            public string SaveError;
        }


        private enum NewJsonValueType
        {
            String,
            Integer,
            Float,
            Boolean,
            Object,
            Array,
            Null
        }

        private sealed class AddJsonValuePopup : PopupWindowContent
        {
            private readonly PersistentJsonTreeWindow _owner;
            private readonly JsonFileData _file;
            private readonly JContainer _parent;
            private readonly string _parentPath;
            private readonly bool _needsPropertyName;

            private static readonly string[] ValueTypeLabels =
            {
                "String",
                "Integer",
                "Float",
                "Boolean",
                "Object",
                "Array",
                "Null"
            };

            private string _propertyName;
            private NewJsonValueType _valueType = NewJsonValueType.String;
            private string _stringValue = string.Empty;
            private long _integerValue;
            private double _floatValue;
            private bool _booleanValue;
            private bool _focusNameField = true;

            public AddJsonValuePopup(
                PersistentJsonTreeWindow owner,
                JsonFileData file,
                JContainer parent,
                string parentPath)
            {
                this._owner = owner;
                this._file = file;
                this._parent = parent;
                this._parentPath = parentPath;

                JObject jsonObject = parent as JObject;
                _needsPropertyName = jsonObject != null;
                _propertyName = _needsPropertyName
                    ? CreateUniquePropertyName(jsonObject, "newValue")
                    : string.Empty;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(390f, _needsPropertyName ? 230f : 205f);
            }

            public override void OnGUI(Rect rect)
            {
                GUILayout.Space(6f);

                EditorGUILayout.LabelField(
                    _needsPropertyName ? "Add Object Property" : "Add Array Item",
                    EditorStyles.boldLabel
                );

                GUILayout.Space(4f);

                if (_needsPropertyName)
                {
                    GUI.SetNextControlName("NewJsonPropertyName");
                    _propertyName = EditorGUILayout.TextField(
                        "Name",
                        _propertyName
                    );

                    if (_focusNameField && Event.current.type == EventType.Repaint)
                    {
                        EditorGUI.FocusTextInControl("NewJsonPropertyName");
                        _focusNameField = false;
                    }
                }

                EditorGUILayout.LabelField(
                    "Type",
                    EditorStyles.miniBoldLabel
                );

                int selectedType = GUILayout.SelectionGrid(
                    (int)_valueType,
                    ValueTypeLabels,
                    4,
                    EditorStyles.miniButton
                );

                _valueType = (NewJsonValueType)selectedType;

                GUILayout.Space(3f);
                DrawInitialValueField();

                string validationError = GetValidationError();

                if (!string.IsNullOrEmpty(validationError))
                {
                    EditorGUILayout.HelpBox(
                        validationError,
                        MessageType.Warning
                    );
                }
                else
                {
                    GUILayout.Space(EditorGUIUtility.singleLineHeight + 2f);
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Cancel", GUILayout.Width(72f)))
                    {
                        editorWindow.Close();
                        GUIUtility.ExitGUI();
                    }

                    using (new EditorGUI.DisabledScope(
                               !string.IsNullOrEmpty(validationError)))
                    {
                        if (GUILayout.Button("Add", GUILayout.Width(72f)))
                        {
                            _owner.AddNewValue(
                                _file,
                                _parent,
                                _parentPath,
                                _propertyName,
                                CreateToken()
                            );

                            editorWindow.Close();
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            private void DrawInitialValueField()
            {
                switch (_valueType)
                {
                    case NewJsonValueType.String:
                        _stringValue = EditorGUILayout.TextField(
                            "Value",
                            _stringValue
                        );
                        break;

                    case NewJsonValueType.Integer:
                        _integerValue = EditorGUILayout.LongField(
                            "Value",
                            _integerValue
                        );
                        break;

                    case NewJsonValueType.Float:
                        _floatValue = EditorGUILayout.DoubleField(
                            "Value",
                            _floatValue
                        );
                        break;

                    case NewJsonValueType.Boolean:
                        _booleanValue = EditorGUILayout.Toggle(
                            "Value",
                            _booleanValue
                        );
                        break;

                    default:
                        EditorGUILayout.LabelField(
                            "Value",
                            GetDefaultValuePreview(_valueType)
                        );
                        break;
                }
            }

            private string GetValidationError()
            {
                if (!_needsPropertyName)
                {
                    return null;
                }

                string trimmedName = _propertyName == null
                    ? string.Empty
                    : _propertyName.Trim();

                if (trimmedName.Length == 0)
                {
                    return "Property name cannot be empty.";
                }

                JObject jsonObject = (JObject)_parent;

                if (jsonObject.Property(trimmedName) != null)
                {
                    return "A property with this name already exists.";
                }

                return null;
            }

            private JToken CreateToken()
            {
                switch (_valueType)
                {
                    case NewJsonValueType.String:
                        return new JValue(_stringValue ?? string.Empty);

                    case NewJsonValueType.Integer:
                        return new JValue(_integerValue);

                    case NewJsonValueType.Float:
                        return new JValue(_floatValue);

                    case NewJsonValueType.Boolean:
                        return new JValue(_booleanValue);

                    case NewJsonValueType.Object:
                        return new JObject();

                    case NewJsonValueType.Array:
                        return new JArray();

                    case NewJsonValueType.Null:
                        return JValue.CreateNull();

                    default:
                        return JValue.CreateNull();
                }
            }

            private static string GetDefaultValuePreview(
                NewJsonValueType type)
            {
                switch (type)
                {
                    case NewJsonValueType.Object:
                        return "{ }";
                    case NewJsonValueType.Array:
                        return "[ ]";
                    case NewJsonValueType.Null:
                        return "null";
                    default:
                        return string.Empty;
                }
            }

            private static string CreateUniquePropertyName(
                JObject jsonObject,
                string baseName)
            {
                if (jsonObject.Property(baseName) == null)
                {
                    return baseName;
                }

                int suffix = 2;

                while (jsonObject.Property(baseName + suffix) != null)
                {
                    suffix++;
                }

                return baseName + suffix;
            }
        }

        private const float TreeIndent = 18f;
        private const float TreePadding = 4f;
        private const float ValueColumnOffset = 360f;
        private const float ValueColumnGap = 12f;
        private const float ValueRightPadding = 4f;
        private const float MinimumLabelColumnWidth = 120f;
        private const float ActionButtonWidth = 22f;
        private const float ActionButtonGap = 4f;
        private const double AutoReloadInterval = 1.0;

        private readonly List<JsonFileData> files = new List<JsonFileData>();
        private readonly Dictionary<string, bool> nodeFoldouts =
            new Dictionary<string, bool>();

        private string _persistentPath;
        private string _folderFingerprint = string.Empty;
        private string _scanError;
        private Vector2 _scrollPosition;
        private double _nextAutoReloadTime;

        private bool _autoReload = true;
        private bool _expandFilesByDefault;
        private bool _expandNodesByDefault = true;
        private int _skippedFileCount;
        private int _visibleTreeRowIndex;

        private GUIStyle _fileHeaderStyle;
        private GUIStyle _containerRowStyle;
        private GUIStyle _variableLabelStyle;

        [MenuItem("qtTools/Persistent JSON Tree", priority = 2)]
        private static void OpenWindow()
        {
            PersistentJsonTreeWindow window =
                GetWindow<PersistentJsonTreeWindow>("Persistent JSON");

            window.minSize = new Vector2(700f, 400f);
            window.Show();
        }

        private void OnEnable()
        {
            _persistentPath = Application.persistentDataPath;
            RefreshAllFiles();
        }

        private void OnInspectorUpdate()
        {
            if (!_autoReload ||
                HasUnsavedChanges() ||
                EditorApplication.timeSinceStartup < _nextAutoReloadTime)
            {
                return;
            }

            _nextAutoReloadTime =
                EditorApplication.timeSinceStartup + AutoReloadInterval;

            try
            {
                string[] paths = GetAllFilePaths();
                string fingerprint = CreateFolderFingerprint(paths);

                if (fingerprint != _folderFingerprint)
                {
                    LoadFiles(paths, fingerprint);
                    Repaint();
                }
            }
            catch
            {
                // Manual Refresh can be used if an automatic scan fails.
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            DrawHeader();
            EditorGUILayout.Space(6f);
            DrawToolbar();
            EditorGUILayout.Space(4f);

            if (!string.IsNullOrEmpty(_scanError))
            {
                EditorGUILayout.HelpBox(_scanError, MessageType.Error);
            }

            if (HasUnsavedChanges() && _autoReload)
            {
                EditorGUILayout.HelpBox(
                    "Auto Reload is paused while there are unsaved changes.",
                    MessageType.Info
                );
            }

            if (files.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    _skippedFileCount > 0
                        ? "No readable JSON content was found. Unreadable or non-JSON files were skipped."
                        : "No files were found in persistentDataPath.",
                    MessageType.Info
                );
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (int i = 0; i < files.Count; i++)
            {
                DrawFile(files[i]);
                EditorGUILayout.Space(4f);
            }

            EditorGUILayout.EndScrollView();
        }

        private void EnsureStyles()
        {
            if (_fileHeaderStyle == null)
            {
                // Used as a clickable label, not as a Foldout or Toggle.
                // Unity therefore draws no triangle/dropdown icon.
                _fileHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(4, 4, 2, 2)
                };
            }

            if (_containerRowStyle == null)
            {
                _containerRowStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(2, 2, 0, 0)
                };
            }

            if (_variableLabelStyle == null)
            {
                _variableLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0)
                };
            }

        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField(
                "Application.persistentDataPath",
                EditorStyles.boldLabel
            );

            Rect pathRect = EditorGUILayout.GetControlRect(
                false,
                EditorGUIUtility.singleLineHeight
            );

            EditorGUI.SelectableLabel(
                pathRect,
                _persistentPath,
                EditorStyles.textField
            );

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Folder", GUILayout.Width(100f)))
                {
                    Directory.CreateDirectory(_persistentPath);
                    EditorUtility.RevealInFinder(_persistentPath);
                }

                if (GUILayout.Button("Refresh All", GUILayout.Width(100f)))
                {
                    RefreshAllFiles();
                    GUIUtility.ExitGUI();
                }

                GUILayout.FlexibleSpace();

                _autoReload = GUILayout.Toggle(
                    _autoReload,
                    "Auto Reload",
                    GUI.skin.button,
                    GUILayout.Width(100f)
                );
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(
                    string.Format(
                        "Layout V7 | Readable JSON: {0} | Skipped: {1}",
                        files.Count,
                        _skippedFileCount
                    ),
                    EditorStyles.boldLabel
                );

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!HasUnsavedChanges()))
                {
                    if (GUILayout.Button(
                            "Save All",
                            EditorStyles.toolbarButton,
                            GUILayout.Width(65f)))
                    {
                        SaveAllDirtyFiles();
                    }
                }

                if (GUILayout.Button(
                        "Expand Files",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(85f)))
                {
                    SetAllFileFoldouts(true);
                }

                if (GUILayout.Button(
                        "Collapse Files",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(90f)))
                {
                    SetAllFileFoldouts(false);
                }

                if (GUILayout.Button(
                        "Expand Tree",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(85f)))
                {
                    SetAllNodeFoldouts(true);
                }

                if (GUILayout.Button(
                        "Collapse Tree",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(90f)))
                {
                    SetAllNodeFoldouts(false);
                }
            }
        }

        private void DrawFile(JsonFileData file)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string headerText = file.IsDirty
                        ? file.RelativePath + "  *"
                        : file.RelativePath;

                    GUIContent headerContent = new GUIContent(
                        headerText,
                        "Click the file name to expand or collapse it."
                    );

                    Rect headerRect = GUILayoutUtility.GetRect(
                        headerContent,
                        _fileHeaderStyle,
                        GUILayout.ExpandWidth(true),
                        GUILayout.Height(EditorGUIUtility.singleLineHeight + 4f)
                    );

                    if (GUI.Button(headerRect, headerContent, _fileHeaderStyle))
                    {
                        file.Expanded = !file.Expanded;
                        GUI.FocusControl(null);
                    }

                    EditorGUIUtility.AddCursorRect(headerRect, MouseCursor.Link);

                    using (new EditorGUI.DisabledScope(!file.IsDirty))
                    {
                        if (GUILayout.Button("Save", GUILayout.Width(50f)))
                        {
                            SaveFile(file);
                        }

                        if (GUILayout.Button("Revert", GUILayout.Width(55f)))
                        {
                            ReloadFile(file);
                        }
                    }

                    if (GUILayout.Button("Reveal", GUILayout.Width(55f)))
                    {
                        EditorUtility.RevealInFinder(file.FullPath);
                    }
                }

                if (!file.Expanded)
                {
                    return;
                }

                EditorGUILayout.LabelField(
                    string.Format(
                        "Size: {0:N0} bytes | Modified: {1}",
                        file.Length,
                        file.LastWriteTimeUtc.ToLocalTime()
                    ),
                    EditorStyles.miniLabel
                );

                if (!string.IsNullOrEmpty(file.SaveError))
                {
                    EditorGUILayout.HelpBox(file.SaveError, MessageType.Error);
                }

                _visibleTreeRowIndex = 0;

                DrawToken(
                    file,
                    "$",
                    file.Root,
                    file.FullPath + "::$",
                    0,
                    new List<bool>(),
                    true
                );
            }
        }

        private void DrawToken(
            JsonFileData file,
            string label,
            JToken token,
            string nodePath,
            int depth,
            List<bool> ancestorHasNextSibling,
            bool isLastChild)
        {
            if (token == null)
            {
                token = JValue.CreateNull();
            }

            JObject jsonObject = token as JObject;
            if (jsonObject != null)
            {
                DrawObject(
                    file,
                    label,
                    jsonObject,
                    nodePath,
                    depth,
                    ancestorHasNextSibling,
                    isLastChild
                );
                return;
            }

            JArray jsonArray = token as JArray;
            if (jsonArray != null)
            {
                DrawArray(
                    file,
                    label,
                    jsonArray,
                    nodePath,
                    depth,
                    ancestorHasNextSibling,
                    isLastChild
                );
                return;
            }

            DrawPrimitive(
                file,
                label,
                token,
                nodePath,
                depth,
                ancestorHasNextSibling,
                isLastChild
            );
        }

        private void DrawObject(
            JsonFileData file,
            string label,
            JObject jsonObject,
            string nodePath,
            int depth,
            List<bool> ancestorHasNextSibling,
            bool isLastChild)
        {
            List<JProperty> properties = jsonObject.Properties().ToList();

            bool expanded = DrawContainerRow(
                file,
                jsonObject,
                label,
                "Object",
                properties.Count,
                nodePath,
                depth,
                ancestorHasNextSibling,
                isLastChild
            );

            if (!expanded)
            {
                return;
            }

            List<bool> childGuides = BuildChildGuides(
                ancestorHasNextSibling,
                depth,
                isLastChild
            );

            for (int i = 0; i < properties.Count; i++)
            {
                JProperty property = properties[i];

                DrawToken(
                    file,
                    property.Name,
                    property.Value,
                    nodePath + "/" + EscapeNodePath(property.Name),
                    depth + 1,
                    childGuides,
                    i == properties.Count - 1
                );
            }
        }

        private void DrawArray(
            JsonFileData file,
            string label,
            JArray jsonArray,
            string nodePath,
            int depth,
            List<bool> ancestorHasNextSibling,
            bool isLastChild)
        {
            bool expanded = DrawContainerRow(
                file,
                jsonArray,
                label,
                "Array",
                jsonArray.Count,
                nodePath,
                depth,
                ancestorHasNextSibling,
                isLastChild
            );

            if (!expanded)
            {
                return;
            }

            List<bool> childGuides = BuildChildGuides(
                ancestorHasNextSibling,
                depth,
                isLastChild
            );

            for (int i = 0; i < jsonArray.Count; i++)
            {
                DrawToken(
                    file,
                    "[" + i + "]",
                    jsonArray[i],
                    nodePath + "/" + i,
                    depth + 1,
                    childGuides,
                    i == jsonArray.Count - 1
                );
            }
        }

        private bool DrawContainerRow(
            JsonFileData file,
            JContainer container,
            string label,
            string typeName,
            int childCount,
            string nodePath,
            int depth,
            List<bool> ancestorHasNextSibling,
            bool isLastChild)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(
                false,
                EditorGUIUtility.singleLineHeight
            );

            bool expanded = GetNodeFoldout(nodePath);
            bool canRemove = CanRemoveValue(container);
            bool showAddButton = expanded;

            DrawAlternatingRowBackground(rowRect);
            DrawTreeConnections(
                rowRect,
                depth,
                ancestorHasNextSibling,
                isLastChild
            );

            float contentX = GetContentX(rowRect, depth);
            float actionsLeft = rowRect.xMax;
            Rect addButtonRect = new Rect();
            Rect removeButtonRect = new Rect();

            // The add button only exists while this Object/Array is expanded.
            if (showAddButton)
            {
                addButtonRect = new Rect(
                    actionsLeft - ActionButtonWidth,
                    rowRect.y,
                    ActionButtonWidth,
                    rowRect.height
                );

                actionsLeft = addButtonRect.x - ActionButtonGap;
            }

            // Root cannot be deleted. Every property or array item can.
            if (canRemove)
            {
                removeButtonRect = new Rect(
                    actionsLeft - ActionButtonWidth,
                    rowRect.y,
                    ActionButtonWidth,
                    rowRect.height
                );

                actionsLeft = removeButtonRect.x - ActionButtonGap;
            }

            Rect contentRect = new Rect(
                contentX,
                rowRect.y,
                Mathf.Max(0f, actionsLeft - contentX),
                rowRect.height
            );

            string rowText = string.Format(
                "{0} ({1})",
                label,
                typeName
            );

            GUIContent rowContent = new GUIContent(
                rowText,
                "Click the row to expand or collapse this node."
            );

            // Manual click handling guarantees that no Foldout/Toggle/Popup icon
            // can appear before the variable name.
            Event currentEvent = Event.current;
            bool hovered = contentRect.Contains(currentEvent.mousePosition);

            if (currentEvent.type == EventType.Repaint && hovered)
            {
                EditorGUI.DrawRect(contentRect, GetRowHoverColor());
            }

            if (currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 &&
                hovered)
            {
                expanded = !expanded;
                nodeFoldouts[nodePath] = expanded;
                GUI.FocusControl(null);
                currentEvent.Use();
                GUI.changed = true;
            }

            GUI.Label(contentRect, rowContent, _containerRowStyle);
            EditorGUIUtility.AddCursorRect(contentRect, MouseCursor.Link);

            if (canRemove)
            {
                GUIContent removeContent = new GUIContent(
                    "-",
                    "Delete this property or array item."
                );

                if (GUI.Button(
                        removeButtonRect,
                        removeContent,
                        EditorStyles.miniButton))
                {
                    RemoveValue(file, container, nodePath);
                    GUIUtility.ExitGUI();
                }
            }

            if (showAddButton)
            {
                GUIContent addContent = new GUIContent(
                    "+",
                    container is JObject
                        ? "Add a property to this object."
                        : "Add an item to this array."
                );

                if (GUI.Button(addButtonRect, addContent, EditorStyles.miniButton))
                {
                    nodeFoldouts[nodePath] = true;
                    PopupWindow.Show(
                        addButtonRect,
                        new AddJsonValuePopup(
                            this,
                            file,
                            container,
                            nodePath
                        )
                    );
                }
            }

            nodeFoldouts[nodePath] = expanded;

            if (expanded && childCount > 0)
            {
                DrawChildStem(rowRect, depth);
            }

            return expanded;
        }

        private void DrawPrimitive(
            JsonFileData file,
            string label,
            JToken token,
            string nodePath,
            int depth,
            List<bool> ancestorHasNextSibling,
            bool isLastChild)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(
                false,
                EditorGUIUtility.singleLineHeight
            );

            DrawAlternatingRowBackground(rowRect);
            DrawTreeConnections(
                rowRect,
                depth,
                ancestorHasNextSibling,
                isLastChild
            );

            bool canRemove = CanRemoveValue(token);
            Rect removeButtonRect = new Rect();
            float valueRight = rowRect.xMax - ValueRightPadding;

            if (canRemove)
            {
                removeButtonRect = new Rect(
                    rowRect.xMax - ActionButtonWidth,
                    rowRect.y,
                    ActionButtonWidth,
                    rowRect.height
                );

                valueRight = removeButtonRect.x - ActionButtonGap;
            }

            float contentX = GetContentX(rowRect, depth);
            string typeName = GetTypeName(token.Type);
            GUIContent labelContent = new GUIContent(
                label + " (" + typeName + ")",
                nodePath
            );

            // Keep all value controls in a separate, aligned column. The value
            // field expands automatically until the delete button or right edge.
            float minimumValueWidth = token.Type == JTokenType.Boolean
                ? 20f
                : 56f;
            float preferredValueX = rowRect.x + ValueColumnOffset;
            float minimumValueX = contentX + MinimumLabelColumnWidth;
            float maximumValueX = valueRight - minimumValueWidth;
            float valueX = Mathf.Clamp(
                Mathf.Max(preferredValueX, minimumValueX),
                contentX,
                Mathf.Max(contentX, maximumValueX)
            );

            Rect labelRect = new Rect(
                contentX,
                rowRect.y,
                Mathf.Max(0f, valueX - contentX - ValueColumnGap),
                rowRect.height
            );

            EditorGUI.LabelField(
                labelRect,
                labelContent,
                _variableLabelStyle
            );

            float remainingWidth = Mathf.Max(0f, valueRight - valueX);

            if (remainingWidth > 1f)
            {
                JValue value = token as JValue;

                if (value == null)
                {
                    DrawReadOnlyValue(
                        new Rect(valueX, rowRect.y, remainingWidth, rowRect.height),
                        GetDisplayValue(token)
                    );
                }
                else
                {
                    switch (token.Type)
                    {
                        case JTokenType.Boolean:
                            DrawBooleanField(file, value, valueX, rowRect);
                            break;

                        case JTokenType.Integer:
                            DrawIntegerField(
                                file,
                                value,
                                valueX,
                                remainingWidth,
                                rowRect
                            );
                            break;

                        case JTokenType.Float:
                            DrawFloatField(
                                file,
                                value,
                                valueX,
                                remainingWidth,
                                rowRect
                            );
                            break;

                        case JTokenType.String:
                            DrawStringField(
                                file,
                                value,
                                valueX,
                                remainingWidth,
                                rowRect
                            );
                            break;

                        case JTokenType.Null:
                        case JTokenType.Undefined:
                            DrawReadOnlyValue(
                                new Rect(
                                    valueX,
                                    rowRect.y,
                                    remainingWidth,
                                    rowRect.height
                                ),
                                token.Type == JTokenType.Null
                                    ? "null"
                                    : "undefined"
                            );
                            break;

                        default:
                            DrawReadOnlyValue(
                                new Rect(
                                    valueX,
                                    rowRect.y,
                                    remainingWidth,
                                    rowRect.height
                                ),
                                GetDisplayValue(token)
                            );
                            break;
                    }
                }
            }

            if (canRemove)
            {
                GUIContent removeContent = new GUIContent(
                    "-",
                    "Delete this property or array item."
                );

                if (GUI.Button(
                        removeButtonRect,
                        removeContent,
                        EditorStyles.miniButton))
                {
                    RemoveValue(file, token, nodePath);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawBooleanField(
            JsonFileData file,
            JValue value,
            float valueX,
            Rect rowRect)
        {
            bool currentValue = value.Value<bool>();

            // A real Boolean checkbox, aligned with the value column.
            Rect toggleRect = new Rect(
                valueX,
                rowRect.y + Mathf.Max(0f, (rowRect.height - 16f) * 0.5f),
                16f,
                16f
            );

            EditorGUI.BeginChangeCheck();
            bool newValue = EditorGUI.Toggle(toggleRect, currentValue);

            if (EditorGUI.EndChangeCheck())
            {
                value.Value = newValue;
                MarkDirty(file);
            }
        }

        private void DrawIntegerField(
            JsonFileData file,
            JValue value,
            float valueX,
            float remainingWidth,
            Rect rowRect)
        {
            long currentValue;

            if (!TryConvertToInt64(value.Value, out currentValue))
            {
                DrawReadOnlyValue(
                    new Rect(valueX, rowRect.y, remainingWidth, rowRect.height),
                    Convert.ToString(value.Value, CultureInfo.InvariantCulture)
                );
                return;
            }

            Rect fieldRect = new Rect(
                valueX,
                rowRect.y,
                remainingWidth,
                rowRect.height
            );

            EditorGUI.BeginChangeCheck();
            long newValue = EditorGUI.LongField(fieldRect, currentValue);

            if (EditorGUI.EndChangeCheck())
            {
                value.Value = newValue;
                MarkDirty(file);
            }
        }

        private void DrawFloatField(
            JsonFileData file,
            JValue value,
            float valueX,
            float remainingWidth,
            Rect rowRect)
        {
            double currentValue;

            if (!TryConvertToDouble(value.Value, out currentValue))
            {
                DrawReadOnlyValue(
                    new Rect(valueX, rowRect.y, remainingWidth, rowRect.height),
                    Convert.ToString(value.Value, CultureInfo.InvariantCulture)
                );
                return;
            }

            Rect fieldRect = new Rect(
                valueX,
                rowRect.y,
                remainingWidth,
                rowRect.height
            );

            EditorGUI.BeginChangeCheck();
            double newValue = EditorGUI.DoubleField(fieldRect, currentValue);

            if (EditorGUI.EndChangeCheck())
            {
                value.Value = newValue;
                MarkDirty(file);
            }
        }

        private void DrawStringField(
            JsonFileData file,
            JValue value,
            float valueX,
            float remainingWidth,
            Rect rowRect)
        {
            string currentValue = value.Value<string>() ?? string.Empty;

            Rect fieldRect = new Rect(
                valueX,
                rowRect.y,
                remainingWidth,
                rowRect.height
            );

            EditorGUI.BeginChangeCheck();
            string newValue = EditorGUI.TextField(fieldRect, currentValue);

            if (EditorGUI.EndChangeCheck())
            {
                value.Value = newValue;
                MarkDirty(file);
            }
        }

        private static void DrawReadOnlyValue(Rect rect, string text)
        {
            string displayText = text ?? string.Empty;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.TextField(rect, displayText);
            }
        }

        private static bool TryConvertToInt64(object value, out long result)
        {
            try
            {
                result = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                result = 0L;
                return false;
            }
        }

        private static bool TryConvertToDouble(object value, out double result)
        {
            try
            {
                result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                result = 0d;
                return false;
            }
        }

        private static string GetTypeName(JTokenType tokenType)
        {
            switch (tokenType)
            {
                case JTokenType.Boolean:
                    return "Boolean";
                case JTokenType.Integer:
                    return "Integer";
                case JTokenType.Float:
                    return "Float";
                case JTokenType.String:
                    return "String";
                case JTokenType.Null:
                    return "Null";
                case JTokenType.Undefined:
                    return "Undefined";
                default:
                    return tokenType.ToString();
            }
        }

        private static List<bool> BuildChildGuides(
            List<bool> ancestorHasNextSibling,
            int depth,
            bool isLastChild)
        {
            List<bool> result = new List<bool>(ancestorHasNextSibling);

            // Root has no sibling branch of its own.
            if (depth > 0)
            {
                result.Add(!isLastChild);
            }

            return result;
        }

        private static void DrawTreeConnections(
            Rect rowRect,
            int depth,
            List<bool> ancestorHasNextSibling,
            bool isLastChild)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            float baseX = rowRect.x + TreePadding;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            for (int i = 0; i < ancestorHasNextSibling.Count; i++)
            {
                if (!ancestorHasNextSibling[i])
                {
                    continue;
                }

                float x = baseX + i * TreeIndent + TreeIndent * 0.5f;
                DrawVerticalLine(
                    x,
                    rowRect.yMin - spacing,
                    rowRect.yMax + spacing
                );
            }

            if (depth <= 0)
            {
                return;
            }

            float branchX =
                baseX + (depth - 1) * TreeIndent + TreeIndent * 0.5f;

            DrawVerticalLine(
                branchX,
                rowRect.yMin - spacing,
                isLastChild
                    ? rowRect.center.y
                    : rowRect.yMax + spacing
            );

            DrawHorizontalLine(
                branchX,
                rowRect.center.y,
                GetContentX(rowRect, depth) - 3f
            );
        }

        private static void DrawChildStem(Rect rowRect, int depth)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            float x =
                rowRect.x + TreePadding +
                depth * TreeIndent + TreeIndent * 0.5f;

            DrawVerticalLine(
                x,
                rowRect.center.y,
                rowRect.yMax + EditorGUIUtility.standardVerticalSpacing
            );
        }

        private static void DrawVerticalLine(
            float x,
            float yMin,
            float yMax)
        {
            if (yMax <= yMin)
            {
                return;
            }

            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            float thickness = 1f / pixelsPerPoint;
            float alignedX = Mathf.Round(x * pixelsPerPoint) / pixelsPerPoint;

            EditorGUI.DrawRect(
                new Rect(
                    alignedX - thickness * 0.5f,
                    yMin,
                    thickness,
                    yMax - yMin
                ),
                GetTreeLineColor()
            );
        }

        private static void DrawHorizontalLine(
            float xMin,
            float y,
            float xMax)
        {
            if (xMax <= xMin)
            {
                return;
            }

            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            float thickness = 1f / pixelsPerPoint;
            float alignedY = Mathf.Round(y * pixelsPerPoint) / pixelsPerPoint;

            EditorGUI.DrawRect(
                new Rect(
                    xMin,
                    alignedY - thickness * 0.5f,
                    xMax - xMin,
                    thickness
                ),
                GetTreeLineColor()
            );
        }

        private void DrawAlternatingRowBackground(Rect rowRect)
        {
            int rowIndex = _visibleTreeRowIndex++;

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(
                rowRect,
                GetAlternatingRowColor(rowIndex)
            );
        }

        private static Color GetAlternatingRowColor(int rowIndex)
        {
            bool alternate = (rowIndex & 1) == 1;

            if (EditorGUIUtility.isProSkin)
            {
                return alternate
                    ? new Color(1f, 1f, 1f, 0.055f)
                    : new Color(1f, 1f, 1f, 0.015f);
            }

            return alternate
                ? new Color(0f, 0f, 0f, 0.055f)
                : new Color(0f, 0f, 0f, 0.012f);
        }

        private static Color GetRowHoverColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.055f)
                : new Color(0f, 0f, 0f, 0.055f);
        }

        private static Color GetTreeLineColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.20f)
                : new Color(0f, 0f, 0f, 0.25f);
        }

        private static float GetContentX(Rect rowRect, int depth)
        {
            return rowRect.x + TreePadding + depth * TreeIndent;
        }

        private bool GetNodeFoldout(string nodePath)
        {
            bool expanded;

            if (nodeFoldouts.TryGetValue(nodePath, out expanded))
            {
                return expanded;
            }

            nodeFoldouts[nodePath] = _expandNodesByDefault;
            return _expandNodesByDefault;
        }

        private void SetAllFileFoldouts(bool expanded)
        {
            _expandFilesByDefault = expanded;

            for (int i = 0; i < files.Count; i++)
            {
                files[i].Expanded = expanded;
            }

            Repaint();
        }

        private void SetAllNodeFoldouts(bool expanded)
        {
            _expandNodesByDefault = expanded;
            nodeFoldouts.Clear();
            Repaint();
        }

        private bool HasUnsavedChanges()
        {
            for (int i = 0; i < files.Count; i++)
            {
                if (files[i].IsDirty)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanRemoveValue(JToken token)
        {
            return token != null && token.Parent != null;
        }

        private void RemoveValue(
            JsonFileData file,
            JToken token,
            string nodePath)
        {
            if (!CanRemoveValue(token))
            {
                return;
            }

            string parentPath = GetParentNodePath(nodePath);
            bool parentIsArray = token.Parent is JArray;

            try
            {
                JProperty property = token.Parent as JProperty;

                if (property != null)
                {
                    property.Remove();
                }
                else
                {
                    token.Remove();
                }

                // Array indexes change after a deletion, so discard cached child
                // foldout states under that array. Object properties keep stable
                // names, so only the removed branch needs to be discarded.
                if (parentIsArray)
                {
                    RemoveChildFoldoutStates(parentPath);
                }
                else
                {
                    RemoveFoldoutBranch(nodePath);
                }

                if (!string.IsNullOrEmpty(parentPath))
                {
                    nodeFoldouts[parentPath] = true;
                }

                MarkDirty(file);
                GUI.FocusControl(null);
                GUI.changed = true;
                Repaint();
            }
            catch (Exception exception)
            {
                file.SaveError =
                    "Could not delete this value:\n" + exception.Message;
                Repaint();
            }
        }

        private static string GetParentNodePath(string nodePath)
        {
            if (string.IsNullOrEmpty(nodePath))
            {
                return null;
            }

            int rootMarkerIndex = nodePath.IndexOf(
                "::$",
                StringComparison.Ordinal
            );
            int separatorIndex = nodePath.LastIndexOf('/');

            // Slashes before ::$ belong to the file system path, not the JSON path.
            if (separatorIndex < 0 ||
                (rootMarkerIndex >= 0 &&
                 separatorIndex < rootMarkerIndex + 3))
            {
                return null;
            }

            return nodePath.Substring(0, separatorIndex);
        }

        private void RemoveFoldoutBranch(string branchPath)
        {
            if (string.IsNullOrEmpty(branchPath))
            {
                return;
            }

            string childPrefix = branchPath + "/";
            List<string> keysToRemove = nodeFoldouts.Keys
                .Where(key =>
                    string.Equals(
                        key,
                        branchPath,
                        StringComparison.Ordinal
                    ) ||
                    key.StartsWith(
                        childPrefix,
                        StringComparison.Ordinal
                    ))
                .ToList();

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                nodeFoldouts.Remove(keysToRemove[i]);
            }
        }

        private void RemoveChildFoldoutStates(string parentPath)
        {
            if (string.IsNullOrEmpty(parentPath))
            {
                return;
            }

            string childPrefix = parentPath + "/";
            List<string> keysToRemove = nodeFoldouts.Keys
                .Where(key => key.StartsWith(
                    childPrefix,
                    StringComparison.Ordinal
                ))
                .ToList();

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                nodeFoldouts.Remove(keysToRemove[i]);
            }
        }

        private void AddNewValue(
            JsonFileData file,
            JContainer parent,
            string parentPath,
            string propertyName,
            JToken newToken)
        {
            JObject jsonObject = parent as JObject;

            if (jsonObject != null)
            {
                string finalName = propertyName == null
                    ? string.Empty
                    : propertyName.Trim();

                if (finalName.Length == 0 ||
                    jsonObject.Property(finalName) != null)
                {
                    file.SaveError =
                        "Could not add the property because its name is invalid or already exists.";
                    Repaint();
                    return;
                }

                jsonObject.Add(new JProperty(finalName, newToken));
                nodeFoldouts[parentPath] = true;

                if (newToken is JContainer)
                {
                    nodeFoldouts[
                        parentPath + "/" + EscapeNodePath(finalName)
                    ] = true;
                }
            }
            else
            {
                JArray jsonArray = parent as JArray;

                if (jsonArray == null)
                {
                    file.SaveError =
                        "Values can only be added to an Object or Array.";
                    Repaint();
                    return;
                }

                int newIndex = jsonArray.Count;
                jsonArray.Add(newToken);
                nodeFoldouts[parentPath] = true;

                if (newToken is JContainer)
                {
                    nodeFoldouts[parentPath + "/" + newIndex] = true;
                }
            }

            MarkDirty(file);
            GUI.FocusControl(null);
            Repaint();
        }

        private static void MarkDirty(JsonFileData file)
        {
            file.IsDirty = true;
            file.SaveError = null;
        }

        private void SaveAllDirtyFiles()
        {
            for (int i = 0; i < files.Count; i++)
            {
                if (files[i].IsDirty)
                {
                    SaveFile(files[i]);
                }
            }

            UpdateFolderFingerprint();
            Repaint();
        }

        private void SaveFile(JsonFileData file)
        {
            try
            {
                using (FileStream stream = new FileStream(
                           file.FullPath,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.Read))
                using (StreamWriter textWriter = new StreamWriter(
                           stream,
                           new UTF8Encoding(false)))
                using (JsonTextWriter jsonWriter = new JsonTextWriter(textWriter))
                {
                    jsonWriter.Formatting = Formatting.Indented;
                    jsonWriter.Indentation = 2;
                    jsonWriter.IndentChar = ' ';
                    file.Root.WriteTo(jsonWriter);
                }

                FileInfo info = new FileInfo(file.FullPath);
                info.Refresh();

                file.Length = info.Length;
                file.LastWriteTimeUtc = info.LastWriteTimeUtc;
                file.IsDirty = false;
                file.SaveError = null;

                UpdateFolderFingerprint();
            }
            catch (Exception exception)
            {
                file.SaveError =
                    "Could not save this file:\n" + exception.Message;
            }

            Repaint();
        }

        private void ReloadFile(JsonFileData file)
        {
            JsonFileData loadedFile;

            if (!TryReadJsonFile(file.FullPath, out loadedFile))
            {
                file.SaveError =
                    "The file can no longer be read as valid JSON.";
                return;
            }

            file.Root = loadedFile.Root;
            file.Length = loadedFile.Length;
            file.LastWriteTimeUtc = loadedFile.LastWriteTimeUtc;
            file.IsDirty = false;
            file.SaveError = null;

            UpdateFolderFingerprint();
            Repaint();
        }

        private void RefreshAllFiles()
        {
            try
            {
                _persistentPath = Application.persistentDataPath;
                Directory.CreateDirectory(_persistentPath);

                string[] paths = GetAllFilePaths();
                LoadFiles(paths, CreateFolderFingerprint(paths));
            }
            catch (Exception exception)
            {
                _scanError =
                    "Could not scan persistentDataPath:\n" + exception.Message;
                Repaint();
            }
        }

        private void LoadFiles(string[] paths, string fingerprint)
        {
            Dictionary<string, bool> oldStates = files.ToDictionary(
                file => file.FullPath,
                file => file.Expanded,
                StringComparer.OrdinalIgnoreCase
            );

            files.Clear();
            _skippedFileCount = 0;
            _scanError = null;

            for (int i = 0; i < paths.Length; i++)
            {
                JsonFileData file;

                if (!TryReadJsonFile(paths[i], out file))
                {
                    _skippedFileCount++;
                    continue;
                }

                bool previousState;
                file.Expanded = oldStates.TryGetValue(
                    file.FullPath,
                    out previousState
                )
                    ? previousState
                    : _expandFilesByDefault;

                files.Add(file);
            }

            _folderFingerprint = fingerprint;
            _nextAutoReloadTime =
                EditorApplication.timeSinceStartup + AutoReloadInterval;
            Repaint();
        }

        private bool TryReadJsonFile(
            string fullPath,
            out JsonFileData fileData)
        {
            fileData = null;

            try
            {
                FileInfo info = new FileInfo(fullPath);

                if (!info.Exists || info.Length == 0)
                {
                    return false;
                }

                JToken root;

                using (FileStream stream = new FileStream(
                           fullPath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader textReader = new StreamReader(
                           stream,
                           new UTF8Encoding(false, true),
                           true))
                using (JsonTextReader jsonReader =
                       new JsonTextReader(textReader))
                {
                    jsonReader.DateParseHandling = DateParseHandling.None;
                    root = JToken.ReadFrom(jsonReader);

                    // Skip files containing another value after the root JSON.
                    if (jsonReader.Read())
                    {
                        return false;
                    }
                }

                if (root == null || root.Type == JTokenType.Comment)
                {
                    return false;
                }

                info.Refresh();

                fileData = new JsonFileData
                {
                    FullPath = fullPath,
                    RelativePath = GetRelativePath(fullPath),
                    Root = root,
                    Length = info.Length,
                    LastWriteTimeUtc = info.LastWriteTimeUtc,
                    Expanded = _expandFilesByDefault,
                    IsDirty = false,
                    SaveError = null
                };

                return true;
            }
            catch
            {
                // Invalid JSON, binary, locked, inaccessible, or unsupported file.
                return false;
            }
        }

        private string[] GetAllFilePaths()
        {
            return EnumerateAllFilesSafely(_persistentPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static IEnumerable<string> EnumerateAllFilesSafely(
            string rootPath)
        {
            Stack<string> pending = new Stack<string>();
            pending.Push(rootPath);

            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                string[] currentFiles;

                try
                {
                    currentFiles = Directory.GetFiles(
                        directory,
                        "*",
                        SearchOption.TopDirectoryOnly
                    );
                }
                catch
                {
                    currentFiles = Array.Empty<string>();
                }

                for (int i = 0; i < currentFiles.Length; i++)
                {
                    yield return currentFiles[i];
                }

                string[] subDirectories;

                try
                {
                    subDirectories = Directory.GetDirectories(
                        directory,
                        "*",
                        SearchOption.TopDirectoryOnly
                    );
                }
                catch
                {
                    subDirectories = Array.Empty<string>();
                }

                for (int i = 0; i < subDirectories.Length; i++)
                {
                    try
                    {
                        FileAttributes attributes =
                            File.GetAttributes(subDirectories[i]);

                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    pending.Push(subDirectories[i]);
                }
            }
        }

        private void UpdateFolderFingerprint()
        {
            try
            {
                string[] paths = GetAllFilePaths();
                _folderFingerprint = CreateFolderFingerprint(paths);
                _nextAutoReloadTime =
                    EditorApplication.timeSinceStartup + AutoReloadInterval;
            }
            catch
            {
                // The next automatic or manual refresh will rebuild it.
            }
        }

        private static string CreateFolderFingerprint(
            IEnumerable<string> paths)
        {
            StringBuilder builder = new StringBuilder();

            foreach (string path in paths)
            {
                try
                {
                    FileInfo info = new FileInfo(path);
                    builder.Append(path)
                        .Append('|')
                        .Append(info.Length)
                        .Append('|')
                        .Append(info.LastWriteTimeUtc.Ticks)
                        .Append('\n');
                }
                catch
                {
                    builder.Append(path).Append("|unreadable\n");
                }
            }

            return builder.ToString();
        }

        private string GetRelativePath(string fullPath)
        {
            if (!fullPath.StartsWith(
                    _persistentPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }

            return fullPath
                .Substring(_persistentPath.Length)
                .TrimStart(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                );
        }

        private static string EscapeNodePath(string value)
        {
            return value.Replace("~", "~0").Replace("/", "~1");
        }

        private static string GetDisplayValue(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Null:
                    return "null";
                case JTokenType.Undefined:
                    return "undefined";
                case JTokenType.String:
                    return token.Value<string>() ?? string.Empty;
                default:
                    return token.ToString(Formatting.None);
            }
        }
    }
}

#endif