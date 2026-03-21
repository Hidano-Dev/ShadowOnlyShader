using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace ShadowOnlyShader
{
    /// <summary>
    /// 影のみ描画用のRenderPass。
    /// RenderGraph UnsafePassおよびレガシーExecuteの両方に対応し、
    /// キャスターRendererをTexture2DArrayの各スライスに深度描画する。
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

            /// <summary>深度Texture2DArray（全VirtualLight共通）。</summary>
            public RenderTexture depthArrayTexture;

            /// <summary>描画対象のライト数。</summary>
            public int lightCount;

            /// <summary>キャスターRendererリストのリスト（各VirtualLight分）。</summary>
            public List<List<Renderer>> casterRendererLists = new List<List<Renderer>>();

            /// <summary>深度描画に使用するDepthOnly Material。</summary>
            public Material depthOnlyMaterial;

            /// <summary>元のカメラView行列（描画後に復元する）。</summary>
            public Matrix4x4 cameraViewMatrix;

            /// <summary>元のカメラProjection行列（描画後に復元する）。</summary>
            public Matrix4x4 cameraProjectionMatrix;

            /// <summary>カメラのカラーターゲット（RenderGraphパスでのレンダーターゲット復元用）。</summary>
            public RTHandle cameraColorTarget;

            /// <summary>カメラのデプスターゲット（RenderGraphパスでのレンダーターゲット復元用）。</summary>
            public RTHandle cameraDepthTarget;

            /// <summary>Rendererリストのプール（GCアロケーション回避）。</summary>
            private readonly List<List<Renderer>> _rendererListPool = new List<List<Renderer>>();
            private int _poolUsedCount;

            /// <summary>
            /// プールからRendererリストを取得する。
            /// プールが不足している場合は新規作成してプールに追加する。
            /// </summary>
            public List<Renderer> GetPooledRendererList()
            {
                if (_poolUsedCount < _rendererListPool.Count)
                {
                    var list = _rendererListPool[_poolUsedCount];
                    list.Clear();
                    _poolUsedCount++;
                    return list;
                }
                var newList = new List<Renderer>();
                _rendererListPool.Add(newList);
                _poolUsedCount++;
                return newList;
            }

            /// <summary>
            /// PassDataの内容をクリアする。
            /// 毎フレームの再利用のために使用する。
            /// </summary>
            public void Clear()
            {
                viewMatrices.Clear();
                projectionMatrices.Clear();
                depthArrayTexture = null;
                lightCount = 0;
                casterRendererLists.Clear();
                _poolUsedCount = 0;
                depthOnlyMaterial = null;
                cameraColorTarget = null;
                cameraDepthTarget = null;
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
        /// レガシーパス用のPassDataキャッシュ。
        /// 毎フレームのGCアロケーションを避けるために再利用する。
        /// </summary>
        private readonly PassData _legacyPassData = new PassData();

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

        #region 共通データ収集

        /// <summary>
        /// VirtualLightからPassDataを収集する共通処理。
        /// RecordRenderGraphとExecuteの両方から使用される。
        /// LateUpdateで行列計算済みのデータを使用する（重複計算なし）。
        /// </summary>
        /// <param name="passData">データを格納するPassData</param>
        /// <param name="cameraViewMatrix">カメラのView行列</param>
        /// <param name="cameraProjectionMatrix">カメラのProjection行列</param>
        /// <returns>有効なライトが1つ以上あればtrue</returns>
        private bool CollectPassData(PassData passData, Matrix4x4 cameraViewMatrix, Matrix4x4 cameraProjectionMatrix)
        {
            passData.Clear();
            passData.depthOnlyMaterial = _depthOnlyMaterial;
            passData.cameraViewMatrix = cameraViewMatrix;
            passData.cameraProjectionMatrix = cameraProjectionMatrix;

            // ManagerからTexture2DArrayを取得
            passData.depthArrayTexture = _manager.DepthArrayTexture;
            if (passData.depthArrayTexture == null)
            {
                return false;
            }

            var virtualLights = _manager.VirtualLights;
            int validCount = 0;

            for (int i = 0; i < virtualLights.Count && validCount < 8; i++)
            {
                var vl = virtualLights[i] as VirtualLight;
                if (vl == null || !vl.isActiveAndEnabled)
                {
                    continue;
                }

                // キャスターRendererの収集（dirtyフラグによりキャッシュ済みの場合はスキップ）
                vl.CollectRenderers();
                var casterRenderers = vl.CasterRenderers;

                // 有効なRendererのみをフィルタリングしてプールされたリストに追加
                var validRenderers = passData.GetPooledRendererList();
                if (casterRenderers != null)
                {
                    for (int r = 0; r < casterRenderers.Count; r++)
                    {
                        var renderer = casterRenderers[r];
                        if (renderer != null && renderer.gameObject.activeInHierarchy && renderer.enabled)
                        {
                            validRenderers.Add(renderer);
                        }
                    }
                }

                // Rendererが0件でも深度テクスチャのクリアが必要なためスキップしない
                passData.viewMatrices.Add(vl.ViewMatrix);
                passData.projectionMatrices.Add(vl.ProjectionMatrix);
                passData.casterRendererLists.Add(validRenderers);
                validCount++;
            }

            passData.lightCount = validCount;
            return validCount > 0;
        }

        #endregion

        #region 共通描画処理

        /// <summary>
        /// CommandBufferを使用して深度描画を実行する共通処理。
        /// Texture2DArrayの各スライスに対してレンダリングする。
        /// RenderGraphパスとレガシーパスの両方から使用される。
        /// </summary>
        /// <param name="cmd">CommandBuffer</param>
        /// <param name="data">PassData</param>
        private static void ExecuteDrawCommands(CommandBuffer cmd, PassData data)
        {
            for (int lightIndex = 0; lightIndex < data.lightCount; lightIndex++)
            {
                var viewMatrix = data.viewMatrices[lightIndex];
                var projMatrix = data.projectionMatrices[lightIndex];
                var casters = data.casterRendererLists[lightIndex];

                // Texture2DArrayの該当スライスをレンダーターゲットに設定
                cmd.SetRenderTarget(data.depthArrayTexture, 0, CubemapFace.Unknown, lightIndex);

                // 深度バッファをクリア（深度を最大値=1.0にクリア）
                cmd.ClearRenderTarget(true, false, Color.clear, 1.0f);

                // 仮想光源のView/Projection行列を設定
                cmd.SetViewProjectionMatrices(viewMatrix, projMatrix);

                // 各キャスターRendererをDepthOnly Materialで描画
                for (int r = 0; r < casters.Count; r++)
                {
                    var renderer = casters[r];
                    if (renderer == null)
                    {
                        continue;
                    }

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

        #endregion

        #region レガシーパス (Execute)

        /// <summary>
        /// レガシーレンダリングパス用のExecute実装。
        /// RenderGraphが無効な環境（Compatibility Mode）で使用される。
        /// </summary>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_manager == null || _depthOnlyMaterial == null)
            {
                return;
            }

            var virtualLights = _manager.VirtualLights;
            if (virtualLights == null || virtualLights.Count == 0)
            {
                return;
            }

            var camera = renderingData.cameraData.camera;

            if (!CollectPassData(_legacyPassData, camera.worldToCameraMatrix, camera.projectionMatrix))
            {
                return;
            }

            var cmd = CommandBufferPool.Get("ShadowOnly Depth Pass");
            ExecuteDrawCommands(cmd, _legacyPassData);

            // レガシーパスではレンダーターゲットが深度RTのまま残るため、
            // カメラのカラー/デプスターゲットに明示的に復元する必要がある。
            // （RenderGraphパスではフレームワークが自動管理するため不要）
#pragma warning disable CS0618 // レガシーパス用APIのdeprecation警告を抑制
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
        /// RenderGraphにUnsafePassを登録し、Texture2DArrayの各スライスに深度テクスチャを生成する。
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
            var resourceData = frameData.Get<UniversalResourceData>();

            using (var builder = renderGraph.AddUnsafePass<PassData>(
                "ShadowOnly Depth Pass", out var passData))
            {
                if (!CollectPassData(passData, camera.worldToCameraMatrix, camera.projectionMatrix))
                {
                    return;
                }

                // カメラのカラー/デプスターゲットをRenderGraphに依存宣言する。
                // UnsafePassが外部レンダーターゲット（深度RT）に描画した後、
                // カメラターゲットを正しく復元するために必要。
                builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Write);
                builder.UseTexture(resourceData.activeDepthTexture, AccessFlags.Write);

                // レンダーターゲット復元用にカメラのRTHandleを保存
#pragma warning disable CS0618 // RenderGraph環境でのdeprecation警告を抑制（レンダーターゲット復元にはRTHandleが必要）
                passData.cameraColorTarget = cameraData.renderer.cameraColorTargetHandle;
                passData.cameraDepthTarget = cameraData.renderer.cameraDepthTargetHandle;
#pragma warning restore CS0618

                // UnsafePassのレンダリング関数を設定
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    ExecuteDrawCommands(cmd, data);

                    // UnsafePassではフレームワークによるレンダーターゲットの自動復元が行われないため、
                    // カメラのカラー/デプスターゲットを明示的に復元する。
                    // Unity RecorderのRenderTexture経由録画など、カメラが非デフォルトターゲットに
                    // 描画する場合、この復元がないと後続パスが深度RTに描画されてしまう。
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
