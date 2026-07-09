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
    /// ShadowOnlyManagerEditorのInspectorから呼び出され、結果がHelpBoxで表示される。
    ///
    /// 検出対象:
    /// - パイプライン系: URP未使用 / Renderer FeatureがRendererに未登録・無効 /
    ///   Quality設定側URPアセットの差し替え漏れ（Graphics設定のみ変更の落とし穴）
    /// - 環境系: 必須シェーダーの欠落・非サポート / Texture2DArray・深度フォーマット非対応
    /// - Manager系: 非Playモード / Manager無効 / 複数Manager / BlendMultiplier=0
    /// - VirtualLight系: 光源なし / 全て非アクティブ / 上限超過 / Manager配下にない光源 /
    ///   CasterRoot未設定・Rendererなし・全て非表示 / キャスターが投影範囲外 /
    ///   ShadowAlpha=0 / DepthBias過大 / 深度テクスチャ解像度過大
    /// - 床面系: 未登録 / null要素 / 全て非表示 / 床がキャスターに含まれる（自己投影） /
    ///   床がどの光源の投影範囲にも入っていない / Play中のマテリアル上書き
    /// </summary>
    internal static class ShadowOnlyDiagnostics
    {
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
            var activeLights = new List<VirtualLight>();
            var lightFrustums = new List<Plane[]>();
            CheckVirtualLights(manager, issues, activeLights, lightFrustums);
            CheckFloorRenderers(manager, issues, activeLights, lightFrustums);
            CheckOrphanVirtualLights(manager, issues);

            // Error → Warning → Info の順に並べる（同一深刻度内は検出順を維持）
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
        /// Manager自体の状態（Playモード・有効状態・重複・グローバルパラメータ）を確認する。
        /// </summary>
        private static void CheckManagerState(ShadowOnlyManager manager, List<DiagnosticIssue> issues)
        {
            if (!Application.isPlaying)
            {
                Add(issues, DiagnosticSeverity.Info,
                    "影の描画・更新はPlayモード中のみ行われます。" +
                    "Editモードでは本ツールの影は表示されません（URP標準の影とは別物です）。");
            }

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
        /// VirtualLightの構成（存在・アクティブ状態・キャスター設定・投影範囲・各パラメータ）を確認する。
        /// アクティブな光源と投影フラスタムを収集し、床面チェックでも再利用する。
        /// </summary>
        private static void CheckVirtualLights(
            ShadowOnlyManager manager,
            List<DiagnosticIssue> issues,
            List<VirtualLight> activeLights,
            List<Plane[]> lightFrustums)
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

            if (activeLights.Count > ShadowOnlyManager.MaxVirtualLights)
            {
                Add(issues, DiagnosticSeverity.Warning,
                    $"アクティブな仮想光源が{activeLights.Count}個あります。" +
                    $"上限は{ShadowOnlyManager.MaxVirtualLights}個のため、" +
                    $"{ShadowOnlyManager.MaxVirtualLights + 1}個目以降は無視されます。");
            }

            foreach (var vl in activeLights)
            {
                // Editモードでは行列がLateUpdateで更新されないため、ここで明示的に計算する
                vl.UpdateMatrices();
                var frustum = GeometryUtility.CalculateFrustumPlanes(vl.ProjectionMatrix * vl.ViewMatrix);
                lightFrustums.Add(frustum);

                CheckSingleLight(vl, frustum, issues);
            }
        }

        /// <summary>
        /// 1つの仮想光源に対する診断を実行する。
        /// </summary>
        private static void CheckSingleLight(VirtualLight vl, Plane[] frustum, List<DiagnosticIssue> issues)
        {
            string lightName = vl.gameObject.name;

            // --- キャスター設定 ---
            if (vl.CasterRoot == null)
            {
                Add(issues, DiagnosticSeverity.Error,
                    $"仮想光源「{lightName}」: Caster Rootが設定されていません。" +
                    "影を落とすオブジェクトの親を指定してください。");
            }
            else
            {
                var renderers = vl.CasterRoot.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    Add(issues, DiagnosticSeverity.Error,
                        $"仮想光源「{lightName}」: Caster Root「{vl.CasterRoot.name}」の配下に" +
                        "Rendererが1つもありません。メッシュを持つオブジェクトを指定してください。");
                }
                else
                {
                    CheckCasterVisibilityAndFrustum(vl, lightName, renderers, frustum, issues);
                }
            }

            // --- 影の濃さ ---
            if (vl.ShadowAlpha <= 0f)
            {
                string reason = vl.SourceLight != null
                    ? $"Source Light「{vl.SourceLight.name}」のShadow Strengthが0のため、同期されたShadow Alphaが0になっています。"
                    : "Shadow Alphaが0のため、";
                Add(issues, DiagnosticSeverity.Warning,
                    $"仮想光源「{lightName}」: {reason}この光源の影は完全に透明です。");
            }

            // --- 深度バイアス ---
            if (vl.DepthBias >= DepthBiasWarningThreshold)
            {
                Add(issues, DiagnosticSeverity.Warning,
                    $"仮想光源「{lightName}」: Depth Biasが大きすぎる可能性があります" +
                    $"（現在値 {vl.DepthBias:F3}）。影が消える・欠ける場合は値を小さくしてください。");
            }

            // --- 深度テクスチャ解像度 ---
            int resolution = vl.ResolveTextureResolution();
            if (resolution >= ResolutionWarningThreshold)
            {
                long bytesPerSlice = (long)resolution * resolution * 4;
                Add(issues, DiagnosticSeverity.Warning,
                    $"仮想光源「{lightName}」: 深度テクスチャ解像度が{resolution}×{resolution}と非常に大きく、" +
                    $"1スライスあたり約{bytesPerSlice / (1024 * 1024)}MBのGPUメモリを消費します。" +
                    "VirtualLightのTexture Resolution、またはURP AssetのMain Light Shadow Resolutionを確認してください。");
            }
        }

        /// <summary>
        /// キャスターRendererの表示状態と、光源の投影フラスタムとの位置関係を確認する。
        /// </summary>
        private static void CheckCasterVisibilityAndFrustum(
            VirtualLight vl,
            string lightName,
            Renderer[] renderers,
            Plane[] frustum,
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
                if (GeometryUtility.TestPlanesAABB(frustum, renderer.bounds))
                {
                    insideCount++;
                }
            }

            if (visibleCount == 0)
            {
                Add(issues, DiagnosticSeverity.Warning,
                    $"仮想光源「{lightName}」: Caster Root「{vl.CasterRoot.name}」配下のRendererが" +
                    "すべて非アクティブまたは無効です。影の元になるオブジェクトが1つも描画されません。");
                return;
            }

            if (insideCount == 0)
            {
                string rangeHint = vl.ProjectionMode == ProjectionMode.Orthographic
                    ? "Orthographic Size"
                    : "Field Of View";
                Add(issues, DiagnosticSeverity.Warning,
                    $"仮想光源「{lightName}」: すべてのキャスターが投影範囲（フラスタム）の外にあります。" +
                    $"光源の位置・向き、{rangeHint}、Near/Far Clip Planeを確認してください。");
            }
            else if (insideCount < visibleCount)
            {
                Add(issues, DiagnosticSeverity.Info,
                    $"仮想光源「{lightName}」: 一部のキャスター（{visibleCount - insideCount}/{visibleCount}個）が" +
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
            List<Plane[]> lightFrustums)
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
            List<Plane[]> lightFrustums)
        {
            // 床がいずれかのCasterRoot配下に含まれる場合、床自身が影を落とし
            // 全面が影になる・ちらつくなどの異常の原因になる
            foreach (var vl in activeLights)
            {
                if (vl.CasterRoot != null && floor.transform.IsChildOf(vl.CasterRoot.transform))
                {
                    Add(issues, DiagnosticSeverity.Warning,
                        $"床面「{floor.gameObject.name}」が仮想光源「{vl.gameObject.name}」の" +
                        "Caster Root配下に含まれています。床自身が影を落とすため、" +
                        "床全体が影になる・ちらつくなどの異常の原因になります。" +
                        "Caster Rootから床を除外してください。");
                    break;
                }
            }

            // 床がどの光源の投影範囲とも交差しない場合、その床には影が投影されない
            if (activeLights.Count > 0)
            {
                bool intersectsAny = false;
                foreach (var frustum in lightFrustums)
                {
                    if (GeometryUtility.TestPlanesAABB(frustum, floor.bounds))
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
        /// Play中に床面のマテリアルが本パッケージ管理外のものに上書きされていないかを確認する。
        /// </summary>
        private static void CheckFloorMaterialOverride(
            ShadowOnlyManager manager, List<Renderer> activeFloors, List<DiagnosticIssue> issues)
        {
            // Managerが動作している（=マテリアル割り当て済みの）Play中のみ意味がある
            if (!Application.isPlaying || !manager.isActiveAndEnabled || manager.FloorMaterial == null)
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
