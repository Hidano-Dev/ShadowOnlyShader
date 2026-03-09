using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShadowOnlyShader
{
    /// <summary>
    /// 影のみ描画システムのScriptableRendererFeature。
    /// URP Universal Rendererに統合し、ShadowOnlyRenderPassの生成・管理を行う。
    /// Requirements: 1.4, 6.1, 6.2
    /// </summary>
    public class ShadowOnlyRendererFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// 深度テクスチャ生成用のRenderPass。
        /// Createメソッドで生成される。
        /// </summary>
        private ShadowOnlyRenderPass _renderPass;

        /// <summary>
        /// 深度描画に使用するDepthOnly Material。
        /// Createメソッドで生成され、Disposeで破棄される。
        /// </summary>
        private Material _depthOnlyMaterial;

        /// <summary>
        /// Manager不在時の警告ログ出力を制御するフラグ。
        /// 毎フレーム警告が出力されないようにする。
        /// </summary>
        private bool _warnedNoManager;

        /// <summary>
        /// ShadowOnlyRenderPassを生成し、初期設定を行う。
        /// RenderPassEventをBeforeRenderingOpaquesに設定する。
        /// </summary>
        public override void Create()
        {
            _renderPass = new ShadowOnlyRenderPass();
            _renderPass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;

            // DepthOnly Materialを生成
            CreateDepthOnlyMaterial();

            if (_depthOnlyMaterial != null)
            {
                _renderPass.SetDepthOnlyMaterial(_depthOnlyMaterial);
            }
        }

        /// <summary>
        /// シーン内のShadowOnlyManagerを検索し、RenderPassにデータを渡してレンダラーに登録する。
        /// Managerが存在しない場合はPassをスキップし、警告ログを出力する。
        /// </summary>
        /// <param name="renderer">ScriptableRenderer</param>
        /// <param name="renderingData">レンダリングデータ</param>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_renderPass == null) return;

            // シーン内のアクティブなShadowOnlyManagerを検索
            var manager = FindActiveManager();

            if (manager == null)
            {
                // Managerが存在しない場合はPassをスキップし、警告を一度だけ出力
                if (!_warnedNoManager)
                {
                    Debug.LogWarning(
                        "[ShadowOnlyShader] シーン内にアクティブなShadowOnlyManagerが見つかりません。" +
                        "ShadowOnlyManagerコンポーネントをシーンに追加してください。");
                    _warnedNoManager = true;
                }
                return;
            }

            // Managerが見つかった場合は警告フラグをリセット
            _warnedNoManager = false;

            // RenderPassにManagerの参照を設定
            _renderPass.SetManager(manager);

            // レンダラーにPassを登録
            renderer.EnqueuePass(_renderPass);
        }

        /// <summary>
        /// リソースの解放を行う。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (_depthOnlyMaterial != null)
            {
                DestroyImmediate(_depthOnlyMaterial);
                _depthOnlyMaterial = null;
            }

            _renderPass = null;
        }

        /// <summary>
        /// シーン内のアクティブなShadowOnlyManagerを検索する。
        /// 複数存在する場合は最初に見つかったものを返す。
        /// </summary>
        /// <returns>アクティブなShadowOnlyManager。存在しない場合はnull。</returns>
        private ShadowOnlyManager FindActiveManager()
        {
            return FindAnyObjectByType<ShadowOnlyManager>();
        }

        /// <summary>
        /// DepthOnly Materialを生成する。
        /// HideFlags.DontSaveを設定し、シーン保存時に永続化しない。
        /// </summary>
        private void CreateDepthOnlyMaterial()
        {
            if (_depthOnlyMaterial != null) return;

            var shader = Shader.Find("Hidden/ShadowOnly/DepthOnly");
            if (shader == null)
            {
                Debug.LogError(
                    "[ShadowOnlyShader] Hidden/ShadowOnly/DepthOnly シェーダーが見つかりません。" +
                    "パッケージのシェーダーが正しくインポートされていることを確認してください。");
                return;
            }

            _depthOnlyMaterial = new Material(shader);
            _depthOnlyMaterial.hideFlags = HideFlags.DontSave;
        }
    }
}
