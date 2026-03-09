namespace ShadowOnlyShader
{
    /// <summary>
    /// 影のブラー品質プリセットを定義する列挙型。
    /// サンプル数が増えるほど滑らかなボケが得られるが、GPUの負荷も増加する。
    /// </summary>
    public enum BlurQuality
    {
        /// <summary>
        /// 低品質。5x5カーネル、約9サンプル。リアルタイム用途向け。
        /// </summary>
        Low,

        /// <summary>
        /// 中品質。9x9カーネル、約25サンプル。バランス型。
        /// </summary>
        Mid,

        /// <summary>
        /// 高品質。13x13カーネル、約49サンプル。高品質レンダリング向け。
        /// </summary>
        High
    }
}
