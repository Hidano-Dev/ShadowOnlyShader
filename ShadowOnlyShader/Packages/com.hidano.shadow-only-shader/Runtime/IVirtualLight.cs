using System.Collections.Generic;
using UnityEngine;

namespace ShadowOnlyShader
{
    /// <summary>
    /// 仮想光源の公開インターフェース。
    /// 投影パラメータ、影の外観パラメータ、VP行列計算、深度RenderTexture管理を提供する。
    /// </summary>
    public interface IVirtualLight
    {
        /// <summary>
        /// 投影方式。OrthographicまたはPerspectiveを選択する。
        /// </summary>
        ProjectionMode ProjectionMode { get; set; }

        /// <summary>
        /// Perspective投影時の視野角（度数）。1-179度の範囲。
        /// </summary>
        float FieldOfView { get; set; }

        /// <summary>
        /// Orthographic投影時の投影サイズ。0より大きい値。
        /// </summary>
        float OrthographicSize { get; set; }

        /// <summary>
        /// ニアクリップ面の距離。
        /// </summary>
        float NearClipPlane { get; set; }

        /// <summary>
        /// ファークリップ面の距離。
        /// </summary>
        float FarClipPlane { get; set; }

        /// <summary>
        /// 深度テクスチャの解像度（ピクセル）。64-4096の範囲にクランプされる。
        /// </summary>
        int TextureResolution { get; set; }

        /// <summary>
        /// 影の色。
        /// </summary>
        Color ShadowColor { get; set; }

        /// <summary>
        /// 影の濃さ（アルファ値）。0.0-1.0の範囲。
        /// </summary>
        float ShadowAlpha { get; set; }

        /// <summary>
        /// ブラー半径。影のボケ具合を制御する。
        /// </summary>
        float BlurRadius { get; set; }

        /// <summary>
        /// 距離ボケ係数。光源からの距離に応じたブラー半径の変化を制御する。
        /// </summary>
        float BlurDistanceFactor { get; set; }

        /// <summary>
        /// カメラ距離ボケ係数。カメラからの距離に応じたブラー半径の変化を制御する。
        /// 値が大きいほど、カメラから遠い影ほどぼやける。
        /// </summary>
        float BlurCameraDistanceFactor { get; set; }

        /// <summary>
        /// カメラ距離アルファ減衰係数。カメラからの距離に応じて影の濃さを減衰させる。
        /// 値が大きいほど、カメラから遠い影ほど薄くなる（近い影は濃いまま保たれる）。
        /// </summary>
        float AlphaCameraDistanceFactor { get; set; }

        /// <summary>
        /// カメラ距離効果のべき乗指数。距離をべき乗してからFactorを掛ける。
        /// 1.0で線形（デフォルト）、1より大きいと遠方で急激に効き、1未満だと近くから素早く効き始める。
        /// BlurCameraDistanceFactorとAlphaCameraDistanceFactorの両方に適用される。
        /// </summary>
        float CameraDistancePower { get; set; }

        /// <summary>
        /// Hue Shift（色相ズレ）。度数（0-360）で指定する。
        /// </summary>
        float HueShift { get; set; }

        /// <summary>
        /// 色収差強度。RGBチャンネルを影の中心からの放射方向に分離する。
        /// </summary>
        float ChromaticAberration { get; set; }

        /// <summary>
        /// 色収差の光源参照。設定されている場合、このLightの色がスペクトル重みの変調に使用される。
        /// nullの場合はChromaticAberrationColorがフォールバックとして使用される。
        /// </summary>
        Light SourceLight { get; set; }

        /// <summary>
        /// SourceLightからTransformと投影パラメータを自動同期するかどうか。
        /// trueの場合、SourceLightの位置・回転・LightType・SpotAngle・Range等を毎フレーム反映する。
        /// </summary>
        bool SyncWithSourceLight { get; set; }

        /// <summary>
        /// 色収差のフォールバック光源色。SourceLightが未設定の場合に使用される。
        /// 光源の色に応じてRGB各波長帯の寄与率が変化する。
        /// 白(1,1,1)=標準CA、単色光=CAなし（物理的に正しい挙動）。
        /// </summary>
        Color ChromaticAberrationColor { get; set; }

        /// <summary>
        /// 実効的な色収差光源色（読み取り専用）。
        /// SourceLightが設定されていればその色、なければChromaticAberrationColorを返す。
        /// </summary>
        Color EffectiveChromaticAberrationColor { get; }

        /// <summary>
        /// コンタクトハードニング強度。PCSS（Percentage Closer Soft Shadows）による
        /// キャスター近接部のシャープな影から遠方の柔らかい影への自然なグラデーションを制御する。
        /// 仮想的な光源サイズに相当し、値が大きいほど半影の広がりが大きくなる。
        /// 0の場合はコンタクトハードニングを無効にし、従来の均一ブラーが適用される。
        /// </summary>
        float ContactHardeningStrength { get; set; }

        /// <summary>
        /// 深度バイアス。シャドウアクネを防止するための深度オフセット。
        /// </summary>
        float DepthBias { get; set; }

        /// <summary>
        /// 法線バイアス。シャドウアクネを防止するための法線方向オフセット。
        /// </summary>
        float NormalBias { get; set; }

        /// <summary>
        /// キャスターのルートGameObject。子階層の全Rendererが影の投影元として使用される。
        /// </summary>
        GameObject CasterRoot { get; set; }

        /// <summary>
        /// CasterRoot配下から収集されたRendererのリスト（読み取り専用）。
        /// </summary>
        IReadOnlyList<Renderer> CasterRenderers { get; }

        /// <summary>
        /// View行列（読み取り専用）。Transformの位置・回転から算出される。
        /// </summary>
        Matrix4x4 ViewMatrix { get; }

        /// <summary>
        /// Projection行列（読み取り専用）。ProjectionModeに応じて算出される。
        /// </summary>
        Matrix4x4 ProjectionMatrix { get; }

        /// <summary>
        /// View * Projection行列（読み取り専用）。
        /// </summary>
        Matrix4x4 ViewProjectionMatrix { get; }

        /// <summary>
        /// 深度RenderTexture（読み取り専用）。仮想光源ごとに管理される。
        /// </summary>
        RenderTexture DepthRenderTexture { get; }

        /// <summary>
        /// CasterRoot配下の全Rendererを再収集する。
        /// </summary>
        void CollectRenderers();

        /// <summary>
        /// View行列とProjection行列を現在のTransformとパラメータから再計算する。
        /// </summary>
        void UpdateMatrices();

        /// <summary>
        /// 深度RenderTextureの存在と解像度を確認し、必要に応じて作成・再作成する。
        /// 解像度が変更された場合は古いRenderTextureを破棄して新しく作成する。
        /// </summary>
        void EnsureDepthTexture();
    }
}
