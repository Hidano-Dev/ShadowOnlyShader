using System.Collections.Generic;
using UnityEngine;

namespace ShadowOnlyShader
{
    /// <summary>
    /// ShadowOnlyManagerの公開インターフェース。
    /// 仮想光源・床面Renderer・グローバルパラメータの管理を提供する。
    /// </summary>
    public interface IShadowOnlyManager
    {
        /// <summary>
        /// 管理中の仮想光源リストを取得する。
        /// 子GameObjectのVirtualLightコンポーネントと常に同期される。
        /// </summary>
        IReadOnlyList<IVirtualLight> VirtualLights { get; }

        /// <summary>
        /// 新しい仮想光源を追加する。
        /// 子GameObjectを生成し、VirtualLightコンポーネントをアタッチして返す。
        /// </summary>
        /// <returns>追加された仮想光源のインターフェース</returns>
        IVirtualLight AddVirtualLight();

        /// <summary>
        /// 指定した仮想光源を削除する。
        /// 対応する子GameObjectも破棄される。
        /// </summary>
        /// <param name="light">削除する仮想光源</param>
        void RemoveVirtualLight(IVirtualLight light);

        /// <summary>
        /// 登録済みの床面Rendererリストを取得する。
        /// </summary>
        IReadOnlyList<Renderer> FloorRenderers { get; }

        /// <summary>
        /// 床面Rendererを追加登録する。
        /// </summary>
        /// <param name="renderer">追加するRenderer</param>
        void AddFloorRenderer(Renderer renderer);

        /// <summary>
        /// 床面Rendererを登録解除する。
        /// </summary>
        /// <param name="renderer">削除するRenderer</param>
        void RemoveFloorRenderer(Renderer renderer);

        /// <summary>
        /// ブラー品質プリセット。全仮想光源に共通で適用される。
        /// </summary>
        BlurQuality BlurQuality { get; set; }

        /// <summary>
        /// 複数仮想光源の影を加算合成する際の倍率パラメータ。
        /// </summary>
        float BlendMultiplier { get; set; }
    }
}
