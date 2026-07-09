using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShadowOnlyShader.Editor
{
    /// <summary>
    /// ShadowOnlyManagerのカスタムInspector。
    /// 仮想光源の追加・削除UIと、影が表示されない原因の診断表示を提供する。
    /// </summary>
    [CustomEditor(typeof(ShadowOnlyManager))]
    public class ShadowOnlyManagerEditor : UnityEditor.Editor
    {
        /// <summary>診断の自動再実行間隔（秒）。Inspector再描画のたびに走らないよう間引く。</summary>
        private const double DiagnosticsIntervalSeconds = 2.0;

        private SerializedProperty _blurQuality;
        private SerializedProperty _blendMultiplier;
        private SerializedProperty _blurResolutionScale;
        private SerializedProperty _adaptiveResolution;
        private SerializedProperty _adaptiveResolutionMinScale;
        private SerializedProperty _floorRenderers;

        private bool _showAdvanced;
        private bool _showDiagnostics = true;
        private List<DiagnosticIssue> _diagnostics;
        private double _lastDiagnosticsTime;

        private void OnEnable()
        {
            _blurQuality = serializedObject.FindProperty("_blurQuality");
            _blendMultiplier = serializedObject.FindProperty("_blendMultiplier");
            _blurResolutionScale = serializedObject.FindProperty("_blurResolutionScale");
            _adaptiveResolution = serializedObject.FindProperty("_adaptiveResolution");
            _adaptiveResolutionMinScale = serializedObject.FindProperty("_adaptiveResolutionMinScale");
            _floorRenderers = serializedObject.FindProperty("_floorRenderers");

            // Inspector表示時に診断を即実行する
            _diagnostics = null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var manager = (ShadowOnlyManager)target;

            // 診断セクション（影が表示されない原因の検出）
            DrawDiagnostics(manager);

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
            EditorGUILayout.PropertyField(_floorRenderers, new GUIContent("床面Renderer",
                "影が映り込む床面のRendererを指定します。ここに登録されたオブジェクトの表面に影が描画されます。"));

            if (GUILayout.Button("床面を自動生成"))
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "ShadowOnlyFloor";
                Undo.RegisterCreatedObjectUndo(floor, "Create Shadow Only Floor");
                floor.transform.SetParent(manager.transform);
                floor.transform.localPosition = new Vector3(0f, -0.005f, 0f);
                floor.transform.localScale = new Vector3(100f, 0.01f, 100f);

                var renderer = floor.GetComponent<Renderer>();
                manager.AddFloorRenderer(renderer);
                EditorUtility.SetDirty(manager);
                Selection.activeGameObject = floor;
            }

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

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(_adaptiveResolution,
                    new GUIContent("適応解像度",
                        "カメラ距離に応じてResolve RTの解像度を自動調整します。" +
                        "遠景時のGPU負荷を大幅に削減します。"));

                if (_adaptiveResolution.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_adaptiveResolutionMinScale,
                        new GUIContent("最小スケール",
                            "適応解像度の下限。ブラー解像度スケールにこの値を掛けた解像度まで縮小されます。" +
                            "例: ブラー解像度0.5 × 最小スケール0.1 = 最小で元の5%解像度"));
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 診断セクションを描画する。
        /// 「影が表示されない」原因になり得る設定・状態を検出し、HelpBoxで一覧表示する。
        /// 診断は一定間隔で自動再実行され、「再診断」ボタンで即時更新もできる。
        /// </summary>
        private void DrawDiagnostics(ShadowOnlyManager manager)
        {
            double now = EditorApplication.timeSinceStartup;
            if (_diagnostics == null || now - _lastDiagnosticsTime > DiagnosticsIntervalSeconds)
            {
                _diagnostics = ShadowOnlyDiagnostics.Run(manager);
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
                    _diagnostics = ShadowOnlyDiagnostics.Run(manager);
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
