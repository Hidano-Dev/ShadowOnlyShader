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
    }
}
