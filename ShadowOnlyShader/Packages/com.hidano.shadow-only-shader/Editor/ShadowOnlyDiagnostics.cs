using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShadowOnlyShader.Editor
{
    /// <summary>
    /// 診断結果の深刻度。
    /// </summary>
    internal enum DiagnosticSeverity
    {
        /// <summary>注意事項（影が出ない直接原因ではないが知っておくべき情報）。</summary>
        Info,

        /// <summary>影が表示されない・欠ける可能性がある設定。</summary>
        Warning,

        /// <summary>影が表示されない決定的な原因。</summary>
        Error,
    }

    /// <summary>
    /// 診断で検出された1件の問題。
    /// </summary>
    internal readonly struct DiagnosticIssue
    {
        public readonly DiagnosticSeverity Severity;
        public readonly string Message;

        public DiagnosticIssue(DiagnosticSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    /// <summary>
    /// 「影が表示されない」原因になり得る設定・状態を検出する診断機能。
    ///
    /// 表示先は2箇所に分かれる:
    /// - Run(manager): ShadowOnlyManagerEditorのInspectorに表示。
    ///   パイプライン系・環境系・Manager系・床面系の診断に加え、
    ///   各VirtualLightの個別問題は「どの光源に何件あるか」の集約1件のみを出す。
    /// - RunForLight(light): VirtualLightEditorのInspectorに表示。
    ///   その光源自身の個別診断（CasterRoot / 投影範囲 / ShadowAlpha /
    ///   DepthBias / Texture Resolution / Manager配下チェック）を出す。
    ///
    /// Manager側の検出対象:
    /// - パイプライン系: URP未使用 / Renderer FeatureがRendererに未登録・無効 /
    ///   Quality設定側URPアセットの差し替え漏れ（Graphics設定のみ変更の落とし穴）
    /// - 環境系: 必須シェーダーの欠落・非サポート / Texture2DArray・深度フォーマット非対応
    /// - Manager系: Manager無効 / 複数Manager / BlendMultiplier=0
    /// - VirtualLight系: 光源なし / 全て非アクティブ / 上限超過 / Manager配下にない光源 /
    ///   個別問題の集約 / 共有深度テクスチャの解像度過大（設定の出どころを明示）
    /// - 床面系: 未登録 / null要素 / 全て非表示 / 床がキャスターに含まれる（自己投影） /
    ///   床がどの光源の投影範囲にも入っていない / マテリアル上書き
    /// </summary>
    internal static class ShadowOnlyDiagnostics
    {
        /// <summary>Manager側の集約メッセージに列挙する光源名の最大数。</summary>
        private const int MaxAggregatedLightNames = 5;

        /// <summary>深度テクスチャ解像度の警告閾値。これ以上でメモリ・負荷警告を出す。</summary>
        private const int ResolutionWarningThreshold = 4096;

        /// <summary>DepthBiasの警告閾値。正規化深度[0,1]に対するバイアスとして大きすぎる値。</summary>
        private const float DepthBiasWarningThreshold = 0.1f;

        private const string DepthShaderName = "Hidden/ShadowOnly/DepthOnly";
        private const string FloorShaderName = "Hidden/ShadowOnlyShader/Floor";
        private const string FloorDisplayShaderName = "Hidden/ShadowOnlyShader/FloorDisplay";

        /// <summary>
        /// 指定したManagerに対して全診断を実行し、検出された問題を深刻度順（Error→Warning→Info）で返す。
        /// </summary>
        public static List<DiagnosticIssue> Run(ShadowOnlyManager manager)
        {
            var issues = new List<DiagnosticIssue>();
            if (manager == null)
            {
                return issues;
            }

            // プレハブアセット上ではシーン依存の診断ができない
            if (!manager.gameObject.scene.IsValid())
            {
                Add(issues, DiagnosticSeverity.Info,
                    "プレハブアセットのため診断は実行されません。シーンに配置した状態で確認してください。");
                return issues;
            }

            CheckPlatformSupport(issues);
            CheckShaders(manager, issues);
            CheckRenderPipeline(issues);
            CheckManagerState(manager, issues);

            // アクティブなVirtualLightと、その投影フラスタム平面を収集
            // （Pointモードの光源は6面分のフラスタムを持つ）
            var activeLights = new List<VirtualLight>();
            var lightFrustums = new List<Plane[][]>();
            CheckVirtualLights(manager, issues, activeLights, lightFrustums);
            CheckFloorRenderers(manager, issues, activeLights, lightFrustums);
            CheckOrphanVirtualLights(manager, issues);

            return SortBySeverity(issues);
        }

        /// <summary>
        /// 指定したVirtualLight単体の診断を実行し、検出された問題を深刻度順で返す。
        /// VirtualLightEditorのInspectorから呼び出される。
        /// メッセージは対象光源自身のInspectorに表示される前提で、光源名のプレフィックスは付けない。
        /// </summary>
        public static List<DiagnosticIssue> RunForLight(VirtualLight light)
        {
            var issues = new List<DiagnosticIssue>();
            if (light == null)
            {
                return issues;
            }

            // プレハブアセット上ではシーン依存の診断ができない
            if (!light.gameObject.scene.IsValid())
            {
                Add(issues, DiagnosticSeverity.Info,
                    "プレハブアセットのため診断は実行されません。シーンに配置した状態で確認してください。");
                return issues;
            }

            if (light.GetComponentInParent<ShadowOnlyManager>(true) == null)
            {
                Add(issues, DiagnosticSeverity.Error,
                    "ShadowOnlyManagerの子階層に配置されていないため、この光源は認識されません。" +
                    "ManagerのGameObjectの子に移動してください。");
            }

            if (!light.isActiveAndEnabled)
            {
                Add(issues, DiagnosticSeverity.Info,
                    "この仮想光源は非アクティブのため描画されません。");
            }

            // Editモードでは行列がLateUpdateで更新されないため、ここで明示的に計算する
            light.UpdateMatrices();
            CollectLightIssues(light, CalculateLightFrustums(light), issues);

            return SortBySeverity(issues);
        }

        /// <summary>
        /// 光源の全スライス分の投影フラスタム平面を計算する。
        /// Orthographic/Perspectiveは1面、Pointは6面を返す。
        /// 呼び出し前にUpdateMatrices()を実行しておくこと。
        /// </summary>
        private static Plane[][] CalculateLightFrustums(VirtualLight light)
        {
            int sliceCount = light.SliceCount;
            var frustums = new Plane[sliceCount][];
            for (int i = 0; i < sliceCount; i++)
            {
                frustums[i] = GeometryUtility.CalculateFrustumPlanes(
                    light.ProjectionMatrix * light.GetSliceViewMatrix(i));
            }
            return frustums;
        }

        /// <summary>
        /// Boundsが光源のいずれかのスライスのフラスタムと交差するかを判定する。
        /// </summary>
        private static bool IntersectsAnyFrustum(Plane[][] frustums, Bounds bounds)
        {
            foreach (var frustum in frustums)
            {
                if (GeometryUtility.TestPlanesAABB(frustum, bounds))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Error → Warning → Info の順に並べ替える（同一深刻度内は検出順を維持）。
        /// </summary>
        private static List<DiagnosticIssue> SortBySeverity(List<DiagnosticIssue> issues)
        {
            var sorted = new List<DiagnosticIssue>(issues.Count);
            for (var severity = DiagnosticSeverity.Error; severity >= DiagnosticSeverity.Info; severity--)
            {
                foreach (var issue in issues)
                {
                    if (issue.Severity == severity)
                    {
                        sorted.Add(issue);
                    }
                }
            }
            return sorted;
        }

        private static void Add(List<DiagnosticIssue> issues, DiagnosticSeverity severity, string message)
        {
            issues.Add(new DiagnosticIssue(severity, message));
        }

        #region 環境チェック

        /// <summary>
        /// GPU・グラフィックスAPIの機能サポートを確認する。
        /// </summary>
        private static void CheckPlatformSupport(List<DiagnosticIssue> issues)
        {
            if (!SystemInfo.supports2DArrayTextures)
            {
                Add(issues, DiagnosticSeverity.Error,
                    "この環境はTexture2DArrayをサポートしていません。" +
                    "深度テクスチャを作成できないため影は描画されません。");
            }

            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth))
            {
                Add(issues, DiagnosticSeverity.Error,
                    "この環境は深度RenderTextureフォーマットをサポートしていません。" +
                    "影の深度テクスチャを作成できません。");
            }
        }

        /// <summary>
        /// パッケージの必須シェーダーが存在し、現在の環境でコンパイル可能かを確認する。
        /// </summary>
        private static void CheckShaders(ShadowOnlyManager manager, List<DiagnosticIssue> issues)
        {
            CheckShader(issues, DepthShaderName, DiagnosticSeverity.Error,
                "深度描画ができないため影は描画されません。");
            CheckShader(issues, FloorShaderName, DiagnosticSeverity.Error,
                "床面に影を描画できません。");

            // FloorDisplayはResolve有効時（BlurResolutionScale < 1.0）のみ必須
            bool resolveActive = manager.BlurResolutionScale < 0.999f;
            CheckShader(issues, FloorDisplayShaderName,
                resolveActive ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                resolveActive
                    ? "Blur Resolution Scaleが1.0未満のため、このシェーダーがないと影が描画されません。"
                    : "Blur Resolution Scaleを1.0未満にした際に影が描画されなくなります。");
        }

        private static void CheckShader(
            List<DiagnosticIssue> issues, string shaderName, DiagnosticSeverity severity, string consequence)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Add(issues, severity,
                    $"シェーダー「{shaderName}」が見つかりません。{consequence}" +
                    "パッケージが正しくインポートされているか確認してください。");
            }
            else if (!shader.isSupported)
            {
                Add(issues, severity,
                    $"シェーダー「{shaderName}」がこの環境でサポートされていません" +
                    $"（コンパイルエラーの可能性があります）。{consequence}" +
                    "Consoleウィンドウのシェーダーエラーを確認してください。");
            }
        }

        #endregion

        #region レンダーパイプラインチェック

        /// <summary>
        /// アクティブなレンダーパイプラインがURPであり、
        /// ShadowOnlyRendererFeatureがRendererに登録・有効化されているかを確認する。
        /// Graphics設定のみ差し替えてQuality設定側が古いままの落とし穴も検出する。
        /// </summary>
        private static void CheckRenderPipeline(List<DiagnosticIssue> issues)
        {
            var activePipeline = GraphicsSettings.currentRenderPipeline;
            if (activePipeline == null)
            {
                Add(issues, DiagnosticSeverity.Error,
                    "レンダーパイプラインが設定されていません（Built-in Render Pipeline）。" +
                    "本ツールはURP専用です。Project SettingsのGraphicsでURPアセットを設定してください。");
                return;
            }

            var activeUrpAsset = activePipeline as UniversalRenderPipelineAsset;
            if (activeUrpAsset == null)
            {
                Add(issues, DiagnosticSeverity.Error,
                    $"アクティブなレンダーパイプライン「{activePipeline.name}」がURPではありません。" +
                    "本ツールはURP専用です。");
                return;
            }

            // Renderer Featureの登録状況を確認（内部フィールドのためリフレクションを使用。
            // 取得に失敗した場合はURPのバージョン差異とみなし、誤検知を避けるためスキップする）
            if (!TryGetFeatureState(activeUrpAsset, out bool found, out bool anyActive))
            {
                return;
            }

            if (!found)
            {
                // Quality設定側のアセットが優先されるため、Graphics設定だけ直しても
                // Featureが動かないケースを明示的に検出する
                var defaultAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                var qualityAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;

                if (qualityAsset != null && defaultAsset != null && qualityAsset != defaultAsset
                    && TryGetFeatureState(defaultAsset, out bool foundInDefault, out _) && foundInDefault)
                {
                    Add(issues, DiagnosticSeverity.Error,
                        $"現在のQualityレベルのURPアセット「{activeUrpAsset.name}」に" +
                        "ShadowOnlyRendererFeatureが登録されていません。" +
                        $"Graphics設定側の「{defaultAsset.name}」には登録されていますが、" +
                        "Qualityレベル側のアセットが優先されるため影は描画されません。" +
                        "Project Settings > Quality の Render Pipeline Asset を差し替えるか、" +
                        $"「{activeUrpAsset.name}」が参照するRendererにもFeatureを追加してください。");
                }
                else
                {
                    Add(issues, DiagnosticSeverity.Error,
                        $"使用中のURPアセット「{activeUrpAsset.name}」が参照するRendererに" +
                        "ShadowOnlyRendererFeatureが登録されていません。" +
                        "Renderer Dataアセットの「Add Renderer Feature」から追加してください。");
                }
            }
            else if (!anyActive)
            {
                Add(issues, DiagnosticSeverity.Error,
                    "ShadowOnlyRendererFeatureはRendererに登録されていますが、無効化されています。" +
                    "Renderer DataアセットでFeatureのチェックボックスを有効にしてください。");
            }
        }

        /// <summary>
        /// URPアセットが参照する全RendererからShadowOnlyRendererFeatureの登録状況を取得する。
        /// </summary>
        /// <returns>リフレクションでRendererリストを取得できた場合はtrue</returns>
        private static bool TryGetFeatureState(
            UniversalRenderPipelineAsset asset, out bool found, out bool anyActive)
        {
            found = false;
            anyActive = false;

            var field = typeof(UniversalRenderPipelineAsset).GetField(
                "m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                return false;
            }

            if (!(field.GetValue(asset) is ScriptableRendererData[] dataList))
            {
                return false;
            }

            foreach (var data in dataList)
            {
                if (data == null || data.rendererFeatures == null) continue;

                foreach (var feature in data.rendererFeatures)
                {
                    if (feature is ShadowOnlyRendererFeature)
                    {
                        found = true;
                        if (feature.isActive)
                        {
                            anyActive = true;
                        }
                    }
                }
            }

            return true;
        }

        #endregion

        #region Manager状態チェック

        /// <summary>
        /// Manager自体の状態（有効状態・重複・グローバルパラメータ）を確認する。
        /// </summary>
        private static void CheckManagerState(ShadowOnlyManager manager, List<DiagnosticIssue> issues)
        {
            if (!manager.isActiveAndEnabled)
            {
                Add(issues, DiagnosticSeverity.Error,
                    "ShadowOnlyManagerが無効です。GameObjectをアクティブにし、" +
                    "コンポーネントを有効にしてください。");
            }

            var managers = Object.FindObjectsByType<ShadowOnlyManager>(FindObjectsSortMode.None);
            if (managers.Length > 1)
            {
                Add(issues, DiagnosticSeverity.Warning,
                    $"シーン内に複数のShadowOnlyManagerが存在します（{managers.Length}個）。" +
                    "最初に見つかった1つのみが使用されるため、意図しないManagerが動作している可能性があります。");
            }

            if (manager.BlendMultiplier <= 0f)
            {
                Add(issues, DiagnosticSeverity.Error,
                    "Blend Multiplierが0のため、すべての影が完全に透明になっています。" +
                    "0より大きい値（標準は1）を設定してください。");
            }
        }

        #endregion

        #region VirtualLightチェック

        /// <summary>
        /// VirtualLightの構成（存在・アクティブ状態・上限超過）を確認し、
        /// 各光源の個別問題は件数を集約して1件のIssueとして報告する。
        /// アクティブな光源と投影フラスタムを収集し、床面チェックでも再利用する。
        /// </summary>
        private static void CheckVirtualLights(
            ShadowOnlyManager manager,
            List<DiagnosticIssue> issues,
            List<VirtualLight> activeLights,
            List<Plane[][]> lightFrustums)
        {
            var allLights = manager.GetComponentsInChildren<VirtualLight>(true);

            if (allLights.Length == 0)
            {
                Add(issues, DiagnosticSeverity.Error,
                    "仮想光源（VirtualLight）が1つもありません。" +
                    "Inspectorの「仮想光源を追加」ボタンから追加してください。");
                return;
            }

            foreach (var vl in allLights)
            {
                if (vl != null && vl.isActiveAndEnabled)
                {
                    activeLights.Add(vl);
                }
            }

            if (activeLights.Count == 0)
            {
                Add(issues, DiagnosticSeverity.Error,
                    $"仮想光源が{allLights.Length}個ありますが、すべて非アクティブです。" +
                    "少なくとも1つの仮想光源を有効にしてください。");
                return;
            }

            // 深度スライスの消費量で上限を判定する（Pointモードの光源は6スライス消費）
            int totalSlices = 0;
            int pointLightCount = 0;
            foreach (var vl in activeLights)
            {
                totalSlices += vl.SliceCount;
                if (vl.ProjectionMode == ProjectionMode.Point)
                {
                    pointLightCount++;
                }
            }

            if (totalSlices > ShadowOnlyManager.MaxVirtualLights)
            {
                string pointHint = pointLightCount > 0
                    ? $"（Pointモードの光源{pointLightCount}個はそれぞれ6スライスを消費します）"
                    : "";
                Add(issues, DiagnosticSeverity.Warning,
                    $"アクティブな仮想光源が合計{totalSlices}スライス分あります{pointHint}。" +
                    $"同時に描画できるのは{ShadowOnlyManager.MaxVirtualLights}スライスまでのため、" +
                    "上限に収まらない光源は描画されません。");
            }

            // 各光源の個別診断はVirtualLight側のInspectorに表示するため、
            // ここではError/Warningを持つ光源の集約結果のみを1件で出す
            int problemLightCount = 0;
            int totalErrors = 0;
            int totalWarnings = 0;
            var problemNames = new List<string>();
            var maxSeverity = DiagnosticSeverity.Info;
            var lightIssues = new List<DiagnosticIssue>();

            foreach (var vl in activeLights)
            {
                // Editモードでは行列がLateUpdateで更新されないため、ここで明示的に計算する
                vl.UpdateMatrices();
                var frustums = CalculateLightFrustums(vl);
                lightFrustums.Add(frustums);

                lightIssues.Clear();
                CollectLightIssues(vl, frustums, lightIssues);

                bool hasProblem = false;
                foreach (var issue in lightIssues)
                {
                    if (issue.Severity == DiagnosticSeverity.Error)
                    {
                        totalErrors++;
                        hasProblem = true;
                        maxSeverity = DiagnosticSeverity.Error;
                    }
                    else if (issue.Severity == DiagnosticSeverity.Warning)
                    {
                        totalWarnings++;
                        hasProblem = true;
                        if (maxSeverity < DiagnosticSeverity.Warning)
                        {
                            maxSeverity = DiagnosticSeverity.Warning;
                        }
                    }
                }

                if (hasProblem)
                {
                    problemLightCount++;
                    if (problemNames.Count < MaxAggregatedLightNames)
                    {
                        problemNames.Add(vl.gameObject.name);
                    }
                }
            }

            if (problemLightCount > 0)
            {
                string names = string.Join("、", problemNames);
                if (problemLightCount > problemNames.Count)
                {
                    names += $" ほか{problemLightCount - problemNames.Count}個";
                }
                Add(issues, maxSeverity,
                    $"{problemLightCount}個の仮想光源に問題があります" +
                    $"（エラー{totalErrors}件 / 警告{totalWarnings}件: {names}）。" +
                    "詳細は各VirtualLightのInspectorの「診断」セクションを確認してください。");
            }

            CheckDepthTextureResolution(issues, activeLights);
        }

        /// <summary>
        /// 全光源共有の深度Texture2DArrayの解像度が過大でないかを確認する。
        /// 実際に使われる解像度は「最初のアクティブなVirtualLight」の設定
        /// （URP Default時はURP AssetのMain Light Shadow Resolution）で決まるため、
        /// Manager側の診断として設定の出どころとともに1件で報告する。
        /// </summary>
        private static void CheckDepthTextureResolution(
            List<DiagnosticIssue> issues, List<VirtualLight> activeLights)
        {
            if (activeLights.Count == 0)
            {
                return;
            }

            var first = activeLights[0];
            int resolution = first.ResolveTextureResolution();
            if (resolution < ResolutionWarningThreshold)
            {
                return;
            }

            // Pointモードの光源は6スライス消費するため、実際のスライス数で見積もる
            int requiredSlices = 0;
            foreach (var vl in activeLights)
            {
                requiredSlices += vl.SliceCount;
            }
            int sliceCount = Mathf.Min(requiredSlices, ShadowOnlyManager.MaxVirtualLights);
            long totalBytes = (long)resolution * resolution * 4 * sliceCount;

            string source;
            string fixHint;
            if (first.TextureResolution != VirtualLight.UseURPResolution)
            {
                source = $"最初のアクティブな仮想光源「{first.gameObject.name}」のTexture Resolution設定";
                fixHint = $"VirtualLight「{first.gameObject.name}」のTexture Resolutionを下げてください。";
            }
            else
            {
                var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                string assetName = urpAsset != null ? $"「{urpAsset.name}」" : "";
                source = $"URPアセット{assetName}のMain Light Shadow Resolution" +
                    "（VirtualLightのTexture ResolutionがURP Defaultのため）";
                fixHint = "Project Settings > Quality で使用中のURPアセットの" +
                    "Main Light Shadow Resolutionを下げるか、" +
                    "VirtualLightのTexture Resolutionに明示的な解像度を指定してください。";
            }

            Add(issues, DiagnosticSeverity.Warning,
                $"深度テクスチャ解像度が{resolution}×{resolution}と非常に大きく、" +
                $"{sliceCount}スライス分で約{totalBytes / (1024 * 1024)}MBのGPUメモリを消費します" +
                "（メモリ確保に失敗すると影が表示されなくなります）。" +
                $"この値は{source}に由来します。{fixHint}");
        }

        /// <summary>
        /// 1つの仮想光源に対する個別診断を実行する。
        /// 結果はVirtualLightEditorのInspectorに表示されるほか、
        /// Manager側では件数の集約に使用される。
        /// </summary>
        private static void CollectLightIssues(VirtualLight vl, Plane[][] frustums, List<DiagnosticIssue> issues)
        {
            // --- キャスター設定 ---
            // 個別のCasterRootが未設定の場合はManagerのDefault Caster Rootにフォールバックする
            var casterRoot = vl.EffectiveCasterRoot;
            if (casterRoot == null)
            {
                Add(issues, DiagnosticSeverity.Error,
                    "キャスタールートが設定されていません。ShadowOnlyManagerの" +
                    "Default Caster Rootに影を落とすオブジェクトの親を指定するか、" +
                    "この光源のCaster Rootで個別に指定してください。");
            }
            else
            {
                var renderers = casterRoot.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    Add(issues, DiagnosticSeverity.Error,
                        $"キャスタールート「{casterRoot.name}」の配下にRendererが1つもありません。" +
                        "メッシュを持つオブジェクトを指定してください。");
                }
                else
                {
                    CheckCasterVisibilityAndFrustum(vl, casterRoot, renderers, frustums, issues);
                }
            }

            // --- 影の濃さ ---
            if (vl.ShadowAlpha <= 0f)
            {
                string reason = vl.SourceLight != null
                    ? $"Source Light「{vl.SourceLight.name}」のShadow Strengthが0のため、同期されたShadow Alphaが0になっています。"
                    : "Shadow Alphaが0のため、";
                Add(issues, DiagnosticSeverity.Warning,
                    $"{reason}この光源の影は完全に透明です。");
            }

            // --- 深度バイアス ---
            if (vl.DepthBias >= DepthBiasWarningThreshold)
            {
                Add(issues, DiagnosticSeverity.Warning,
                    "Depth Biasが大きすぎる可能性があります" +
                    $"（現在値 {vl.DepthBias:F3}）。影が消える・欠ける場合は値を小さくしてください。");
            }

            // --- 深度テクスチャ解像度（明示指定時のみ。URP Default由来はManager側で診断する） ---
            if (vl.TextureResolution != VirtualLight.UseURPResolution
                && vl.TextureResolution >= ResolutionWarningThreshold)
            {
                long bytesPerSlice = (long)vl.TextureResolution * vl.TextureResolution * 4;
                Add(issues, DiagnosticSeverity.Warning,
                    $"Texture Resolutionが{vl.TextureResolution}×{vl.TextureResolution}と非常に大きく、" +
                    $"1スライスあたり約{bytesPerSlice / (1024 * 1024)}MBのGPUメモリを消費します。" +
                    "深度テクスチャは全光源共有のため、この光源が最初のアクティブ光源の場合は" +
                    "全スライスがこの解像度になります。");
            }
        }

        /// <summary>
        /// キャスターRendererの表示状態と、光源の投影フラスタムとの位置関係を確認する。
        /// </summary>
        private static void CheckCasterVisibilityAndFrustum(
            VirtualLight vl,
            GameObject casterRoot,
            Renderer[] renderers,
            Plane[][] frustums,
            List<DiagnosticIssue> issues)
        {
            int visibleCount = 0;
            int insideCount = 0;

            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                {
                    continue;
                }

                visibleCount++;
                if (IntersectsAnyFrustum(frustums, renderer.bounds))
                {
                    insideCount++;
                }
            }

            if (visibleCount == 0)
            {
                Add(issues, DiagnosticSeverity.Warning,
                    $"キャスタールート「{casterRoot.name}」配下のRendererが" +
                    "すべて非アクティブまたは無効です。影の元になるオブジェクトが1つも描画されません。");
                return;
            }

            if (insideCount == 0)
            {
                string hint;
                switch (vl.ProjectionMode)
                {
                    case ProjectionMode.Orthographic:
                        hint = "光源の位置・向き、Orthographic Size、Near/Far Clip Planeを確認してください。";
                        break;
                    case ProjectionMode.Point:
                        // Pointは全方向をカバーするため、範囲外の原因は距離（Near/Far）のみ
                        hint = "光源の位置と、Near/Far Clip Plane（影の届く距離）を確認してください。";
                        break;
                    default:
                        hint = "光源の位置・向き、Field Of View、Near/Far Clip Planeを確認してください。";
                        break;
                }
                Add(issues, DiagnosticSeverity.Warning,
                    $"すべてのキャスターが投影範囲（フラスタム）の外にあります。{hint}");
            }
            else if (insideCount < visibleCount)
            {
                Add(issues, DiagnosticSeverity.Info,
                    $"一部のキャスター（{visibleCount - insideCount}/{visibleCount}個）が" +
                    "投影範囲の外にあります。該当オブジェクトの影は欠けるか表示されません。");
            }
        }

        /// <summary>
        /// Manager配下にないVirtualLight（管理対象外で無視されるもの）を検出する。
        /// </summary>
        private static void CheckOrphanVirtualLights(ShadowOnlyManager manager, List<DiagnosticIssue> issues)
        {
            var allInScene = Object.FindObjectsByType<VirtualLight>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            List<string> orphanNames = null;
            foreach (var vl in allInScene)
            {
                if (vl == null) continue;
                if (vl.GetComponentInParent<ShadowOnlyManager>(true) == null)
                {
                    orphanNames ??= new List<string>();
                    orphanNames.Add(vl.gameObject.name);
                }
            }

            if (orphanNames != null)
            {
                Add(issues, DiagnosticSeverity.Warning,
                    $"ShadowOnlyManagerの子階層にないVirtualLightが存在します（{string.Join("、", orphanNames)}）。" +
                    "VirtualLightはManagerの子GameObjectに配置しないと認識されません。");
            }
        }

        #endregion

        #region 床面チェック

        /// <summary>
        /// 床面Rendererの構成（登録・表示状態・キャスターとの重複・投影範囲・マテリアル上書き）を確認する。
        /// </summary>
        private static void CheckFloorRenderers(
            ShadowOnlyManager manager,
            List<DiagnosticIssue> issues,
            List<VirtualLight> activeLights,
            List<Plane[][]> lightFrustums)
        {
            var floors = manager.FloorRenderers;

            if (floors.Count == 0)
            {
                Add(issues, DiagnosticSeverity.Error,
                    "床面Rendererが1つも登録されていません。影が映り込む床を" +
                    "「床面Renderer」に登録するか、「床面を自動生成」ボタンを使用してください。");
                return;
            }

            int nullCount = 0;
            var activeFloors = new List<Renderer>();
            foreach (var floor in floors)
            {
                if (floor == null)
                {
                    nullCount++;
                }
                else if (floor.gameObject.activeInHierarchy && floor.enabled)
                {
                    activeFloors.Add(floor);
                }
            }

            if (nullCount > 0)
            {
                Add(issues, DiagnosticSeverity.Warning,
                    $"床面Rendererのリストに空（None）の要素が{nullCount}件含まれています。" +
                    "削除済みオブジェクトの残骸の可能性があります。");
            }

            if (activeFloors.Count == 0)
            {
                Add(issues, DiagnosticSeverity.Error,
                    "登録されている床面Rendererがすべて非アクティブまたは無効です。" +
                    "影を描画する床が1つも表示されていません。");
                return;
            }

            foreach (var floor in activeFloors)
            {
                CheckFloorAgainstLights(floor, issues, activeLights, lightFrustums);
            }

            CheckFloorMaterialOverride(manager, activeFloors, issues);
        }

        /// <summary>
        /// 1つの床面Rendererについて、キャスターとの重複と光源投影範囲との交差を確認する。
        /// </summary>
        private static void CheckFloorAgainstLights(
            Renderer floor,
            List<DiagnosticIssue> issues,
            List<VirtualLight> activeLights,
            List<Plane[][]> lightFrustums)
        {
            // 床がいずれかの実効キャスタールート配下に含まれる場合、床自身が影を落とし
            // 全面が影になる・ちらつくなどの異常の原因になる
            foreach (var vl in activeLights)
            {
                var casterRoot = vl.EffectiveCasterRoot;
                if (casterRoot != null && floor.transform.IsChildOf(casterRoot.transform))
                {
                    Add(issues, DiagnosticSeverity.Warning,
                        $"床面「{floor.gameObject.name}」が仮想光源「{vl.gameObject.name}」の" +
                        $"キャスタールート「{casterRoot.name}」配下に含まれています。" +
                        "床自身が影を落とすため、床全体が影になる・ちらつくなどの異常の原因になります。" +
                        "キャスタールートから床を除外してください。");
                    break;
                }
            }

            // 床がどの光源の投影範囲とも交差しない場合、その床には影が投影されない
            if (activeLights.Count > 0)
            {
                bool intersectsAny = false;
                foreach (var frustums in lightFrustums)
                {
                    if (IntersectsAnyFrustum(frustums, floor.bounds))
                    {
                        intersectsAny = true;
                        break;
                    }
                }

                if (!intersectsAny)
                {
                    Add(issues, DiagnosticSeverity.Warning,
                        $"床面「{floor.gameObject.name}」がどの仮想光源の投影範囲にも入っていません。" +
                        "この床には影が描画されません。光源のFar Clip Planeや投影範囲、" +
                        "床と光源の位置関係を確認してください。");
                }
            }
        }

        /// <summary>
        /// 床面のマテリアルが本パッケージ管理外のものに上書きされていないかを確認する。
        /// ExecuteAlwaysによりEditモードでもマテリアルが割り当てられるため、
        /// Play/Editの両モードで確認する。
        /// </summary>
        private static void CheckFloorMaterialOverride(
            ShadowOnlyManager manager, List<Renderer> activeFloors, List<DiagnosticIssue> issues)
        {
            // Managerが動作している（=マテリアル割り当て済みの）場合のみ意味がある
            if (!manager.isActiveAndEnabled || manager.FloorMaterial == null)
            {
                return;
            }

            foreach (var floor in activeFloors)
            {
                var mat = floor.sharedMaterial;
                string shaderName = (mat != null && mat.shader != null) ? mat.shader.name : null;

                if (shaderName != FloorShaderName && shaderName != FloorDisplayShaderName)
                {
                    Add(issues, DiagnosticSeverity.Warning,
                        $"床面「{floor.gameObject.name}」のマテリアルが本パッケージ管理外のもの" +
                        $"（{(mat != null ? mat.name : "None")}）になっています。" +
                        "他のスクリプトによる上書きの可能性があり、この床には影が描画されません。");
                }
            }
        }

        #endregion
    }
}
