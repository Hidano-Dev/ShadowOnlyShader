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
        /// RenderGraphにUnsafePassを登録し、深度テクスチャの生成を行う。
        /// Task 5.2で完全な実装を行う。
        /// </summary>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Task 5.2で深度テクスチャ生成の完全な実装を行う
            // 現段階ではスケルトンのみ
        }
    }
}
