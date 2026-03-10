using UnityEditor;
using UnityEngine;

namespace ShadowOnlyShader.Editor
{
    /// <summary>
    /// VirtualLightのカスタムInspector。
    /// SourceLight設定時にChromaticAberrationColorを非表示にする。
    /// </summary>
    [CustomEditor(typeof(VirtualLight))]
    public class VirtualLightEditor : UnityEditor.Editor
    {
        private SerializedProperty _sourceLight;
        private SerializedProperty _chromaticAberrationColor;

        private void OnEnable()
        {
            _sourceLight = serializedObject.FindProperty("_sourceLight");
            _chromaticAberrationColor = serializedObject.FindProperty("_chromaticAberrationColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 全プロパティを描画（ChromaticAberrationColorを除く）
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

                // SourceLightが設定されている場合、ChromaticAberrationColorを非表示
                if (iterator.propertyPath == "_chromaticAberrationColor"
                    && _sourceLight.objectReferenceValue != null)
                {
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
