using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using Object = UnityEngine.Object;

public class SceneScriptGenerator : EditorWindow
{
    private enum ScriptType
    {
        OverlayScene,
        Scene,
        Popup
    }

    private string _name = "";
    private ScriptType _type = ScriptType.Scene;

    private bool _hasInput = false;
    private bool _hasOutput = false;

    private string _sceneBase = "qtScene";
    private string _popupBase = "qtPopup";
    private string _overlaySceneBase = "qtOverlayScene";
    private string _mediatorBase = "qtMediator";
    private string _requestBase = "qtRequestMediator";
    private string _paramInBase = "ParamInput";
    private string _paramOutBase = "ParamOutput";

    private Vector2 _previewScroll;
    private string _preview = "";

    [MenuItem("qtTools/Scene Script Generator")]
    public static void Open()
    {
        var w = GetWindow<SceneScriptGenerator>("Scene Script Generator");
        w.minSize = new Vector2(400, 540);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);

        EditorGUI.BeginChangeCheck();

        _type = (ScriptType)EditorGUILayout.EnumPopup("Type", _type);
        _name = EditorGUILayout.TextField("Name", _name);

        if (_type == ScriptType.Popup)
        {
            EditorGUILayout.Space(4);
            _hasInput = EditorGUILayout.Toggle("Input", _hasInput);
            _hasOutput = EditorGUILayout.Toggle("Output", _hasOutput);
        }

        bool changed = EditorGUI.EndChangeCheck();

        // EditorGUILayout.Space(4);
        // _sceneBase = EditorGUILayout.TextField("Scene Base", _sceneBase);
        // _popupBase = EditorGUILayout.TextField("Popup Base", _popupBase);
        // _mediatorBase = EditorGUILayout.TextField("Mediator Base", _mediatorBase);
        // _requestBase = EditorGUILayout.TextField("Request Base", _requestBase);
        // _paramInBase = EditorGUILayout.TextField("ParamInput Base", _paramInBase);
        // _paramOutBase = EditorGUILayout.TextField("ParamOutput Base", _paramOutBase);
        //
        // EditorGUILayout.Space(8);

        if (changed) RefreshPreview();

        if (!string.IsNullOrWhiteSpace(_name))
        {
            EditorGUILayout.LabelField("Folder path: ", GetFolder());
            EditorGUILayout.Space(4);

            string suffix = Suffix();
            EditorGUILayout.LabelField($"• {_name}{suffix}.cs");
            EditorGUILayout.LabelField($"• {_name}{suffix}Mediator.cs");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(_preview, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Generate", GUILayout.Height(32)))
                Generate();
        }
        else
        {
            EditorGUILayout.HelpBox("Nhập tên để bắt đầu.", MessageType.Info);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string Suffix()
    {
        switch (_type)
        {
            case ScriptType.OverlayScene:
            {
                return "OverlayScene";
            }
            case ScriptType.Scene:
            {
                return "Scene";
            }
            case ScriptType.Popup:
            {
                return "Popup";
            }
            default:
            {
                throw new ArgumentOutOfRangeException();
            }
        }
    }

    private string BaseClass()
    {
        switch (_type)
        {
            case ScriptType.OverlayScene:
            {
                return _overlaySceneBase;
            }
            case ScriptType.Scene:
            {
                return _sceneBase;
            }
            case ScriptType.Popup:
            {
                return _popupBase;
            }
            default:
            {
                throw new ArgumentOutOfRangeException();
            }
        }
    }

    private string GetFolder()
    {
        switch (_type)
        {
            case ScriptType.OverlayScene:
            {
                return $"Assets/_Scripts/UI/OverlayPopup/{_name}{Suffix()}";
            }
            case ScriptType.Scene:
            {
                return $"Assets/_Scripts/UI/Scene/{_name}{Suffix()}";
            }
            case ScriptType.Popup:
            {
                return $"Assets/_Scripts/UI/Popup/{_name}{Suffix()}";
            }
            default:
            {
                throw new ArgumentOutOfRangeException();
            }
        }
    }

    private string MediatorBase()
    {
        string view = _name + Suffix();
        string logic = view + "Logic";
        string paramIn = view + "ParamInput";
        string paramOut = view + "ParamOutput";

        if (_type == ScriptType.Popup && _hasInput)
        {
            string generic = $"{view}, {logic}, {paramIn}";
            return $"{_requestBase}<{generic}>";
        }
        else
        {
            string generic = $"{view}, {logic}";
            return $"{_mediatorBase}<{generic}>";
        }
    }

    private void RefreshPreview()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            _preview = "";
            return;
        }

        var parts = new System.Collections.Generic.List<string>();
        parts.Add(BuildView());
        parts.Add(BuildMediator());
        _preview = string.Join("\n\n", parts);
    }

