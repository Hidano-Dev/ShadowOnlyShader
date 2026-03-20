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
    /// 結果はグローバルテクスチャとして設定され、Display Pass（Pass 0）でサンプリングされる。
    ///
    /// 有効/無効の切り替えはShadowOnlyManagerがマテリアルキーワード
    /// _SHADOW_RESOLVE_ACTIVE で制御する（LateUpdateで設定済み）。
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

            _resolveRT = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
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
        /// 低解像度RTにフロアRendererをPass 1（Resolve）で描画し、
        /// グローバルテクスチャを設定する。
        /// </summary>
        private static void ExecuteResolveCommands(CommandBuffer cmd, PassData data)
        {
            var rt = data.resolveTexture;

            // Resolve RTをレンダーターゲットに設定
            cmd.SetRenderTarget(rt);
            cmd.SetViewport(new Rect(0, 0, rt.width, rt.height));
            cmd.ClearRenderTarget(true, true, new Color(0, 0, 0, 0), 1.0f);

            // フロアRendererをPass 1（ShadowOnlyResolve）で描画
            // Pass 1は常にフルシャドウ計算を実行する（キーワード _SHADOW_RESOLVE_ACTIVE の影響を受けない）
            for (int i = 0; i < data.floorRenderers.Count; i++)
            {
                var renderer = data.floorRenderers[i];
                if (renderer == null) continue;

                int submeshCount = renderer.sharedMaterials.Length;
                for (int s = 0; s < submeshCount; s++)
                {
                    cmd.DrawRenderer(renderer, data.floorMaterial, s, 1); // Pass 1 = ShadowOnlyResolve
                }
            }

            // Resolve結果をグローバルテクスチャとして設定
            // Display Pass（Pass 0）の _SHADOW_RESOLVE_ACTIVE バリアントがこのテクスチャをサンプリングする
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
            float scale = _manager.BlurResolutionScale;
            var resolveRT = EnsureResolveTexture(camera.pixelWidth, camera.pixelHeight, scale);

            _legacyPassData.resolveTexture = resolveRT;
            _legacyPassData.floorMaterial = _manager.FloorMaterial;
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
            var resourceData = frameData.Get<UniversalResourceData>();

            float scale = _manager.BlurResolutionScale;
            var resolveRT = EnsureResolveTexture(camera.pixelWidth, camera.pixelHeight, scale);

            using (var builder = renderGraph.AddUnsafePass<PassData>(
                "ShadowOnly Resolve Pass", out var passData))
            {
                passData.resolveTexture = resolveRT;
                passData.floorMaterial = _manager.FloorMaterial;
                CollectFloorRenderers(passData);

                if (passData.floorRenderers.Count == 0)
                {
                    return;
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
