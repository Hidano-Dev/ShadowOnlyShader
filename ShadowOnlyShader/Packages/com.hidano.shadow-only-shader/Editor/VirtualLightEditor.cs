using UnityEditor;
using UnityEngine;

namespace ShadowOnlyShader.Editor
{
    /// <summary>
    /// VirtualLightのカスタムInspector。
    /// SourceLight設定時にChromaticAberrationColorを非表示にする。
    /// SyncWithSourceLight有効時にProjectionパラメータを読み取り専用にする。
    /// </summary>
    [CustomEditor(typeof(VirtualLight))]
    public class VirtualLightEditor : UnityEditor.Editor
    {
        private SerializedProperty _sourceLight;
        private SerializedProperty _syncWithSourceLight;
        private SerializedProperty _chromaticAberrationColor;

        // Projection fields that are synced from SourceLight
        private static readonly string[] SyncedProjectionFields =
        {
            "_projectionMode",
            "_fieldOfView",
            "_orthographicSize",
            "_farClipPlane",
        };

        private void OnEnable()
        {
            _sourceLight = serializedObject.FindProperty("_sourceLight");
            _syncWithSourceLight = serializedObject.FindProperty("_syncWithSourceLight");
            _chromaticAberrationColor = serializedObject.FindProperty("_chromaticAberrationColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool hasSourceLight = _sourceLight.objectReferenceValue != null;
            bool isSyncing = hasSourceLight && _syncWithSourceLight.boolValue;

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

                // SourceLightが設定されている場合、ChromaticAberrationColorを非表示
                if (iterator.propertyPath == "_chromaticAberrationColor" && hasSourceLight)
                {
                    continue;
                }

                // Sync有効時、同期対象のProjectionフィールドは読み取り専用で表示
                if (isSyncing && IsSyncedField(iterator.propertyPath))
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            if (isSyncing)
            {
                EditorGUILayout.HelpBox(
                    "SourceLight から Transform と投影パラメータを自動同期中です。グレーアウトされたパラメータは Light から取得されます。",
                    MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static bool IsSyncedField(string propertyPath)
        {
            foreach (var field in SyncedProjectionFields)
            {
                if (propertyPath == field) return true;
            }
            return false;
        }
    }
}
