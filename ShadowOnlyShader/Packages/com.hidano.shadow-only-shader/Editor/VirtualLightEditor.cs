using UnityEditor;
using UnityEngine;

namespace ShadowOnlyShader.Editor
{
    /// <summary>
    /// VirtualLightのカスタムInspector。
    /// SourceLight設定時にChromaticAberrationColorを非表示にする。
    /// SyncWithSourceLight有効時にProjectionパラメータとBiasを読み取り専用にする。
    /// TextureResolutionを2のべき乗ドロップダウンで表示する。
    /// </summary>
    [CustomEditor(typeof(VirtualLight))]
    public class VirtualLightEditor : UnityEditor.Editor
    {
        private SerializedProperty _sourceLight;
        private SerializedProperty _syncWithSourceLight;
        private SerializedProperty _chromaticAberrationColor;
        private SerializedProperty _projectionMode;
        private SerializedProperty _textureResolution;

        // 2のべき乗解像度の選択肢
        private static readonly int[] ResolutionValues = { 64, 128, 256, 512, 1024, 2048, 4096 };
        private static readonly GUIContent[] ResolutionLabels =
        {
            new GUIContent("64"),
            new GUIContent("128"),
            new GUIContent("256"),
            new GUIContent("512"),
            new GUIContent("1024"),
            new GUIContent("2048"),
            new GUIContent("4096"),
        };

        // Projection fields that are synced from SourceLight
        private static readonly string[] SyncedProjectionFields =
        {
            "_projectionMode",
            "_fieldOfView",
            "_orthographicSize",
            "_farClipPlane",
        };

        // Bias fields that are synced from SourceLight
        private static readonly string[] SyncedBiasFields =
        {
            "_depthBias",
            "_normalBias",
        };

        private void OnEnable()
        {
            _sourceLight = serializedObject.FindProperty("_sourceLight");
            _syncWithSourceLight = serializedObject.FindProperty("_syncWithSourceLight");
            _chromaticAberrationColor = serializedObject.FindProperty("_chromaticAberrationColor");
            _projectionMode = serializedObject.FindProperty("_projectionMode");
            _textureResolution = serializedObject.FindProperty("_textureResolution");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool hasSourceLight = _sourceLight.objectReferenceValue != null;
            bool wasSyncing = hasSourceLight && _syncWithSourceLight.boolValue;

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

                // SourceLightが未設定の場合、SyncWithSourceLightトグルを非表示
                if (iterator.propertyPath == "_syncWithSourceLight" && !hasSourceLight)
                {
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

                // Sync有効時、同期対象のProjectionフィールドは読み取り専用で表示
                if (wasSyncing && IsSyncedField(iterator.propertyPath))
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            // SyncWithSourceLightトグルの変更を検出し、Undo対応で保存/復元を行う
            bool isSyncing = hasSourceLight && _syncWithSourceLight.boolValue;
            if (wasSyncing != isSyncing)
            {
                var virtualLight = (VirtualLight)target;
                Undo.RecordObject(virtualLight, isSyncing ? "Enable SyncWithSourceLight" : "Disable SyncWithSourceLight");
                Undo.RecordObject(virtualLight.transform, isSyncing ? "Enable SyncWithSourceLight" : "Disable SyncWithSourceLight");

                if (isSyncing)
                {
                    virtualLight.SavePreSyncState();
                }
                else
                {
                    virtualLight.RestorePreSyncState();
                }
            }

            if (isSyncing)
            {
                EditorGUILayout.HelpBox(
                    "SourceLight から Transform・投影パラメータ・Bias を自動同期中です。グレーアウトされたパラメータは Light から取得されます。",
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
            // 完全一致を探す
            for (int i = 0; i < ResolutionValues.Length; i++)
            {
                if (ResolutionValues[i] == value) return i;
            }

            // 一致しない場合は最も近い値を選択
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
            foreach (var field in SyncedProjectionFields)
            {
                if (propertyPath == field) return true;
            }
            foreach (var field in SyncedBiasFields)
            {
                if (propertyPath == field) return true;
            }
            return false;
        }
    }
}
