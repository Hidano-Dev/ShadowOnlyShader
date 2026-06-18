using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace ShadowOnlyShader
{
    /// <summary>
    /// 影計算を低解像度RTで事前実行するRenderPass。
    /// BlurResolutionScale が 1.0 未満の場合に有効化され、
    /// フロアRendererを低解像度RTに描画してブラー等の重い計算を実行する。
    /// 結果はグローバルテクスチャ _ShadowResolveTex として設定され、
    /// FloorDisplayシェーダー（ShadowOnlyManagerがResolve有効時に割り当てる）でサンプリングされる。
    ///
    /// 有効/無効の切り替えはShadowOnlyManagerがフロアRendererのマテリアル自体を
    /// 切り替えることで制御する（LateUpdateでFloorMaterialとFloorDisplayMaterialを切り替え）。
    /// このパスはResolve RTの描画とグローバルテクスチャの設定のみを行う。
    /// </summary>
    public class ShadowOnlyShadowResolvePass : ScriptableRenderPass
    {
        /// <summary>
        /// UnsafePassに渡すデータ構造体。
        /// </summary>
        internal class PassData
        {
            /// <summary>Resolve用RenderTexture。</summary>
            public RenderTexture resolveTexture;

            /// <summary>描画対象のフロアRendererリスト。</summary>
            public List<Renderer> floorRenderers = new List<Renderer>();

            /// <summary>フロア用Material。</summary>
            public Material floorMaterial;

            /// <summary>アクティブなVirtualLightの数。</summary>
            public int lightCount;

            /// <summary>カメラのView行列（Resolve RTを画面と同じ投影で描くために使用）。</summary>
            public Matrix4x4 cameraViewMatrix;

            /// <summary>カメラのProjection行列（同上）。</summary>
            public Matrix4x4 cameraProjectionMatrix;
        }

        /// <summary>アクティブなShadowOnlyManagerの参照。</summary>
        private ShadowOnlyManager _manager;

        /// <summary>レガシーパス用のPassDataキャッシュ。</summary>
        private readonly PassData _legacyPassData = new PassData();

        /// <summary>Resolve用RenderTexture（パスが管理）。</summary>
        private RenderTexture _resolveRT;

        /// <summary>
        /// 解像度スナップのステップサイズ（ピクセル）。
        /// RT再作成の頻度を抑えるために離散的な解像度にスナップする。
        /// </summary>
        private const int ResolutionSnapStep = 64;

        /// <summary>シェーダーグローバル変数: Resolveテクスチャ。</summary>
        private static readonly int _resolveTexId = Shader.PropertyToID("_ShadowResolveTex");

        /// <summary>シェーダーグローバル変数: ライトごとのResolve描画インデックス。</summary>
        private static readonly int _resolveLightIndexId = Shader.PropertyToID("_ResolveLightIndex");

        /// <summary>
        /// Passで使用するManagerを設定する。
        /// </summary>
        public void SetManager(ShadowOnlyManager manager)
        {
            _manager = manager;
        }

        /// <summary>
        /// Resolve RTを確保・再作成する。
        /// 解像度が変わった場合のみ再作成される。
        /// </summary>
        /// <param name="width">RTの幅（ピクセル）</param>
        /// <param name="height">RTの高さ（ピクセル）</param>
        private RenderTexture EnsureResolveTexture(int width, int height)
        {
            if (_resolveRT != null && _resolveRT.width == width && _resolveRT.height == height)
            {
                return _resolveRT;
            }

            ReleaseResolveTexture();

            // デプスバッファ不要（ZTest Always + ZWrite Off）— 帯域幅を節約
            _resolveRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            _resolveRT.filterMode = FilterMode.Bilinear;
            _resolveRT.hideFlags = HideFlags.DontSave;
            _resolveRT.Create();

            return _resolveRT;
        }

        /// <summary>
        /// カメラ距離に応じた実効スケールを計算する。
        /// 適応解像度が有効な場合、カメラと床面の距離から動的にスケールを低減する。
        /// </summary>
        /// <param name="camera">現在のカメラ</param>
        /// <param name="baseScale">ユーザー設定のBlurResolutionScale</param>
        /// <returns>適用すべき実効スケール</returns>
        private float ComputeEffectiveScale(Camera camera, float baseScale)
        {
            if (!_manager.AdaptiveResolution)
            {
                return baseScale;
            }

            if (!_manager.TryGetFloorBounds(out var bounds))
            {
                return baseScale;
            }

            // カメラから床面Boundsの中心までの距離
            float distance = Vector3.Distance(camera.transform.position, bounds.center);

            // 基準サイズ: 床面Boundsの対角半径
            // この距離では床がビューをほぼ覆うため、フル解像度が必要
            float referenceSize = bounds.extents.magnitude;
            if (referenceSize < 0.001f)
            {
                referenceSize = 1f;
            }

            // 距離係数: 基準距離以内では1.0、遠くなるほど低下
            float distanceFactor = Mathf.Clamp(
                referenceSize / Mathf.Max(distance, 0.001f),
                _manager.AdaptiveResolutionMinScale,
                1.0f);

            return baseScale * distanceFactor;
        }

        /// <summary>
        /// 実効スケールからResolve RTのピクセルサイズを計算する。
        /// ResolutionSnapStepの倍数にスナップしてRT再作成の頻度を抑える。
        /// </summary>
        private static (int width, int height) ComputeResolveSize(
            int cameraWidth, int cameraHeight, float effectiveScale)
        {
            int width = Mathf.Max(ResolutionSnapStep, (int)(cameraWidth * effectiveScale));
            int height = Mathf.Max(ResolutionSnapStep, (int)(cameraHeight * effectiveScale));

            // SnapStepの倍数にスナップ（RT再作成の頻度を抑制）
            width = (width / ResolutionSnapStep) * ResolutionSnapStep;
            height = (height / ResolutionSnapStep) * ResolutionSnapStep;

            return (width, height);
        }

        /// <summary>
        /// Resolve RTを解放する。
        /// </summary>
        public void ReleaseResolveTexture()
        {
            if (_resolveRT != null)
            {
                _resolveRT.Release();
                Object.DestroyImmediate(_resolveRT);
                _resolveRT = null;
            }
        }

        /// <summary>
        /// Resolveパスを実行すべきかどうかを判定する。
        /// </summary>
        private bool ShouldExecute()
        {
            return _manager != null
                && _manager.BlurResolutionScale < 0.999f
                && _manager.FloorMaterial != null
                && _manager.FloorRenderers.Count > 0;
        }

        /// <summary>
        /// フロアRendererの有効なリストを収集する。
        /// </summary>
        private void CollectFloorRenderers(PassData passData)
        {
            passData.floorRenderers.Clear();
            var renderers = _manager.FloorRenderers;
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r != null && r.gameObject.activeInHierarchy && r.enabled)
                {
                    passData.floorRenderers.Add(r);
                }
            }
        }

        #region 共通描画処理

        /// <summary>
        /// CommandBufferを使用してResolve描画を実行する共通処理。
        /// ライトごとに個別にドローコールを発行し、加算ブレンド（Blend One One）で合成する。
        /// これにより各ドローコールがTexture2DArrayの1スライスのみアクセスし、
        /// GPU テクスチャキャッシュスラッシングを回避する。
        /// </summary>
        /// <summary>
        /// Per-light描画とsingle-draw描画の切り替え閾値（ピクセル数）。
        /// この閾値以下のRT面積ではsingle-draw（Pass 2）を使用し、
        /// GPUパイプラインの状態変更オーバーヘッドを回避する。
        /// 閾値以上ではper-light（Pass 1）を使用し、
        /// Texture2DArrayのキャッシュスラッシングを回避する。
        /// 目安: 480×270 = 129,600（1080pの1/4解像度相当）
        /// </summary>
        private const int PerLightPixelThreshold = 130000;

        private static void ExecuteResolveCommands(CommandBuffer cmd, PassData data)
        {
            var rt = data.resolveTexture;
            int pixelCount = rt.width * rt.height;
            bool usePerLight = pixelCount >= PerLightPixelThreshold && data.lightCount > 1;

            // Resolve RTをレンダーターゲットに設定してクリア
            cmd.SetRenderTarget(rt);
            cmd.SetViewport(new Rect(0, 0, rt.width, rt.height));
            cmd.ClearRenderTarget(false, true, new Color(0, 0, 0, 0));

            // カメラのView/Projection行列を明示的に設定する。
            // RenderGraph の UnsafePass ではカメラ行列が自動バインドされないため、
            // これを省くと床が誤った行列で描かれ、Resolve RT 内で縮小・隅寄りになる
            // （Display シェーダーは screenUV で画面全体[0,1]を前提にサンプリングするため位置ずれになる）。
            // 画面（カメラカラーターゲット）と同じVPで描くことで screenUV と一致させる。
            cmd.SetViewProjectionMatrices(data.cameraViewMatrix, data.cameraProjectionMatrix);

            if (usePerLight)
            {
                // --- Per-light描画: 大きいRTでキャッシュスラッシングを回避 ---
                // Pass 1（Blend One One）でライトごとに個別描画
                for (int light = 0; light < data.lightCount; light++)
                {
                    cmd.SetGlobalInt(_resolveLightIndexId, light);

                    for (int i = 0; i < data.floorRenderers.Count; i++)
                    {
                        var renderer = data.floorRenderers[i];
                        if (renderer == null) continue;

                        int submeshCount = renderer.sharedMaterials.Length;
                        for (int s = 0; s < submeshCount; s++)
                        {
                            cmd.DrawRenderer(renderer, data.floorMaterial, s, 1);
                        }
                    }
                }
            }
            else
            {
                // --- Single-draw描画: 小さいRTでGPUオーバーヘッドを最小化 ---
                // Pass 2（Blend Off）で全ライトを一括描画
                // RTが小さいためテクスチャキャッシュに全スライスが収まる
                for (int i = 0; i < data.floorRenderers.Count; i++)
                {
                    var renderer = data.floorRenderers[i];
                    if (renderer == null) continue;

                    int submeshCount = renderer.sharedMaterials.Length;
                    for (int s = 0; s < submeshCount; s++)
                    {
                        cmd.DrawRenderer(renderer, data.floorMaterial, s, 2); // Pass 2 = SingleDraw
                    }
                }
            }

            // Resolve結果をグローバルテクスチャとして設定
            cmd.SetGlobalTexture(_resolveTexId, rt);
        }

        #endregion

        #region レガシーパス (Execute)

        /// <summary>
        /// レガシーレンダリングパス用のExecute実装。
        /// </summary>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!ShouldExecute())
            {
                return;
            }

            var camera = renderingData.cameraData.camera;
            float effectiveScale = ComputeEffectiveScale(camera, _manager.BlurResolutionScale);
            var (w, h) = ComputeResolveSize(camera.pixelWidth, camera.pixelHeight, effectiveScale);
            var resolveRT = EnsureResolveTexture(w, h);

            _legacyPassData.resolveTexture = resolveRT;
            _legacyPassData.floorMaterial = _manager.FloorMaterial;
            _legacyPassData.lightCount = _manager.ActiveVirtualLightCount;
            _legacyPassData.cameraViewMatrix = camera.worldToCameraMatrix;
            _legacyPassData.cameraProjectionMatrix = camera.projectionMatrix;
            CollectFloorRenderers(_legacyPassData);

            if (_legacyPassData.floorRenderers.Count == 0)
            {
                return;
            }

            var cmd = CommandBufferPool.Get("ShadowOnly Resolve Pass");
            ExecuteResolveCommands(cmd, _legacyPassData);

            // カメラのカラー/デプスターゲットを復元
