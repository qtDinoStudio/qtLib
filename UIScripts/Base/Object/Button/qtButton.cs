using System;
using NaughtyAttributes;
using qtLib.UI.Base;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.UI;

#endif

namespace qtLib.UIScripts.Base.Object.Button
{
    [RequireComponent(typeof(Image))]
    public class qtButton : UnityEngine.UI.Button
    {
        #region ----- Component Config -----

        [Serializable]
        protected class ButtonSetting
        {
            public Transition transition = Transition.ColorTint;
            
            [Space] 
            public Material normalMaterial;
            public Material grayScaleMaterial;
        
            public Color normalColor = Color.white;
            public Color grayScaleColor = Color.white;
        }
        
        [SerializeField] protected ButtonSetting _buttonSetting;
        private Image _image;
        private TextMeshProUGUI _text;
        private Image[] _imagesInChildren;
        
        private bool _isInitialized = false;

        #endregion
        
        #region ----- Unity Event -----

        protected override void Awake()
        {
            base.Awake();
            _Initialize();
        }

        #endregion

        #region ----- Public Function -----

        public virtual void SetInteractable(bool isInteractable, bool changeTextColor = true, bool changeImageColor = true, bool changeChildImage = true)
        {
            if (!_isInitialized)
            {
                _Initialize();
            }
            this.interactable = isInteractable;
            
            SetGrayScale(!isInteractable, changeTextColor, changeImageColor, changeChildImage);
        }

        public void SetGrayScale(bool isGrayScale, bool changeTextColor = true, bool changeImageColor = true, bool changeChildImage = true)
        {
            if (!_isInitialized)
            {
                _Initialize();
            }

            if (changeTextColor)
            {
                SetTextColor(isGrayScale ? _buttonSetting.grayScaleColor : _buttonSetting.normalColor);
            }

            if (changeImageColor)
            {
                _image.material = isGrayScale ? _buttonSetting.grayScaleMaterial : _buttonSetting.normalMaterial;
            }

            if (changeChildImage)
            {
                for (var i = 0; i < _imagesInChildren.Length; i++)
                {
                    _imagesInChildren[i].material = isGrayScale ? _buttonSetting.grayScaleMaterial : _buttonSetting.normalMaterial;
                }
            }
        }

        public virtual void SetEnable(bool isEnable)
        {
            if (!_isInitialized)
            {
                _Initialize();
            }

            this.enabled = isEnable;
        }

        public virtual void SetText(string text)
        {
            if (!_isInitialized)
            {
                _Initialize();
            }
            _text.SetText(text);
        }

        public virtual void SetTextColor(Color color)
        {
            if (!_isInitialized)
            {
                _Initialize();
            }
            _text.color = color;
        }
        
        #endregion

        #region ----- Private Function -----

        protected virtual void _Initialize()
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;
            
            _image = GetComponent<Image>();
            _text = GetComponentInChildren<TextMeshProUGUI>();

            _imagesInChildren = GetComponentsInChildren<Image>(true);
        }
        
        protected virtual void _PlaySfx()
        {
            // _PlaySfxSoundController.Instance.PlaySfx(SoundController.SoundID.Click,0.1f);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            _PlaySfx();
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (qtUiFlow.IsBusy)
            {
                return;
            }
            base.OnPointerClick(eventData);
        }

        #endregion


#if UNITY_EDITOR
        [HideInInspector] [SerializeField] private Material _editorGrayScaleMaterial; 
        
        [Button("Validate", EButtonEnableMode.Editor)]
        protected override void OnValidate()
        {
            if (_editorGrayScaleMaterial == null)
            {
                _editorGrayScaleMaterial = Resources.Load<Material>("UI_GrayscaleShader");
                _buttonSetting = new ButtonSetting
                {
                    grayScaleMaterial = _editorGrayScaleMaterial
                };
            }

            transition = _buttonSetting.transition;
            switch (_buttonSetting.transition)
            {
                case Transition.None:
                case Transition.ColorTint:
                case Transition.SpriteSwap:
                {
                    if (gameObject.TryGetComponent(out Animator temp))
                    {
                        DestroyImmediate(temp, true);
                    }
                    break;
                }
                case Transition.Animation:
                {
                    if (!gameObject.TryGetComponent(out Animator temp))
                    {
                        temp = gameObject.AddComponent<Animator>();
                    }
                
                    if (temp.runtimeAnimatorController == null)
                    {
                        temp.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Button_Animation");
                    }
                    break;
                }
            }
        }
        
        [CustomEditor(typeof(qtButton))]
        [CanEditMultipleObjects]
        public class qtButtonEditor : ButtonEditor
        {
            private SerializedProperty _buttonSetting;
            private MonoScript _script;
            private bool _showLegacyButton = true;

            protected override void OnEnable()
            {
                base.OnEnable();

                _buttonSetting = serializedObject.FindProperty("_buttonSetting");
                _script = MonoScript.FromMonoBehaviour((qtButton)target);
            }

            public override void OnInspectorGUI()
            {
                EditorGUI.BeginDisabledGroup(true);

                EditorGUILayout.ObjectField(
                    "Script",
                    _script,
                    typeof(MonoScript),
                    false);

                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space();
                
                serializedObject.Update();

                EditorGUILayout.LabelField("QT Button", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_buttonSetting, true);

                EditorGUILayout.Space();

                _showLegacyButton = EditorGUILayout.BeginFoldoutHeaderGroup(
                    _showLegacyButton,
                    "Legacy Button");

                if (_showLegacyButton)
                {
                    EditorGUILayout.Space();

                    // Inspector mặc định của Unity Button
                    base.OnInspectorGUI();
                }

                EditorGUILayout.EndFoldoutHeaderGroup();

                serializedObject.ApplyModifiedProperties();
            }
        }
#endif
    }
}
