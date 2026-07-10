using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShadowOnlyShader.Editor
{
    /// <summary>
    /// VirtualLightのカスタムInspector。
    /// Inspector上部にこの光源の個別診断（CasterRoot・投影範囲・ShadowAlpha等）を表示する。
    /// SourceLight設定時に同期対象フィールドを非表示にし、ChromaticAberrationColorも非表示にする。
    /// TextureResolutionを2のべき乗ドロップダウン（URP Default付き）で表示する。
    /// オプション項目はフォールドアウトで畳んで表示する。
    /// </summary>
    [CustomEditor(typeof(VirtualLight))]
    [CanEditMultipleObjects]
    public class VirtualLightEditor : UnityEditor.Editor
    {
        /// <summary>診断の自動再実行間隔（秒）。Inspector再描画のたびに走らないよう間引く。</summary>
        private const double DiagnosticsIntervalSeconds = 2.0;

        private SerializedProperty _casterRoot;
        private SerializedProperty _sourceLight;
        private SerializedProperty _chromaticAberrationColor;
        private SerializedProperty _projectionMode;
        private SerializedProperty _fitToCasters;
        private SerializedProperty _textureResolution;

        // Shadow Color / Hue Shift
        private SerializedProperty _shadowColor;
        private SerializedProperty _hueShift;

        // Depth Bias
        private SerializedProperty _depthBias;
        private SerializedProperty _normalBias;

        // Blur
        private SerializedProperty _blurRadius;
        private SerializedProperty _blurDistanceFactor;
        private SerializedProperty _blurCameraDistanceFactor;
        private SerializedProperty _alphaCameraDistanceFactor;
        private SerializedProperty _cameraDistancePower;

        // Effects
        private SerializedProperty _chromaticAberration;
        private SerializedProperty _contactHardeningStrength;
        private SerializedProperty _contactDarkeningStrength;
        private SerializedProperty _contactDarkeningRange;

        private static Texture2D _hueSpectrumTexture;

        // Foldout states
        private bool _shadowColorFoldout;
        private bool _depthBiasFoldout;
        private bool _blurFoldout;
        private bool _effectsFoldout;

        // 診断表示の状態
        private bool _showDiagnostics = true;
        private List<DiagnosticIssue> _diagnostics;
        private double _lastDiagnosticsTime;

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
            "_farClipPlane",
            "_shadowAlpha",
        };

        // フォールドアウト内で描画するためイテレーターではスキップするフィールド
        private static readonly string[] FoldoutFields =
        {
            "_shadowColor",
            "_hueShift",
            "_depthBias",
            "_normalBias",
            "_blurRadius",
            "_blurDistanceFactor",
            "_blurCameraDistanceFactor",
            "_alphaCameraDistanceFactor",
            "_cameraDistancePower",
            "_chromaticAberration",
            "_contactHardeningStrength",
            "_contactDarkeningStrength",
            "_contactDarkeningRange",
        };

        private void OnEnable()
        {
            _casterRoot = serializedObject.FindProperty("_casterRoot");
            _sourceLight = serializedObject.FindProperty("_sourceLight");
            _chromaticAberrationColor = serializedObject.FindProperty("_chromaticAberrationColor");
            _projectionMode = serializedObject.FindProperty("_projectionMode");
            _fitToCasters = serializedObject.FindProperty("_fitToCasters");
            _textureResolution = serializedObject.FindProperty("_textureResolution");

            _shadowColor = serializedObject.FindProperty("_shadowColor");
            _hueShift = serializedObject.FindProperty("_hueShift");

            _depthBias = serializedObject.FindProperty("_depthBias");
            _normalBias = serializedObject.FindProperty("_normalBias");

            _blurRadius = serializedObject.FindProperty("_blurRadius");
            _blurDistanceFactor = serializedObject.FindProperty("_blurDistanceFactor");
            _blurCameraDistanceFactor = serializedObject.FindProperty("_blurCameraDistanceFactor");
            _alphaCameraDistanceFactor = serializedObject.FindProperty("_alphaCameraDistanceFactor");
            _cameraDistancePower = serializedObject.FindProperty("_cameraDistancePower");

            _chromaticAberration = serializedObject.FindProperty("_chromaticAberration");
            _contactHardeningStrength = serializedObject.FindProperty("_contactHardeningStrength");
            _contactDarkeningStrength = serializedObject.FindProperty("_contactDarkeningStrength");
            _contactDarkeningRange = serializedObject.FindProperty("_contactDarkeningRange");

            // Inspector表示時に診断を即実行する
            _diagnostics = null;

            EnsureHueSpectrumTexture();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 診断セクション（この光源が影を表示できない原因の検出）
            // マルチ選択時は対象が曖昧になるため表示しない
            if (!serializedObject.isEditingMultipleObjects)
            {
                DrawDiagnostics((VirtualLight)target);
            }

            // マルチオブジェクト対応: いずれかがSourceLightを持つか、値が混在しているかを判定
            bool hasSourceLight = _sourceLight.objectReferenceValue != null;
            bool mixedSourceLight = _sourceLight.hasMultipleDifferentValues;
            bool anyHasSourceLight = hasSourceLight || mixedSourceLight;

            // 実効的な投影モードを算出（SourceLight設定時はLight.typeから、未設定時は_projectionModeから）
            // Edit モードでは SyncFromSourceLight が走らないため、
            // Light.type と _projectionMode が乖離する場合がある。
            // 表示制御には常にこの effectiveProjectionMode を使用する。
            // マルチ選択時で投影モードが混在する場合はnullとし、両方のフィールドを表示する。
            ProjectionMode? effectiveProjectionMode;
            if (serializedObject.isEditingMultipleObjects)
            {
                // 全オブジェクトの実効モードを調べ、統一されていればそれを使用
                ProjectionMode? unified = null;
                bool allSame = true;
                foreach (var t in targets)
                {
                    var vl = (VirtualLight)t;
                    var vlSo = new SerializedObject(vl);
                    var vlSourceLight = vlSo.FindProperty("_sourceLight");
                    var vlProjectionMode = vlSo.FindProperty("_projectionMode");
                    ProjectionMode mode;
                    if (vlSourceLight.objectReferenceValue != null)
                    {
                        mode = ProjectionModeFromLight(vlSourceLight.objectReferenceValue as Light);
                    }
                    else
                    {
                        mode = (ProjectionMode)vlProjectionMode.enumValueIndex;
                    }
                    vlSo.Dispose();

                    if (unified == null)
                    {
                        unified = mode;
                    }
                    else if (unified != mode)
                    {
                        allSame = false;
                        break;
                    }
                }
                effectiveProjectionMode = allSame ? unified : null;
            }
            else if (hasSourceLight)
            {
                effectiveProjectionMode = ProjectionModeFromLight(_sourceLight.objectReferenceValue as Light);
            }
            else
            {
                effectiveProjectionMode = (ProjectionMode)_projectionMode.enumValueIndex;
            }

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

                // CasterRootは上書き指定であることが分かるよう、未設定時にManagerの共通設定を表示する
                if (iterator.propertyPath == "_casterRoot")
                {
                    EditorGUILayout.PropertyField(iterator);
                    DrawCasterRootHint();
                    continue;
                }

                // FieldOfViewはPerspective時のみ、OrthographicSizeはOrthographic時のみ表示する
                // （PointはFOV 90°固定×6面のため両方とも非表示）。
                // 投影モードが混在する場合は両方表示する
                if (iterator.propertyPath == "_fieldOfView"
                    && effectiveProjectionMode != null
                    && effectiveProjectionMode != ProjectionMode.Perspective)
                {
                    continue;
                }

                // Fit To CastersはOrthographic時のみ表示する
                if (iterator.propertyPath == "_fitToCasters"
                    && effectiveProjectionMode != null
                    && effectiveProjectionMode != ProjectionMode.Orthographic)
                {
                    continue;
                }

                if (iterator.propertyPath == "_orthographicSize")
                {
                    if (effectiveProjectionMode != null
                        && effectiveProjectionMode != ProjectionMode.Orthographic)
                    {
                        continue;
                    }

                    // Fit To Casters有効時はOrthographic Sizeが使用されないためグレーアウトする
                    // （マルチ選択で値が混在する場合は編集可能のままにする）
                    bool fitEnabled = _fitToCasters.boolValue && !_fitToCasters.hasMultipleDifferentValues;
                    using (new EditorGUI.DisabledScope(fitEnabled))
                    {
                        EditorGUILayout.PropertyField(iterator);
                    }
                    continue;
                }

                // SourceLightが設定されている場合、ChromaticAberrationColorを非表示
                // マルチ選択時はいずれかがSourceLightを持つ場合に非表示
                if (iterator.propertyPath == "_chromaticAberrationColor" && anyHasSourceLight)
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
                // マルチ選択時はいずれかがSourceLightを持つ場合に非表示
                if (anyHasSourceLight && IsSyncedField(iterator.propertyPath))
                {
                    continue;
                }

                // フォールドアウト対象フィールドはグループの先頭で一括描画、それ以外はスキップ
                if (IsFoldoutField(iterator.propertyPath))
                {
                    // 各グループの先頭フィールドでセクションを描画
                    if (iterator.propertyPath == "_shadowColor")
                        DrawShadowColorSection();
                    else if (iterator.propertyPath == "_depthBias")
                        DrawDepthBiasSection();
                    else if (iterator.propertyPath == "_blurRadius")
                        DrawBlurSection();
                    else if (iterator.propertyPath == "_chromaticAberration")
                        DrawEffectsSection();
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            // SourceLight着脱の検出（マルチオブジェクト対応）
            Light newLight = _sourceLight.objectReferenceValue as Light;
            bool wasSet = prevLight != null;
            bool isSet = newLight != null;

            if (wasSet != isSet)
            {
                foreach (var t in targets)
                {
                    var virtualLight = (VirtualLight)t;
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
            }

            if (effectiveProjectionMode == ProjectionMode.Point)
            {
                EditorGUILayout.HelpBox(
                    "Point モード: FOV 90°×6面（±X/±Y/±Z）で全方向に影を投影します。" +
                    "深度テクスチャを6スライス使用します" +
                    $"（同時描画上限は{ShadowOnlyManager.MaxVirtualLights}スライス）。",
                    MessageType.Info);
            }

            if (anyHasSourceLight)
            {
                EditorGUILayout.HelpBox(
                    "SourceLight から Transform・投影パラメータ・ShadowAlpha を自動同期中です。",
                    MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }

        #region Diagnostics

        /// <summary>
        /// この光源の個別診断セクションを描画する。
        /// ShadowOnlyManagerEditorの診断と同じスタイルで、一定間隔の自動再実行と
        /// 「再診断」ボタンによる即時更新に対応する。
        /// </summary>
        private void DrawDiagnostics(VirtualLight light)
        {
            double now = EditorApplication.timeSinceStartup;
            if (_diagnostics == null || now - _lastDiagnosticsTime > DiagnosticsIntervalSeconds)
            {
                _diagnostics = ShadowOnlyDiagnostics.RunForLight(light);
                _lastDiagnosticsTime = now;
            }

            int errorCount = 0;
            int warningCount = 0;
            foreach (var issue in _diagnostics)
            {
                if (issue.Severity == DiagnosticSeverity.Error) errorCount++;
                else if (issue.Severity == DiagnosticSeverity.Warning) warningCount++;
            }

            string label = (errorCount == 0 && warningCount == 0)
                ? "診断: 問題なし"
                : $"診断: エラー {errorCount}件 / 警告 {warningCount}件";

            _showDiagnostics = EditorGUILayout.Foldout(_showDiagnostics, label, true, EditorStyles.foldoutHeader);
            if (_showDiagnostics)
            {
                if (_diagnostics.Count == 0)
                {
                    EditorGUILayout.HelpBox("問題は検出されませんでした。", MessageType.Info);
                }
                else
                {
                    foreach (var issue in _diagnostics)
                    {
                        EditorGUILayout.HelpBox(issue.Message, ToMessageType(issue.Severity));
                    }
                }

                if (GUILayout.Button("再診断"))
                {
                    _diagnostics = ShadowOnlyDiagnostics.RunForLight(light);
                    _lastDiagnosticsTime = now;
                }
            }

            EditorGUILayout.Space(8);
        }

        private static MessageType ToMessageType(DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Error: return MessageType.Error;
                case DiagnosticSeverity.Warning: return MessageType.Warning;
                default: return MessageType.Info;
            }
        }

        #endregion

        #region Caster Root Hint

        /// <summary>
        /// CasterRoot未設定時に、実際に使用されるManagerのDefault Caster Rootを表示する。
        /// どちらも未設定の場合のエラーは診断セクションが報告するため、ここでは扱わない。
        /// </summary>
        private void DrawCasterRootHint()
        {
            if (serializedObject.isEditingMultipleObjects) return;
            if (_casterRoot.objectReferenceValue != null) return;

            var light = (VirtualLight)target;
            var manager = light.GetComponentInParent<ShadowOnlyManager>(true);
            if (manager != null && manager.DefaultCasterRoot != null)
            {
                EditorGUILayout.HelpBox(
                    $"未設定のため、ShadowOnlyManagerのDefault Caster Root" +
                    $"「{manager.DefaultCasterRoot.name}」を使用します。",
                    MessageType.Info);
            }
        }

        #endregion

        #region Foldout Sections

        private void DrawShadowColorSection()
        {
            _shadowColorFoldout = EditorGUILayout.Foldout(_shadowColorFoldout, "Shadow Color / Hue Shift", true);
            if (!_shadowColorFoldout) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_shadowColor);
            DrawHueShiftSlider();
            EditorGUI.indentLevel--;
        }

        private void DrawDepthBiasSection()
        {
            _depthBiasFoldout = EditorGUILayout.Foldout(_depthBiasFoldout, "Depth Bias", true);
            if (!_depthBiasFoldout) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_depthBias);
            EditorGUILayout.PropertyField(_normalBias);
            EditorGUI.indentLevel--;
        }

        private void DrawBlurSection()
        {
            _blurFoldout = EditorGUILayout.Foldout(_blurFoldout, "Blur", true);
            if (!_blurFoldout) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_blurRadius);
            EditorGUILayout.PropertyField(_blurDistanceFactor);
            EditorGUILayout.PropertyField(_blurCameraDistanceFactor);
            EditorGUILayout.PropertyField(_alphaCameraDistanceFactor);
            EditorGUILayout.PropertyField(_cameraDistancePower);
            EditorGUI.indentLevel--;
        }

        private void DrawEffectsSection()
        {
            _effectsFoldout = EditorGUILayout.Foldout(_effectsFoldout, "Effects", true);
            if (!_effectsFoldout) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_chromaticAberration);
            EditorGUILayout.PropertyField(_contactHardeningStrength);
            EditorGUILayout.PropertyField(_contactDarkeningStrength);
            if (_contactDarkeningStrength.floatValue > 0f || _contactDarkeningStrength.hasMultipleDifferentValues)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_contactDarkeningRange);
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
        }

        #endregion

        #region Resolution Popup

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

        #endregion

        #region Hue Shift Slider

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

        #endregion

        #region Utilities

        /// <summary>
        /// Unity Lightの種類から実効的な投影モードを求める。
        /// SyncFromSourceLightの同期規則（Directional→Orthographic /
        /// Spot→Perspective / Point→Point）と一致させる。
        /// </summary>
        private static ProjectionMode ProjectionModeFromLight(Light light)
        {
            if (light == null) return ProjectionMode.Perspective;

            switch (light.type)
            {
                case LightType.Directional: return ProjectionMode.Orthographic;
                case LightType.Point: return ProjectionMode.Point;
                default: return ProjectionMode.Perspective;
            }
        }

        private static bool IsSyncedField(string propertyPath)
        {
            foreach (var field in SyncedFields)
            {
                if (propertyPath == field) return true;
            }
            return false;
        }

        private static bool IsFoldoutField(string propertyPath)
        {
            foreach (var field in FoldoutFields)
            {
                if (propertyPath == field) return true;
            }
            return false;
        }

        private static void DrawRectBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);               // top
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);       // bottom
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);               // left
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);       // right
        }

        #endregion
    }
}
