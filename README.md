## 概要

VLiveKitの一部として開発している、  
ポストプロセスおよびレンズフィルター系のパッケージです。

ライブ映像に近い見た目を再現するためのフィルターや、  
HDRP環境でのポストプロセス制御を補助する機能をまとめています。

---

## 主な機能

### ポストプロセス

- Diffusion（原神ライクな表現）
- キャラクターブルーム
- 各種カスタムポストプロセス

---

### レンズフィルター（予定）

- ライブ映像で使用されるレンズ表現の再現
- 映像演出向けフィルターの追加

---

### ユーティリティ

HDRPでのポストプロセス実装を補助するための機能：

- BaseColorの取得
- Depthの取得
- Normalの取得
- Stencilの取得

シェーダーやポストプロセスでのデータ参照を簡略化します。

---

## 含まれるライブラリ

本パッケージには以下のライブラリが含まれています：

- Kino  
  https://github.com/keijiro/Kino  
  License: Unlicense

※ 上記ライブラリには個別のライセンスが適用されます。

---

## 開発状況

本パッケージはライブ制作での使用を前提に、  
継続的に調整・改善を行っています。

---

## インストール

`Packages/manifest.json` の `dependencies` に以下を追加してください。

```json
{
  "dependencies": {
    "com.toshi.vlivekit.lensfilters": "https://github.com/toshi-kundesu/VLiveKit_LiveLensFilters.git?path=/Assets/toshi.VLiveKit/LiveLensFilters#main"
  }
}
```

---

## Rain On Lens

`Rain On Lens` is available from `Post-processing/toshi/LensFilters/Rain On Lens`.

It uses the VLiveKit `CreativeFx` water-droplet mode to add animated rain beads, falling trails, refraction, and highlights as an HDRP custom post-process. Add it to an HDRP Volume, then tune:

- `Intensity`: overall blend amount.
- `Rain Amount`: droplet density.
- `Droplet Size`: bead size.
- `Refraction`: screen-space distortion through droplets.
- `Highlight`: wet sparkle strength.
- `Fall Speed`: downward animation speed.
- `Tint`: subtle color and alpha for the water layer.

The original `sixwaytest/Assets/RaindropShader` reference is a Heartfelt-derived shader marked `CC BY-NC-SA 3.0`, so VLiveKit keeps this package on its own CreativeFx implementation instead of copying that source shader into the public package.
