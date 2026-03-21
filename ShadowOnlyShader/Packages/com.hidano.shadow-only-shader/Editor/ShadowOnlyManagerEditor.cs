using UnityEditor;
using UnityEngine;

namespace ShadowOnlyShader.Editor
{
    /// <summary>
    /// ShadowOnlyManagerのカスタムInspector。
    /// 仮想光源の追加・削除UIを提供する。
    /// </summary>
    [CustomEditor(typeof(ShadowOnlyManager))]
    public class ShadowOnlyManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _blurQuality;
        private SerializedProperty _blendMultiplier;
        private SerializedProperty _blurResolutionScale;
        private SerializedProperty _floorRenderers;

        private bool _showAdvanced;

        private void OnEnable()
        {
            _blurQuality = serializedObject.FindProperty("_blurQuality");
            _blendMultiplier = serializedObject.FindProperty("_blendMultiplier");
            _blurResolutionScale = serializedObject.FindProperty("_blurResolutionScale");
            _floorRenderers = serializedObject.FindProperty("_floorRenderers");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var manager = (ShadowOnlyManager)target;

            // 仮想光源セクション
            EditorGUILayout.LabelField("仮想光源", EditorStyles.boldLabel);

            // 子VirtualLightの一覧表示
            var virtualLights = manager.GetComponentsInChildren<VirtualLight>();
            if (virtualLights.Length == 0)
            {
                EditorGUILayout.HelpBox("仮想光源がありません。下のボタンから追加してください。", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < virtualLights.Length; i++)
                {
                    var vl = virtualLights[i];
                    if (vl == null) continue;

                    EditorGUILayout.BeginHorizontal();

                    // クリックで選択できるオブジェクトフィールド
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(vl.gameObject, typeof(GameObject), true);
                    EditorGUI.EndDisabledGroup();

                    if (GUILayout.Button("選択", GUILayout.Width(40)))
                    {
                        Selection.activeGameObject = vl.gameObject;
                    }

                    if (GUILayout.Button("複製", GUILayout.Width(40)))
                    {
                        var clone = Instantiate(vl.gameObject, manager.transform);
                        clone.name = vl.gameObject.name;
                        Undo.RegisterCreatedObjectUndo(clone, "Duplicate VirtualLight");
                        manager.RefreshVirtualLights();
                        Selection.activeGameObject = clone;
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button("削除", GUILayout.Width(40)))
                    {
                        Undo.DestroyObjectImmediate(vl.gameObject);
                        manager.RefreshVirtualLights();
                        GUIUtility.ExitGUI();
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("仮想光源を追加"))
            {
                var go = new GameObject("VirtualLight");
                Undo.RegisterCreatedObjectUndo(go, "Add VirtualLight");
                go.transform.SetParent(manager.transform);
                go.transform.localPosition = new Vector3(0f, 5f, 0f);
                go.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
                go.AddComponent<VirtualLight>();
                manager.RefreshVirtualLights();
                Selection.activeGameObject = go;
            }

            EditorGUILayout.Space(8);

            // 床面Renderer
            EditorGUILayout.PropertyField(_floorRenderers, new GUIContent("床面Renderer"));

            EditorGUILayout.Space(8);

            EditorGUILayout.PropertyField(_blurQuality);

            EditorGUILayout.Space(8);

            // 詳細設定（折りたたみ）
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "詳細設定", true);
            if (_showAdvanced)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_blendMultiplier);
                EditorGUILayout.PropertyField(_blurResolutionScale,
                    new GUIContent("ブラー解像度スケール",
                        "影のブラー計算の解像度。低い値ほど軽量ですが影がぼやけます。" +
                        "通常は 0.5（デフォルト）で十分です。"));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("GameObject/Shadow Only/Shadow Only Manager", false, 10)]
        private static void CreateShadowOnlyManager()
        {
            var go = new GameObject("ShadowOnlyManager");
            Undo.RegisterCreatedObjectUndo(go, "Create ShadowOnlyManager");
            go.AddComponent<ShadowOnlyManager>();
            Selection.activeGameObject = go;
        }
    }
}