    // ── Generate ──────────────────────────────────────────────────────────────

    private void Generate()
    {
        string n = _name.Trim();
        if (string.IsNullOrEmpty(n)) return;
 
        string folder = GetFolder();
        string resFolder = folder + "/Resources";
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(resFolder);
 
        string suffix = Suffix();
        WriteFile(folder, $"{n}{suffix}.cs",         BuildView());
        WriteFile(folder, $"{n}{suffix}Mediator.cs", BuildMediator());
 
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done", $"Files created in:\n{folder}", "OK");
 
        var obj = AssetDatabase.LoadAssetAtPath<Object>(folder);
        if (obj != null) EditorGUIUtility.PingObject(obj);
    }
    
    private static void WriteFile(string folder, string file, string content)
    {
        string path = Path.Combine(folder, file);
        if (File.Exists(path))
            if (!EditorUtility.DisplayDialog("Overwrite?", $"{file} already exists. Overwrite?", "Yes", "Skip"))
                return;
        File.WriteAllText(path, content);
    }

    // ── Code builders ─────────────────────────────────────────────────────────

    private string BuildView()
    {
        string cls = _name + Suffix();
        string b   = BaseClass();
 
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using qtLib.UI.Base;");
        sb.AppendLine();
        sb.AppendLine($"namespace _Scripts.UI.{Suffix()}.{cls}");
        sb.AppendLine("{");
        sb.AppendLine($"    public class {cls} : {b}");
        sb.AppendLine("    {");
        sb.AppendLine("        #region ----- Component Config -----");
        sb.AppendLine("        #endregion");
        sb.AppendLine();
        sb.AppendLine("        #region ----- Properties -----");
        sb.AppendLine("        #endregion");
        sb.AppendLine();
        sb.AppendLine("        #region ----- Public Functions -----");
        sb.AppendLine("        #endregion");
        sb.AppendLine();
        sb.AppendLine("        #region ----- Private Functions -----");
        sb.AppendLine("        #endregion");
        sb.AppendLine("    }");
        sb.Append("}");
 
        return sb.ToString();
    }
    
    private string BuildMediator()
    {
        string view = _name + Suffix();
        string logic = view + "Logic";
        string mediator = view + "Mediator";
        string paramIn = view + "ParamInput";
        string paramOut = view + "ParamOutput";
        string medBase = MediatorBase();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using qtLib.UI.Base;");
        sb.AppendLine("using Cysharp.Threading.Tasks;");
        sb.AppendLine();
        sb.AppendLine($"namespace _Scripts.UI.{Suffix()}.{view}");
        sb.AppendLine("{");

        if (_type == ScriptType.Popup && _hasInput)
        {
            sb.AppendLine($"    public class {paramIn} : {_paramInBase}");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        if (_type == ScriptType.Popup && _hasOutput)
        {
            sb.AppendLine($"    public class {paramOut} : {_paramOutBase}");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        if (_type == ScriptType.Popup && _hasInput)
        {
            sb.AppendLine($"    public class {logic} : qtLogic<{paramIn}>");
        }
        else
        {
            sb.AppendLine($"    public class {logic} : qtLogic");
        }
        sb.AppendLine("    {");
        sb.AppendLine("        public override UniTask Initialize()");
        sb.AppendLine("        {");
        sb.AppendLine("            return base.Initialize();");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public class {mediator} : {medBase}");
        sb.AppendLine("    {");
        sb.AppendLine($"        public {mediator}() : base()");
        sb.AppendLine("        {");
        sb.AppendLine("            _configUI = (ui, logic, mediator) =>");
        sb.AppendLine("            {");
        if (_hasOutput)
        {
            sb.AppendLine("                ui.uiResult = new UniTaskCompletionSource<ParamOutput>();");
        }
        sb.AppendLine("                return UniTask.CompletedTask;");
        sb.AppendLine("            };");
        sb.AppendLine();
        sb.AppendLine("            _beforeUIShow = (ui, logic, mediator) =>");
        sb.AppendLine("            {");
        sb.AppendLine("                return UniTask.CompletedTask;");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        protected override void RemoveEvent()");
        sb.AppendLine("        {");
        sb.AppendLine("            base.RemoveEvent();");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        #region ----- Button Events -----");
        sb.AppendLine("        #endregion");
        sb.AppendLine();
        sb.AppendLine("        #region ----- Private Functions -----");
        sb.AppendLine("        #endregion");

        if (_hasInput)
        {
            sb.AppendLine($"        public override UniTask<{paramIn}> RequestData()");
            sb.AppendLine("        {");
            sb.AppendLine($"            return UniTask.FromResult(new {paramIn}());");
            sb.AppendLine("        }");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}