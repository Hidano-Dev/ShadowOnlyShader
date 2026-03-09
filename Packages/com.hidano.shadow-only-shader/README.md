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

影を落としたいオブジェクト (キャラクターモデルなど) のルート GameObject を VirtualLight の **Caster Root** に指定します。その配下のすべての Renderer が影の投影元になります。

### 5. 床面を登録する

影を受け取る透明な床メッシュの Renderer を Manager の **Floor Renderers** に追加します。床面のマテリアルは実行時に自動で割り当てられます。

## 主なパラメータ

### Manager (全体設定)

| パラメータ | 説明 |
|-----------|------|
| Blur Quality | ぼかし品質 (Low / Mid / High) |
| Blend Multiplier | 影の合成強度 |
| Floor Renderers | 影を表示する床面のリスト |

### VirtualLight (光源ごとの設定)

| パラメータ | 説明 |
|-----------|------|
| Projection Mode | 投影方式 (Orthographic / Perspective) |
| Shadow Color | 影の色 |
| Shadow Alpha | 影の不透明度 (0.0〜1.0) |
| Blur Radius | ぼかしの強さ |
| Blur Distance Factor | 距離に応じたぼかしの変化量 |
| Hue Shift | 色相の回転 (0〜360°) |
| Chromatic Aberration | 色収差の強さ |
| Texture Resolution | 深度テクスチャの解像度 (64〜4096) |
| Caster Root | 影を落とすオブジェクトのルート |

## 特徴

- **非破壊レンダリング** — 対象オブジェクトの Layer やマテリアルを一切変更しません
- **複数光源対応** — 仮想光源を複数配置して独立した影を重ね合わせ可能
- **SkinnedMeshRenderer 対応** — アニメーション中のキャラクターの影もリアルタイムに描画
- **Gizmo 表示** — Scene ビューで光源の投影範囲を視覚的に確認できます

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
```

## ライセンス

このプロジェクトのライセンスについてはリポジトリのライセンスファイルを参照してください。
