# HANDOVER

対象: `ShadowOnlyShader/Packages/com.hidano.shadow-only-shader`（Unity 6 / URP 17・RenderGraph）

## ◯ 今回やったこと

- **「影が縞々になる」問題の原因調査**（調査のみ。コード変更なし）。
- スクリーンショットの画像解析（拡大・ローパス・彩度/R-B マップ）とコードリーディングで仮説を4つ検証:
  1. シャドウアクネ（床がキャスター混入）→ 棄却（CasterRoot はキャラのみ）
  2. ブラー疎カーネルのゴースト（Blur Radius 起因）→ 機序は正しいが支配要因の特定が誤り
  3. Z ファイティング（キャッチャーと可視床の同一平面）→ 棄却（キャッチャーは +0.01 済み）
  4. 色収差の離散5タップ → 棄却（CA は全灯 0）
- **原因確定（ユーザー実験による）**: `Orthographic Size = 40`（投影範囲 80m×80m）が原因。5 に下げたら縞消滅。
  - 機序: テクセル実寸 = OrthoSize×2 ÷ 解像度。ブラーのサンプル間隔は「Blur Radius **テクセル**」なので、テクセルが数 cm になると二値影のコピーが分離して等間隔の縞になる（疎カーネルゴースト。支配要因はテクセル実寸）。
  - ユーザーが Size 40 にしていた理由: **ライトの角度によってキャスターが投影範囲から外れ影が途切れる**ため。
- 解決方針を合意: **Fit To Casters（投影範囲のキャスター自動追従）を実装する**（次セッションの作業）。

## ◯ 決定事項

- **Fit To Casters を VirtualLight にオプトインで実装**（既存の手動 OrthoSize 運用はデフォルト維持）。
- 実装方式:
  1. 毎フレームの行列更新時（LateUpdate 系）に `CasterRenderers` の合成 `Bounds` をライト空間へ変換
  2. XY 範囲を覆う**オフセンター正射影**（`Matrix4x4.Ortho(l, r, b, t, near, far)`）を生成。マージン +10% 程度 + ブラーカーネル分の余白
  3. **テクセルスナップ**（投影ウィンドウ原点を 1 テクセル単位に量子化）で shadow swimming を防止
- シャドウマップはキャスターの UV 範囲だけ覆えばよい（受影点はライト空間で同じ UV に射影されるため、レシーバー範囲を含める必要はない）。

## ◯ 捨てた選択肢と理由

- **アクネ対策（Offset / NormalBias 実装 / bias 調整）を今回の修正とする** → 縞の原因ではなかった。ただし下記「学び」の別件バグ・改善は別途対応の価値あり。
- **ブラーを screen-space 分離ガウシアン（Resolve RT 上）に作り直す** → 根本対策としては有効だが工事が大きい。Fit To Casters でテクセル実寸が小さく保たれれば縞は実用上解決するため見送り。
- **Poisson disk + IGN ジッタ化** → 同上。Fit To Casters 後もゴーストが残る場合の次の手として保留。
- **実効 blurRadius のクランプ** → 対症療法で見た目のボケ幅も制限されるため不採用。

## ◯ ハマりどころ

- スクリーンショットのシーンはリポジトリの SampleScene と**別物**（利用側プロジェクト、キャラ2体）。コミット済みシーンの値（OrthoSize 5, CA 0.05 等）から実環境を推定すると誤る。実際の値はユーザーに聞くこと。
- 縞の見た目（世界空間の平行帯・ブラー品質で幅と濃さだけ変化・Blur Radius 無反応）から仮説を絞ったが、最終確定はユーザーの実機実験だった。リモート推理より「切り分け実験の依頼」を早めに出すのが効率的。

## ◯ 学び

調査中に見つけた**別件の問題**（今回の縞とは無関係だが修正価値あり）:

- **NormalBias が未実装**: `VirtualLight._normalBias` はシリアライズ・Inspector 表示されるが、`ShadowOnlyManager.UpdateMaterialProperties()` が転送せず、HLSL にも処理がない。完全に無反応。実装するか削除すべき。
- **Perspective 投影の DepthBias が 1/w² スケール**（`ShadowOnlyFloorCommon.hlsl:302`）: 実効値が距離の2乗で減衰し、ユーザー設定がほぼ効かない。効く値まで上げると診断が「過大」警告を出す矛盾。
- 深度テクスチャ 8192 × 灯数スライスは D24 で数百 MB 級。`EnsureDepthArrayTexture` の作成失敗→解像度半減フォールバックが黙って効いている可能性がある（Console 警告確認）。
- ブラーの構造: 見た目のボケ幅 = 2R × blurRadius テクセル（R: Low=2/Mid=4/High=6）、ゴースト周期 = blurRadius × テクセル実寸。望むボケ幅 W に対し縞周期は常に W/(2R)。
- 色収差はブラー有効時 5 タップ・無効時 11 タップの離散サンプリング（強度を上げると同種の縞が出る潜在リスク）。

## ◯ 次にやること

1. **【最優先】Fit To Casters の実装**（ユーザー合意済み）:
   - `VirtualLight` に `_fitToCasters`（bool、デフォルト false）追加
   - 行列計算（`CalculateProjectionMatrix` / `UpdateMatrices` 周辺）でキャスター Bounds →ライト空間 AABB →オフセンター Ortho + マージン + テクセルスナップ
   - Orthographic モードのみ対象（Perspective/Point は対象外でよいか実装時に判断）
   - `VirtualLightEditor` に UI 追加（Fit 有効時は OrthoSize をグレーアウト等）
   - Edit モード（診断が `UpdateMatrices()` を呼ぶ経路）でも破綻しないこと
   - テスト追加（`Tests/Editor/VirtualLightMatrixTests.cs` 周辺に）
2. 【推奨・小】診断に「OrthoSize 過大（テクセル実寸が閾値超え）」警告を追加 — 今回の問題の再発防止。テクセル実寸 = Size×2÷解像度 で判定
3. 【任意】NormalBias 未実装の解消（実装 or フィールド削除）、Perspective bias 1/w² の見直し
4. CHANGELOG / README 更新、バージョン更新はユーザーに確認

## ◯ 関連ファイル

調査で読んだ主要ファイル（今回変更なし）:

- `ShadowOnlyShader/Packages/com.hidano.shadow-only-shader/Runtime/VirtualLight.cs`（行列計算・OrthoSize・実装対象の中心）
- `ShadowOnlyShader/Packages/com.hidano.shadow-only-shader/Runtime/ShadowOnlyManager.cs`（パラメータ転送 `UpdateMaterialProperties` / 深度 Tex2DArray 管理）
- `ShadowOnlyShader/Packages/com.hidano.shadow-only-shader/Runtime/ShadowOnlyRenderPass.cs`（深度パス、スライスごと描画）
- `ShadowOnlyShader/Packages/com.hidano.shadow-only-shader/Runtime/ShadowOnlyShadowResolvePass.cs`（低解像度 Resolve）
- `ShadowOnlyShader/Packages/com.hidano.shadow-only-shader/Runtime/Shaders/ShadowOnlyFloorCommon.hlsl`（影判定・ブラー・CA。縞の機序はここのブラーループ）
- `ShadowOnlyShader/Packages/com.hidano.shadow-only-shader/Runtime/Shaders/ShadowOnlyFloor.shader` / `ShadowOnlyFloorDisplay.shader` / `ShadowOnlyDepth.shader`
- `ShadowOnlyShader/Assets/Scenes/SampleScene.unity`（サンプルシーン。実環境とは別物な点に注意）
