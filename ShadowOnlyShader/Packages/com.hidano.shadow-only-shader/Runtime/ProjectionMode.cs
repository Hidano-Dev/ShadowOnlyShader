namespace ShadowOnlyShader
{
    /// <summary>
    /// 仮想光源の投影方式を定義する列挙型。
    /// Orthographicは平行光源、Perspectiveはスポットライト、
    /// Pointはポイントライト（全方向）に相当する影を生成する。
    /// </summary>
    public enum ProjectionMode
    {
        /// <summary>
        /// 正射影。平行光源に相当し、均一なサイズの影を生成する。
        /// OrthographicSizeパラメータで投影範囲を制御する。
        /// </summary>
        Orthographic,

        /// <summary>
        /// 透視投影。スポットライトに相当し、距離に応じたパースのある影を生成する。
        /// FOVパラメータで投影角度を制御する。
        /// </summary>
        Perspective,

        /// <summary>
        /// 全方向投影。ポイントライトに相当し、FOV 90°の透視投影6面（±X/±Y/±Z）で
        /// 全方向に影を生成する。深度テクスチャを6スライス消費する。
        /// 面の向きはワールド軸基準で、Transformの回転は影響しない。
        /// </summary>
        Point
    }
}
