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