#pragma warning disable CS0618
            var cameraRenderer = renderingData.cameraData.renderer;
            cmd.SetRenderTarget(
                cameraRenderer.cameraColorTargetHandle,
                cameraRenderer.cameraDepthTargetHandle);
#pragma warning restore CS0618

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        #endregion

        #region RenderGraphパス (RecordRenderGraph)

        /// <summary>
        /// RenderGraphにUnsafePassを登録し、低解像度RTにフロアの影を事前計算する。
        /// </summary>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!ShouldExecute())
            {
                return;
            }

            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;

            float effectiveScale = ComputeEffectiveScale(camera, _manager.BlurResolutionScale);
            var (w, h) = ComputeResolveSize(camera.pixelWidth, camera.pixelHeight, effectiveScale);
            var resolveRT = EnsureResolveTexture(w, h);

            using (var builder = renderGraph.AddUnsafePass<PassData>(
                "ShadowOnly Resolve Pass", out var passData))
            {
                passData.resolveTexture = resolveRT;
                passData.floorMaterial = _manager.FloorMaterial;
                passData.lightCount = _manager.ActiveVirtualLightCount;
                passData.cameraViewMatrix = camera.worldToCameraMatrix;
                passData.cameraProjectionMatrix = camera.projectionMatrix;
                CollectFloorRenderers(passData);

                if (passData.floorRenderers.Count == 0)
                {
                    return;
                }

                // このパスは外部のResolve RTにのみ描画し、結果をグローバルテクスチャとして公開する。
                // カメラのカラーを UseTexture(Write) で宣言するとURPのクリアを奪い残像の原因になるため、
                // カメラターゲットには触れない（後続のRasterRenderPassが各自再バインドする）。
                // SetGlobalTexture等のグローバル状態変更を行うため明示的に許可する。
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    ExecuteResolveCommands(cmd, data);
                });
            }
        }

        #endregion
    }
}
