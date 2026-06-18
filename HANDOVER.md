# HANDOVER

対象: `Packages/com.hidano.shadow-only-shader`（Unity 6.3 / URP 17・RenderGraph）

## ◯ 今回やったこと

- README 修正: install の git URL を `HidanoDev` → `Hidano-Dev` に訂正し、サブフォルダ指定 `?path=ShadowOnlyShader/Packages/com.hidano.shadow-only-shader` を追加。
- README セットアップ順を変更: 「床面登録 → 仮想光源追加」の順に。Play中のみ影が更新される旨も追記。
- `ShadowOnlyManagerEditor.cs`: 「床面Renderer」フィールドの GUIContent に tooltip を追加（他フィールドは既に `[Tooltip]` 済み）。
- **残像バグ修正（本セッションの主成果）**: `ShadowOnlyRenderPass.cs` / `ShadowOnlyShadowResolvePass.cs` の `RecordRenderGraph` を修正。
  - 非推奨 `cameraData.renderer.cameraColorTargetHandle`（記録フェーズで例外）を削除。
  - カメラカラー/デプスへの `UseTexture(AccessFlags.Write)` と手動レンダーターゲット復元を削除。
  - `builder.AllowGlobalStateModification(true)` を追加。
  - 不要になった PassData フィールド `cameraColorTarget`/`cameraDepthTarget` を除去。
  - → ユーザー確認済みで残像解消。

## ◯ 決定事項

- このシステムは **Play中のみ動作**（`LateUpdate` 駆動。`ExecuteAlways` 無し）。Editモードの影は URP標準シャドウであり別物。
- **Light Layer / RenderingLayerMask / cullingMask は一切非対応**。キャスター＝各 VirtualLight の `CasterRoot` 配下、レシーバ＝Manager の `Floor Renderers`。
- レガシー `Execute`（Compatibility Mode）側の `cameraColorTargetHandle` 参照はスコープ内で正当 → 変更しない。
- 実行プロジェクトは git 取得版（`Library/PackageCache/...@hash`）。修正反映には push 後に再取得（Package Manager Update、または `Packages/packages-lock.json` の該当エントリ削除で再解決）が必要。

## ◯ 捨てた選択肢と理由

- 残像対策で `UseTexture(Write)` を残して `AccessFlags.Read` に変える案 → 採用せず。外部RTにしか描かないパスがカメラカラーに依存宣言すること自体が不要かつ有害（`BeforeRenderingOpaques` で URP のクリアを奪う）。宣言ごと削除が正解。
- 行列ロジック（`GL.GetGPUProjectionMatrix` vs `SetViewProjectionMatrices`）の投機的書き換え → 保留。サンプルでは動く前提のため、位置ずれの原因確定前に触らない。

## ◯ ハマりどころ

- 影が出なかった主因は **Quality 設定の URP アセット未差し替え**。Graphics 設定だけ直しても、Quality レベル側のアセットが優先されるため Renderer Feature が動かず、警告も出ない（`AddRenderPasses` が呼ばれないため）。
- `Shadow Alpha` は `Source Light` 設定時に Inspector から隠れ、値は SpotLight の `Shadow Strength` に同期される。
- 残像（クリアされず前フレーム蓄積）は RenderGraph 経路特有。Feature オフで消えることで package 起因と切り分けた。

## ◯ 学び

- UnsafePass で外部RTに描く場合、カメラターゲットを `UseTexture(Write)` 宣言すると URP のクリアを奪う。外部RTのみのパスはカメラターゲットに触れず、`AllowGlobalStateModification(true)` + `AllowPassCulling(false)` で十分。
- `cameraColorTargetHandle` は RenderGraph の記録フェーズでは呼べない（実行スコープ内のみ）。

## ◯ 次にやること

1. **【要・実機確認】影の位置ずれ修正済み（本セッション）**。原因は `ShadowOnlyFloorCommon.hlsl` の手動Yフリップ `#if UNITY_UV_STARTS_AT_TOP { shadowUV.y = 1 - shadowUV.y }`（2箇所）。コミット `f7f9b66` で `_LightVPMatrices` を `GL.GetGPUProjectionMatrix(proj, true)` のGPU変換済みに変更した際、D3D では行列内に既にY反転が含まれるため手動フリップが二重反転となり縦方向にずれていた（GL系は `UNITY_UV_STARTS_AT_TOP` が false で元々無害）。両分岐を削除して解決。→ Play中に Windows(D3D) 実機で影位置が一致するか確認すること。
2. （任意）package.json の version を上げ、Package Manager Update での反映を容易にする。
3. 変更一式を commit & push し、実行プロジェクト側で再取得。

## ◯ 関連ファイル

- `ShadowOnlyShader/Packages/com.hidano.shadow-only-shader/Runtime/ShadowOnlyRenderPass.cs`
- `ShadowOnlyShader/Packages/com.hidano.shadow-only-shader/Runtime/ShadowOnlyShadowResolvePass.cs`
- `ShadowOnlyShader/Packages/com.hidano.shadow-only-shader/Editor/ShadowOnlyManagerEditor.cs`
- `ShadowOnlyShader/Packages/com.hidano.shadow-only-shader/README.md`
- 影計算本体（位置ずれ調査用）: `Runtime/Shaders/ShadowOnlyFloorCommon.hlsl`, `Runtime/VirtualLight.cs`, `Runtime/ShadowOnlyManager.cs`
