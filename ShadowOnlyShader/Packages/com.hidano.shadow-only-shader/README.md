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
https://github.com/HidanoDev/ShadowOnlyShader.git
```

## セットアップ

### 1. Renderer Feature を登録する

使用中の **URP Renderer Asset** を選択し、Inspector で **Add Renderer Feature** → **Shadow Only Renderer Feature** を追加してください。

### 2. Manager を配置する

シーンに空の GameObject を作成し、`ShadowOnlyManager` コンポーネントを追加します。

### 3. 仮想光源を追加する

Manager の Inspector から仮想光源 (VirtualLight) を追加します。子 GameObject として生成されるので、位置・回転を調整して影の方向を決めてください。

### 4. キャスターを設定する

影を落としたいオブジェクト (キャラクターモデルなど) のルート GameObject を VirtualLight の **Caster Root** に指定します。その配下のすべての Renderer (MeshRenderer・SkinnedMeshRenderer) が影の投影元になります。

### 5. 床面を登録する

影を受け取る透明な床メッシュの Renderer を Manager の **Floor Renderers** に追加します。床面のマテリアルは実行時に自動で割り当てられます。

## 主なパラメータ

### Manager (全体設定)

| パラメータ | 説明 |
|-----------|------|
| Blur Quality | ぼかし品質 (Low / Mid / High) |
| Blend Multiplier | 影の合成強度 |
| Blur Resolution Scale | ブラー計算の解像度スケール (0.1〜1.0)。低い値ほど軽量だが影がぼやける |
| Floor Renderers | 影を表示する床面のリスト |

### VirtualLight (光源ごとの設定)

| パラメータ | 説明 |
|-----------|------|
| Projection Mode | 投影方式 (Orthographic / Perspective) |
| Shadow Color | 影の色 |
| Shadow Alpha | 影の不透明度 (0.0〜1.0) |
| Blur Radius | ぼかしの強さ |
| Blur Distance Factor | 光源からの距離に応じたぼかしの変化量 |
| Blur Camera Distance Factor | カメラからの距離に応じたぼかしの増加量 |
| Alpha Camera Distance Factor | カメラからの距離に応じた影の減衰量 |
| Camera Distance Power | カメラ距離効果のカーブ指数 (1=線形) |
| Contact Hardening Strength | PCSS コンタクトハードニングの強度 (0 で無効) |
| Hue Shift | 色相の回転 (0〜360°) |
| Chromatic Aberration | 色収差の強さ |
| Source Light | Unity Light との同期（Transform・投影パラメータ・影の濃さ・色収差フリンジ色を自動連動） |
| Chromatic Aberration Color | 色収差の光源色（Source Light 未設定時のフォールバック） |
| Texture Resolution | 深度テクスチャの解像度 (URP Default / 64〜8192) |
| Caster Root | 影を落とすオブジェクトのルート |

## 特徴

- **非破壊レンダリング** — 対象オブジェクトの Layer やマテリアルを一切変更しません
- **複数光源対応** — 仮想光源を最大 8 つ配置して独立した影を重ね合わせ可能
- **SkinnedMeshRenderer 対応** — アニメーション中のキャラクターの影もリアルタイムに描画
- **低解像度 Resolve** — ブラー等の重い計算を低解像度 RT で事前実行し、GPU 負荷を大幅削減
- **Source Light 同期** — Unity Light コンポーネントと連動し、位置・投影・影の濃さを自動同期
- **Gizmo 表示** — Scene ビューで光源の投影範囲を視覚的に確認できます

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

// 仮想光源を追加
var light = manager.AddVirtualLight();
light.ProjectionMode = ProjectionMode.Orthographic;
light.CasterRoot = myCharacter;
light.ShadowAlpha = 0.6f;

// 床面を追加
manager.AddFloorRenderer(floorRenderer);

// 低解像度Resolveで軽量化
manager.BlurResolutionScale = 0.5f;
```

## ライセンス

このプロジェクトのライセンスについてはリポジトリのライセンスファイルを参照してください。
