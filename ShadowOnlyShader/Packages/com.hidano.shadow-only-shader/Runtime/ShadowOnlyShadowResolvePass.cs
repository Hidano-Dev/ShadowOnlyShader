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

            /// <summary>カメラのカラーターゲット（レンダーターゲット復元用）。</summary>
            public RTHandle cameraColorTarget;

            /// <summary>カメラのデプスターゲット（レンダーターゲット復元用）。</summary>
            public RTHandle cameraDepthTarget;
        }

        /// <summary>アクティブなShadowOnlyManagerの参照。</summary>
        private ShadowOnlyManager _manager;

        /// <summary>レガシーパス用のPassDataキャッシュ。</summary>
        private readonly PassData _legacyPassData = new PassData();

        /// <summary>Resolve用RenderTexture（パスが管理）。</summary>
        private RenderTexture _resolveRT;

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
        /// カメラ解像度またはスケールが変わった場合に再作成される。
        /// </summary>
        private RenderTexture EnsureResolveTexture(int cameraWidth, int cameraHeight, float scale)
        {
            int width = Mathf.Max(1, (int)(cameraWidth * scale));
            int height = Mathf.Max(1, (int)(cameraHeight * scale));

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
        private static void ExecuteResolveCommands(CommandBuffer cmd, PassData data)
        {
            var rt = data.resolveTexture;

            // --- セットアップ計測 ---
            cmd.BeginSample("Resolve_Setup");
            cmd.SetRenderTarget(rt);
            cmd.SetViewport(new Rect(0, 0, rt.width, rt.height));
            cmd.ClearRenderTarget(false, true, new Color(0, 0, 0, 0));
            cmd.EndSample("Resolve_Setup");

            // --- ライトごとの描画計測 ---
            for (int light = 0; light < data.lightCount; light++)
            {
                string lightLabel = $"Resolve_Light{light}";
                cmd.BeginSample(lightLabel);
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
                cmd.EndSample(lightLabel);
            }

            // --- グローバルテクスチャ設定 ---
            cmd.BeginSample("Resolve_SetGlobal");
            cmd.SetGlobalTexture(_resolveTexId, rt);
            cmd.EndSample("Resolve_SetGlobal");
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
            float scale = _manager.BlurResolutionScale;
            var resolveRT = EnsureResolveTexture(camera.pixelWidth, camera.pixelHeight, scale);

            _legacyPassData.resolveTexture = resolveRT;
            _legacyPassData.floorMaterial = _manager.FloorMaterial;
            _legacyPassData.lightCount = _manager.ActiveVirtualLightCount;
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
                // 60フレームに1回だけログ出力（スパム防止）
                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[ResolvePass] ShouldExecute=false | " +
                        $"manager={(_manager != null)} " +
                        $"scale={_manager?.BlurResolutionScale:F2} " +
                        $"floorMat={(_manager?.FloorMaterial != null)} " +
                        $"floorCount={_manager?.FloorRenderers.Count}");
                }
                return;
            }

            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;
            var resourceData = frameData.Get<UniversalResourceData>();

            float scale = _manager.BlurResolutionScale;
            var resolveRT = EnsureResolveTexture(camera.pixelWidth, camera.pixelHeight, scale);

            using (var builder = renderGraph.AddUnsafePass<PassData>(
                "ShadowOnly Resolve Pass", out var passData))
            {
                passData.resolveTexture = resolveRT;
                passData.floorMaterial = _manager.FloorMaterial;
                passData.lightCount = _manager.ActiveVirtualLightCount;
                CollectFloorRenderers(passData);

                if (passData.floorRenderers.Count == 0)
                {
                    if (Time.frameCount % 60 == 0)
                    {
                        Debug.Log("[ResolvePass] floorRenderers.Count == 0 after collect, skipping");
                    }
                    return;
                }

                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[ResolvePass] EXECUTING | " +
                        $"lights={passData.lightCount} " +
                        $"renderers={passData.floorRenderers.Count} " +
                        $"rtSize={passData.resolveTexture.width}x{passData.resolveTexture.height}");
                }

                // カメラターゲットの依存宣言（復元用）
                builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Write);
                builder.UseTexture(resourceData.activeDepthTexture, AccessFlags.Write);

                // レンダーターゲット復元用にカメラのRTHandleを保存
#pragma warning disable CS0618
                passData.cameraColorTarget = cameraData.renderer.cameraColorTargetHandle;
                passData.cameraDepthTarget = cameraData.renderer.cameraDepthTargetHandle;
#pragma warning restore CS0618

                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    ExecuteResolveCommands(cmd, data);

                    // カメラのカラー/デプスターゲットを復元
                    if (data.cameraColorTarget != null && data.cameraDepthTarget != null)
                    {
                        CoreUtils.SetRenderTarget(cmd, data.cameraColorTarget, data.cameraDepthTarget);
                    }
                });
            }
        }

        #endregion
    }
}
