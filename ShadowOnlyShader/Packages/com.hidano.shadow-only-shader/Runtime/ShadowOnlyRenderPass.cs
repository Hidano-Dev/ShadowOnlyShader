using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace ShadowOnlyShader
{
    /// <summary>
    /// 影のみ描画用のRenderPass。
    /// RenderGraph UnsafePassでキャスターRendererを深度RenderTextureに描画する。
    /// Requirements: 1.3, 1.4, 5.1, 5.3, 5.4
    /// </summary>
    public class ShadowOnlyRenderPass : ScriptableRenderPass
    {
        /// <summary>
        /// UnsafePassに渡すデータ構造体。
        /// 各VirtualLightの深度レンダリングに必要な情報を保持する。
        /// </summary>
        internal class PassData
        {
            /// <summary>View行列のリスト（各VirtualLight分）。</summary>
            public List<Matrix4x4> viewMatrices = new List<Matrix4x4>();

            /// <summary>Projection行列のリスト（各VirtualLight分）。</summary>
            public List<Matrix4x4> projectionMatrices = new List<Matrix4x4>();

            /// <summary>深度RenderTextureのリスト（各VirtualLight分）。</summary>
            public List<RenderTexture> depthRenderTextures = new List<RenderTexture>();

            /// <summary>キャスターRendererリストのリスト（各VirtualLight分）。</summary>
            public List<List<Renderer>> casterRendererLists = new List<List<Renderer>>();

            /// <summary>深度描画に使用するDepthOnly Material。</summary>
            public Material depthOnlyMaterial;

            /// <summary>元のカメラView行列（描画後に復元する）。</summary>
            public Matrix4x4 cameraViewMatrix;

            /// <summary>元のカメラProjection行列（描画後に復元する）。</summary>
            public Matrix4x4 cameraProjectionMatrix;

            /// <summary>
            /// PassDataの内容をクリアする。
            /// 毎フレームの再利用のために使用する。
            /// </summary>
            public void Clear()
            {
                viewMatrices.Clear();
                projectionMatrices.Clear();
                depthRenderTextures.Clear();
                casterRendererLists.Clear();
                depthOnlyMaterial = null;
            }
        }

        /// <summary>
        /// アクティブなShadowOnlyManagerの参照。
        /// RendererFeatureのAddRenderPassesで毎フレーム設定される。
        /// </summary>
        private ShadowOnlyManager _manager;

        /// <summary>
        /// 深度描画に使用するDepthOnly Material。
        /// RendererFeatureから渡される。
        /// </summary>
        private Material _depthOnlyMaterial;

        /// <summary>
        /// Passで使用するManagerを設定する。
        /// </summary>
        /// <param name="manager">アクティブなShadowOnlyManager</param>
        public void SetManager(ShadowOnlyManager manager)
        {
            _manager = manager;
        }

        /// <summary>
        /// DepthOnly Materialを設定する。
        /// </summary>
        /// <param name="material">DepthOnly Material</param>
        public void SetDepthOnlyMaterial(Material material)
        {
            _depthOnlyMaterial = material;
        }

        /// <summary>
        /// RenderGraphにUnsafePassを登録し、各VirtualLightの深度テクスチャを生成する。
        /// AddUnsafePassを使用し、UnsafeGraphContextからCommandBufferを取得して
        /// SetRenderTarget、Clear、SetViewProjectionMatrices、DrawRendererを実行する。
        /// </summary>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // ManagerまたはDepthOnly Materialが未設定の場合は早期リターン
            if (_manager == null || _depthOnlyMaterial == null)
            {
                return;
            }

            var virtualLights = _manager.VirtualLights;
            if (virtualLights == null || virtualLights.Count == 0)
            {
                return;
            }

            // カメラのView/Projection行列を取得（描画後に復元するため）
            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;

            using (var builder = renderGraph.AddUnsafePass<PassData>(
                "ShadowOnly Depth Pass", out var passData))
            {
                // PassDataをセットアップ
                passData.Clear();
                passData.depthOnlyMaterial = _depthOnlyMaterial;
                passData.cameraViewMatrix = camera.worldToCameraMatrix;
                passData.cameraProjectionMatrix = camera.projectionMatrix;

                bool hasValidLight = false;

                for (int i = 0; i < virtualLights.Count; i++)
                {
                    var vl = virtualLights[i] as VirtualLight;
                    if (vl == null || !vl.isActiveAndEnabled)
                    {
                        continue;
                    }

                    // 深度RenderTextureの確認・作成
                    vl.EnsureDepthTexture();
                    var depthRT = vl.DepthRenderTexture;
                    if (depthRT == null)
                    {
                        continue;
                    }

                    // VP行列の更新
                    vl.UpdateMatrices();

                    // キャスターRendererの収集
                    vl.CollectRenderers();
                    var casterRenderers = vl.CasterRenderers;
                    if (casterRenderers == null || casterRenderers.Count == 0)
                    {
                        continue;
                    }

                    // 有効なRendererのみをフィルタリングしてリストに追加
                    var validRenderers = new List<Renderer>();
                    for (int r = 0; r < casterRenderers.Count; r++)
                    {
                        var renderer = casterRenderers[r];
                        if (renderer != null && renderer.gameObject.activeInHierarchy && renderer.enabled)
                        {
                            validRenderers.Add(renderer);
                        }
                    }

                    if (validRenderers.Count == 0)
                    {
                        continue;
                    }

                    passData.viewMatrices.Add(vl.ViewMatrix);
                    passData.projectionMatrices.Add(vl.ProjectionMatrix);
                    passData.depthRenderTextures.Add(depthRT);
                    passData.casterRendererLists.Add(validRenderers);
                    hasValidLight = true;
                }

                if (!hasValidLight)
                {
                    return;
                }

                // UnsafePassのレンダリング関数を設定
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    ExecutePass(data, context);
                });
            }
        }

        /// <summary>
        /// UnsafePass内で実行される描画処理。
        /// 各VirtualLightごとに深度RenderTextureへのレンダリングを行う。
        /// CommandBuffer.DrawRendererで対象Rendererを直接描画参照し、
        /// Rendererに一切の変更を加えない。
        /// </summary>
        /// <param name="data">PassData（VP行列、深度RT、キャスターリスト等）</param>
        /// <param name="context">UnsafeGraphContext（CommandBuffer取得元）</param>
        private static void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            // UnsafeGraphContextからネイティブCommandBufferを取得
            var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            for (int lightIndex = 0; lightIndex < data.depthRenderTextures.Count; lightIndex++)
            {
                var depthRT = data.depthRenderTextures[lightIndex];
                var viewMatrix = data.viewMatrices[lightIndex];
                var projMatrix = data.projectionMatrices[lightIndex];
                var casters = data.casterRendererLists[lightIndex];

                // 深度RenderTextureをレンダーターゲットに設定
                cmd.SetRenderTarget(depthRT);

                // 深度バッファをクリア（深度を最大値=1.0にクリア）
                cmd.ClearRenderTarget(true, false, Color.clear, 1.0f);

                // 仮想光源のView/Projection行列を設定
                // これにより、シェーダー内のUNITY_MATRIX_V、UNITY_MATRIX_P、
                // UNITY_MATRIX_VPが仮想光源の行列に置き換わる
                cmd.SetViewProjectionMatrices(viewMatrix, projMatrix);

                // 各キャスターRendererをDepthOnly Materialで描画
                // CommandBuffer.DrawRendererはRendererのメッシュを指定Materialで描画する
                // 対象Rendererには一切変更を加えない（Layer、Material、コンポーネント等）
                for (int r = 0; r < casters.Count; r++)
                {
                    var renderer = casters[r];
                    // 描画時点でのnullチェック（実行中にRendererが破棄される可能性への対応）
                    if (renderer == null)
                    {
                        continue;
                    }

                    // 複数サブメッシュに対応するため、Renderer内の全Materialスロット分を描画
                    // sharedMaterialsの長さがサブメッシュ数に対応する
                    // SkinnedMeshRendererの場合も現在のアニメーション状態がそのまま反映される
                    int submeshCount = renderer.sharedMaterials.Length;
                    for (int s = 0; s < submeshCount; s++)
                    {
                        cmd.DrawRenderer(renderer, data.depthOnlyMaterial, s, 0);
                    }
                }
            }

            // 元のカメラView/Projection行列を復元する
            cmd.SetViewProjectionMatrices(data.cameraViewMatrix, data.cameraProjectionMatrix);
        }
    }
}
