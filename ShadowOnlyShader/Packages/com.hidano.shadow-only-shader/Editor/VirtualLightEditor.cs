using UnityEditor;
using UnityEngine;

namespace ShadowOnlyShader.Editor
{
    /// <summary>
    /// VirtualLightのカスタムInspector。
    /// SourceLight設定時に同期対象フィールドを非表示にし、ChromaticAberrationColorも非表示にする。
    /// TextureResolutionを2のべき乗ドロップダウン（URP Default付き）で表示する。
    /// </summary>
    [CustomEditor(typeof(VirtualLight))]
    public class VirtualLightEditor : UnityEditor.Editor
    {
        private SerializedProperty _sourceLight;
        private SerializedProperty _chromaticAberrationColor;
        private SerializedProperty _projectionMode;
        private SerializedProperty _textureResolution;
        private SerializedProperty _shadowColor;
        private SerializedProperty _hueShift;

        private static Texture2D _hueSpectrumTexture;
        private bool _shadowColorFoldout;

        // 解像度ドロップダウンの選択肢（0 = URP Default）
        private static readonly int[] ResolutionValues = { 0, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
        private static readonly GUIContent[] ResolutionLabels =
        {
            new GUIContent("URP Default"),
            new GUIContent("64"),
            new GUIContent("128"),
            new GUIContent("256"),
            new GUIContent("512"),
            new GUIContent("1024"),
            new GUIContent("2048"),
            new GUIContent("4096"),
            new GUIContent("8192"),
        };

        // SourceLight設定時に非表示にするフィールド
        private static readonly string[] SyncedFields =
        {
            "_projectionMode",
            "_fieldOfView",
            "_orthographicSize",
            "_farClipPlane",
            "_shadowAlpha",
        };

        private void OnEnable()
        {
            _sourceLight = serializedObject.FindProperty("_sourceLight");
            _chromaticAberrationColor = serializedObject.FindProperty("_chromaticAberrationColor");
            _projectionMode = serializedObject.FindProperty("_projectionMode");
            _textureResolution = serializedObject.FindProperty("_textureResolution");
            _shadowColor = serializedObject.FindProperty("_shadowColor");
            _hueShift = serializedObject.FindProperty("_hueShift");
            EnsureHueSpectrumTexture();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool hasSourceLight = _sourceLight.objectReferenceValue != null;

            // SourceLight着脱の検出（Undo対応で保存/復元を行う）
            Light prevLight = _sourceLight.objectReferenceValue as Light;

            // 全プロパティを描画
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                // Script フィールドは無効化して表示
                if (iterator.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(iterator);
                    }
                    continue;
                }

                // Orthographicモード時、FieldOfViewを非表示
                if (iterator.propertyPath == "_fieldOfView"
                    && _projectionMode.enumValueIndex == (int)ProjectionMode.Orthographic)
                {
                    continue;
                }

                // ShadowColor + HueShift をフォールドアウトにまとめて表示
                if (iterator.propertyPath == "_shadowColor")
                {
                    DrawShadowColorSection();
                    continue;
                }

                // HueShift は ShadowColor セクション内で描画済み
                if (iterator.propertyPath == "_hueShift")
                {
                    continue;
                }

                // SourceLightが設定されている場合、ChromaticAberrationColorを非表示
                if (iterator.propertyPath == "_chromaticAberrationColor" && hasSourceLight)
                {
                    continue;
                }

                // TextureResolutionは2のべき乗ドロップダウンで表示
                if (iterator.propertyPath == "_textureResolution")
                {
                    DrawResolutionPopup();
                    continue;
                }

                // SourceLight設定時、同期対象フィールドを非表示
                if (hasSourceLight && IsSyncedField(iterator.propertyPath))
                {
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            // SourceLight着脱の検出
            Light newLight = _sourceLight.objectReferenceValue as Light;
            bool wasSet = prevLight != null;
            bool isSet = newLight != null;

            if (wasSet != isSet)
            {
                var virtualLight = (VirtualLight)target;
                Undo.RecordObject(virtualLight, isSet ? "Set SourceLight" : "Clear SourceLight");
                Undo.RecordObject(virtualLight.transform, isSet ? "Set SourceLight" : "Clear SourceLight");

                if (isSet)
                {
                    virtualLight.SavePreSyncState();
                }
                else
                {
                    virtualLight.RestorePreSyncState();
                }
            }

            if (hasSourceLight)
            {
                EditorGUILayout.HelpBox(
                    "SourceLight から Transform・投影パラメータ・ShadowAlpha を自動同期中です。",
                    MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawResolutionPopup()
        {
            int currentValue = _textureResolution.intValue;
            int selectedIndex = FindResolutionIndex(currentValue);

            var label = new GUIContent(_textureResolution.displayName, _textureResolution.tooltip);
            int newIndex = EditorGUILayout.Popup(label, selectedIndex, ResolutionLabels);

            if (newIndex >= 0 && newIndex < ResolutionValues.Length)
            {
                _textureResolution.intValue = ResolutionValues[newIndex];
            }
        }

        private static int FindResolutionIndex(int value)
        {
            for (int i = 0; i < ResolutionValues.Length; i++)
            {
                if (ResolutionValues[i] == value) return i;
            }

            // 一致しない場合は最も近い値を選択（0=URP Defaultは除外して比較）
            int bestIndex = 0;
            int bestDiff = int.MaxValue;
            for (int i = 0; i < ResolutionValues.Length; i++)
            {
                int diff = Mathf.Abs(ResolutionValues[i] - value);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        private static bool IsSyncedField(string propertyPath)
        {
            foreach (var field in SyncedFields)
            {
                if (propertyPath == field) return true;
            }
            return false;
        }

        private void DrawShadowColorSection()
        {
            _shadowColorFoldout = EditorGUILayout.Foldout(_shadowColorFoldout, "Shadow Color / Hue Shift", true);
            if (!_shadowColorFoldout) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_shadowColor);
            DrawHueShiftSlider();
            EditorGUI.indentLevel--;
        }

        private static void EnsureHueSpectrumTexture()
        {
            if (_hueSpectrumTexture != null) return;

            const int width = 256;
            _hueSpectrumTexture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color[width];
            for (int i = 0; i < width; i++)
            {
                float hue = (float)i / (width - 1);
                pixels[i] = Color.HSVToRGB(hue, 1f, 1f);
            }

            _hueSpectrumTexture.SetPixels(pixels);
            _hueSpectrumTexture.Apply();
        }

        private void DrawHueShiftSlider()
        {
            var label = new GUIContent(_hueShift.displayName, _hueShift.tooltip);
            Rect totalRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);

            // Label
            totalRect = EditorGUI.PrefixLabel(totalRect, label);

            // Color swatch (right side)
            const float swatchWidth = 24f;
            const float swatchMargin = 4f;
            Rect swatchRect = new Rect(
                totalRect.xMax - swatchWidth,
                totalRect.y,
                swatchWidth,
                totalRect.height);

            // Slider area (remaining space)
            Rect sliderRect = new Rect(
                totalRect.x,
                totalRect.y,
                totalRect.width - swatchWidth - swatchMargin,
                totalRect.height);

            // Draw gradient background
            if (_hueSpectrumTexture != null)
            {
                // Inset slightly to align with slider track
                Rect gradientRect = new Rect(
                    sliderRect.x + 2f,
                    sliderRect.y + 2f,
                    sliderRect.width - 4f,
                    sliderRect.height - 4f);
                GUI.DrawTexture(gradientRect, _hueSpectrumTexture);
            }

            // Draw slider on top (transparent background via style trick)
            EditorGUI.BeginChangeCheck();
            float newValue = GUI.HorizontalSlider(sliderRect, _hueShift.floatValue, 0f, 360f);
            if (EditorGUI.EndChangeCheck())
            {
                _hueShift.floatValue = newValue;
            }

            // Draw preview swatch
            float hueNormalized = _hueShift.floatValue / 360f;
            Color previewColor = Color.HSVToRGB(hueNormalized, 1f, 1f);
            EditorGUI.DrawRect(swatchRect, previewColor);

            // Swatch border
            Color borderColor = EditorGUIUtility.isProSkin
                ? new Color(0.1f, 0.1f, 0.1f)
                : new Color(0.6f, 0.6f, 0.6f);
            DrawRectBorder(swatchRect, borderColor);

            // Degree label below
            Rect degreeRect = new Rect(sliderRect.x, sliderRect.yMax, sliderRect.width, EditorGUIUtility.singleLineHeight);
            EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 0.5f);
            var smallStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter };
            GUI.Label(degreeRect, $"{_hueShift.floatValue:F0}°", smallStyle);
        }

        private static void DrawRectBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);               // top
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);       // bottom
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);               // left
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);       // right
        }
    }
}
