# Shadow Only Shader

Unity URP 向けの「影だけ」を描画するシェーダーパッケージです。
透明な床面にキャラクターの影のみを表示できるため、VTuber 配信やキャラクター映像制作などで活用できます。

## 動作環境

- Unity 6.3 LTS 以降
- Universal Render Pipeline (URP) 17.0 以降

## インストール

Unity Package Manager からインストールできます。

1. **Window > Package Manager** を開く
2. 左上の **+** ボタン → **Add package from git URL...** を選択
3. 以下の URL を入力:

```
https://github.com/Hidano-Dev/ShadowOnlyShader.git?path=ShadowOnlyShader/Packages/com.hidano.shadow-only-shader
```

> 特定バージョンに固定する場合は末尾にタグを付けてください（例: `...com.hidano.shadow-only-shader#v1.0.0`）。

## セットアップ

### 1. Renderer Feature を登録する

使用中の **URP Renderer Asset** を選択し、Inspector で **Add Renderer Feature** → **Shadow Only Renderer Feature** を追加してください。

### 2. Manager を配置する

シーンに空の GameObject を作成し、`ShadowOnlyManager` コンポーネントを追加します。

### 3. 床面を登録する

影を受け取る透明な床メッシュの Renderer を Manager の **Floor Renderers** に追加します。床面のマテリアルは自動で割り当てられます（Manager の Inspector にある **床面を自動生成** ボタンでも作成できます）。自動割り当てされたマテリアルはシーン保存時に元のマテリアルへ自動復元されるため、シーンファイルは変更されません。

> 仮想光源を配置する前に床面を先に登録しておくと、次のステップで光源を動かしたときに影が床へ落ちる様子を確認しながら調整できます。

### 4. 仮想光源を追加する

Manager の Inspector から仮想光源 (VirtualLight) を追加します。子 GameObject として生成されるので、位置・回転を調整して影の方向を決めてください。

> 影は **Edit モード中もプレビュー表示** されます。Play せずに光源の位置・回転を調整しながら配灯結果を確認できます（`Source Light` 同期のみ Play 中に反映されます）。

### 5. キャスターを設定する

影を落としたいオブジェクト (キャラクターモデルなど) のルート GameObject を Manager の **Default Caster Root** に指定します。その配下のすべての Renderer (MeshRenderer・SkinnedMeshRenderer) が、全仮想光源の影の投影元になります。

特定の光源だけ別のオブジェクトの影を落としたい場合は、その VirtualLight の **Caster Root** に個別指定してください（未設定の光源は Manager の Default Caster Root を使用します）。

## 主なパラメータ

### Manager (全体設定)

| パラメータ | 説明 |
|-----------|------|
| Default Caster Root | 全光源共通の、影を落とすオブジェクトのルート |
| Blur Quality | ぼかし品質 (Low / Mid / High) |
| Blend Multiplier | 影の合成強度 |
| Blur Resolution Scale | ブラー計算の解像度スケール (0.1〜1.0)。低い値ほど軽量だが影がぼやける |
| Floor Renderers | 影を表示する床面のリスト |

### VirtualLight (光源ごとの設定)

| パラメータ | 説明 |
|-----------|------|
| Projection Mode | 投影方式 (Orthographic / Perspective / Point)。Point は FOV 90°×6 面で全方向に影を投影（深度テクスチャを 6 スライス使用） |
| Fit To Casters | Orthographic 時に投影範囲をキャスター全体へ毎フレーム自動フィット。ライトの角度によらずキャスターが投影範囲に収まり、テクセル密度も高く保たれる（有効時 Orthographic Size は不使用） |
| Shadow Color | 影の色 |
| Shadow Alpha | 影の不透明度 (0.0〜1.0) |
| Blur Radius | ぼかしの強さ |
| Blur Distance Factor | 光源からの距離に応じたぼかしの変化量 |
| Blur Camera Distance Factor | カメラからの距離に応じたぼかしの増加量 |
| Alpha Camera Distance Factor | カメラからの距離に応じた影の減衰量 |
| Camera Distance Power | カメラ距離効果のカーブ指数 (1=線形) |
| Contact Hardening Strength | PCSS コンタクトハードニングの強度 (0 で無効) |
| Contact Darkening Strength | 接地ダークニングの強度。キャスターに近い影 (足元など) を濃くして設置感を強調 (0 で無効) |
| Contact Darkening Range | 接地ダークニングの効果範囲。小さいほど接地部分だけが濃くなる |
| Hue Shift | 色相の回転 (0〜360°) |
| Chromatic Aberration | 色収差の強さ |
| Source Light | Unity Light との同期（Transform・投影パラメータ・影の濃さ・色収差フリンジ色を自動連動） |
| Chromatic Aberration Color | 色収差の光源色（Source Light 未設定時のフォールバック） |
| Texture Resolution | 深度テクスチャの解像度 (URP Default / 64〜8192) |
| Caster Root | この光源だけ影の元を変えたい場合の個別指定（未設定なら Manager の Default Caster Root を使用） |

