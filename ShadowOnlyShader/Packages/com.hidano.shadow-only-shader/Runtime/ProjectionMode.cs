namespace ShadowOnlyShader
{
    /// <summary>
    /// 仮想光源の投影方式を定義する列挙型。
    /// Orthographicは平行光源、Perspectiveは点光源/スポットライトに相当する影を生成する。
    /// </summary>
    public enum ProjectionMode
    {
        /// <summary>
        /// 正射影。平行光源に相当し、均一なサイズの影を生成する。
        /// OrthographicSizeパラメータで投影範囲を制御する。
        /// </summary>
        Orthographic,

        /// <summary>
        /// 透視投影。点光源/スポットライトに相当し、距離に応じたパースのある影を生成する。
        /// FOVパラメータで投影角度を制御する。
        /// </summary>
        Perspective
    }
}
