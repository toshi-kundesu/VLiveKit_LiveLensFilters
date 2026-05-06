# VLive Lens Filters

ライブ映像向けの post-process / lens filter 表現をまとめた Unity package です。

## Package

- Package name: `com.toshi.vlivekit.lensfilters`
- Version: `0.0.5`
- Unity: 2022.3
- Repository: https://github.com/toshi-kundesu/VLiveKit_LiveLensFilters
- Package root: `Assets/toshi.VLiveKit/LiveLensFilters`

## 主な内容

- Diffusion や character bloom などの映像フィルター
- HDRP の color / depth / normal / stencil を使う補助 utility
- ライブカメラの見た目を調整するための shader / material

## 依存・同梱 asset

- Unity Visual Compositor 0.30.7-preview
- Kino: https://github.com/keijiro/Kino

## インストール

Unity の `Packages/manifest.json` の `dependencies` に追加します。

```json
{
  "dependencies": {
    "com.toshi.vlivekit.lensfilters": "https://github.com/toshi-kundesu/VLiveKit_LiveLensFilters.git?path=/Assets/toshi.VLiveKit/LiveLensFilters#main"
  }
}
```

VLiveKit sandbox では submodule として `Packages/VLiveKit_LiveLensFilters` に配置し、`file:` 参照で読み込んでいます。

## 注意

- Kino 由来の asset には元 repository の license が適用されます。

## License

この package 独自のコードと asset は repository の `LICENSE` に従います。third-party asset を含む場合は、それぞれの license / README を確認してください。