## 特徴

- **非破壊レンダリング** — 対象オブジェクトの Layer やマテリアルを一切変更しません
- **複数光源対応** — 仮想光源を最大 32 スライス分（通常光源 1 個 = 1 スライス、Point 光源 1 個 = 6 スライス）配置して独立した影を重ね合わせ可能
- **ポイントライト対応** — Point モード（90°×6 面のキューブ投影）で全方向に影を投影。Unity の Point Light との自動同期にも対応
- **接地ダークニング** — キャスターと受影面の距離に応じて影の濃さを変化させ、AO のような設置感を影パイプライン内で表現
- **SkinnedMeshRenderer 対応** — アニメーション中のキャラクターの影もリアルタイムに描画
- **低解像度 Resolve** — ブラー等の重い計算を低解像度 RT で事前実行し、GPU 負荷を大幅削減
- **Source Light 同期** — Unity Light コンポーネントと連動し、位置・投影・影の濃さを自動同期
- **Gizmo 表示** — Scene ビューで光源の投影範囲を視覚的に確認できます
- **診断機能** — 影が表示されない原因を自動検出し、Inspector に一覧表示します

## トラブルシューティング（診断機能）

影が上手く表示されない場合は、ShadowOnlyManager の Inspector 上部にある **「診断」** セクションを確認してください。以下のような原因を自動検出し、エラー・警告・情報の 3 段階で表示します。

- Renderer Feature の未登録・無効化（Quality 設定側 URP アセットの差し替え漏れを含む）
- 仮想光源・床面 Renderer の未設定や非アクティブ、上限（32 スライス）超過
- 床面が光源の投影範囲（フラスタム）の外にある
- `Blend Multiplier` が 0 で影が透明になっている
- 共有深度テクスチャの解像度過大（GPU メモリ警告。設定の出どころと修正手順を表示）など

VirtualLight 個別の問題（キャスタールート未設定（共通・個別とも）・キャスターが投影範囲外・`Shadow Alpha` が 0 など）は、**各 VirtualLight の Inspector 上部** の「診断」セクションに表示されます。Manager 側には「どの光源に問題が何件あるか」の集約結果のみが表示されます。

診断は自動で更新されますが、「再診断」ボタンで即時に再実行できます。

### 深度テクスチャ解像度が大きすぎると警告される場合

深度テクスチャは全光源共有で、解像度は **最初のアクティブな VirtualLight** の設定で決まります。

- VirtualLight の **Texture Resolution** が「URP Default」の場合 → 使用中の URP アセット（Project Settings > Quality で設定されているもの）の **Main Light Shadow Resolution** がそのまま使われます。URP 側の設定を下げるか、VirtualLight 側で明示的に低い解像度（例: 1024 / 2048）を指定してください。
- Texture Resolution を明示指定している場合 → その VirtualLight の Inspector で値を下げてください。

解像度 × 光源数分の GPU メモリを消費するため（例: 8192×8192 × 8 ライトで約 2GB）、確保に失敗すると影が表示されなくなります。v0.6.0 以降は確保に失敗した場合、解像度を自動で半減して描画を継続します（Console に警告が出ます）。

## アーキテクチャ

```
ShadowOnlyRendererFeature
├── ShadowOnlyRenderPass       深度テクスチャ生成 (Texture2DArray)
├── ShadowOnlyShadowResolvePass  低解像度RTで影を事前計算 (BlurResolutionScale < 1.0 時)
└── Floor Shader                フロアRendererに影を描画
    ├── Floor (フル計算)         BlurResolutionScale = 1.0 時に使用
    └── FloorDisplay (軽量)     Resolve結果をサンプリングするだけ
```

## サンプルシーン

Package Manager の Samples タブから **Basic Setup** をインポートすると、設定済みのサンプルシーンを試すことができます。

## ランタイム API

スクリプトから動的に制御することもできます。

```csharp
var manager = GetComponent<ShadowOnlyManager>();

// 全光源共通のキャスターを設定
manager.DefaultCasterRoot = myCharacter;

// 仮想光源を追加
var light = manager.AddVirtualLight();
light.ProjectionMode = ProjectionMode.Orthographic;
light.ShadowAlpha = 0.6f;

// この光源だけ別のオブジェクトの影を落とす（個別上書き）
// light.CasterRoot = otherObject;

// 床面を追加
manager.AddFloorRenderer(floorRenderer);

// 低解像度Resolveで軽量化
manager.BlurResolutionScale = 0.5f;
```

## ライセンス

このプロジェクトのライセンスについてはリポジトリのライセンスファイルを参照してください。
