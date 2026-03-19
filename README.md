# ThinkingTMP

> **結論: 可能。** TextMeshProのFontAssets（SDFフォントアトラス）を、Unity非依存の環境で.NET 10 CLIツールとして生成できることを実証しました。

TextMeshProのFontAssetsを作成するCLIを（Unity非依存で）作成できないか検討する

---

## 概要

Unity Editorを使わずに、TTF/OTFフォントファイルから TextMeshPro互換の SDF (Signed Distance Field) フォントアセットを生成する .NET 10 CLIツールの実装です。

生成されるファイルをそのまま Unity プロジェクトの `Assets/` フォルダに配置するだけで利用できます。

## 生成されるファイル

| ファイル | 説明 |
|---|---|
| `{フォント名} SDF Atlas.png` | グレースケール SDF アトラステクスチャ |
| `{フォント名} SDF Atlas.png.meta` | Unity TextureImporter メタファイル（SingleChannel/Linear設定済み） |
| `{フォント名} SDF.asset` | TMP_FontAsset Unity YAMLファイル（グリフテーブル・文字テーブル・フェイス情報含む） |
| `{フォント名} SDF.asset.meta` | Unity NativeFormatImporter メタファイル |

## SDF Atlas 生成例

以下は Liberation Sans Regular (90pt, 9px padding, 512×512 atlas) で生成したアトラスのプレビューです:

![SDF Atlas Preview](https://github.com/user-attachments/assets/f3e2f790-755e-4967-9e8c-3c6ee46b00d8)

明るいほどグリフ内部（SDF 値が高い）、暗いほどグリフ外部（SDF 値が低い）を表します。このソフトな境界情報を使って TMP がスケーラブルなテキスト描画を行います。

## アーキテクチャ

```
ThinkingTMP/
├── src/
│   ├── ThinkingTMP.Core/           # コアライブラリ
│   │   ├── Models/                 # データモデル (FaceInfo, GlyphData, etc.)
│   │   ├── GlyphRasterizer.cs      # フォント読み込み・グリフラスタライズ
│   │   ├── SdfGenerator.cs         # 8SSEDT アルゴリズムによる SDF 生成
│   │   ├── AtlasPacker.cs          # シェルフアルゴリズムによるグリフパッキング
│   │   ├── FontAssetGenerator.cs   # 処理オーケストレーター
│   │   └── UnityAssetWriter.cs     # Unity YAML ファイル出力
│   └── ThinkingTMP.Cli/            # CLI エントリポイント
└── tests/
    └── ThinkingTMP.Tests/          # ユニットテスト
```

### 使用ライブラリ

| ライブラリ | バージョン | 用途 |
|---|---|---|
| [SixLabors.Fonts](https://github.com/SixLabors/Fonts) | 2.1.3 | フォントファイル読み込み・グリフメトリクス取得 |
| [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) | 3.1.12 | 画像処理・PNG出力 |
| [SixLabors.ImageSharp.Drawing](https://github.com/SixLabors/ImageSharp.Drawing) | 2.1.7 | グリフのラスタライズ描画 |
| [System.CommandLine](https://github.com/dotnet/command-line-api) | 2.0.5 | CLI 引数パース |

## 使い方

### ビルド

```bash
dotnet build --configuration Release
```

### 実行

```bash
# 基本的な使い方（ASCII印字可能文字 U+0020–U+007E）
dotnet run --project src/ThinkingTMP.Cli -- \
  --font /path/to/MyFont.ttf \
  --output ./out

# オプション指定
dotnet run --project src/ThinkingTMP.Cli -- \
  --font /path/to/MyFont.ttf \
  --output ./out \
  --size 90 \
  --padding 9 \
  --atlas-width 1024 \
  --atlas-height 1024 \
  --oversample 4

# 名前付き文字セット指定
dotnet run --project src/ThinkingTMP.Cli -- \
  --font MyFont.ttf --output ./out \
  --charset "Basic Latin" "Latin-1 Supplement"

# コードポイント範囲直接指定
dotnet run --project src/ThinkingTMP.Cli -- \
  --font MyFont.ttf --output ./out \
  --codepoints 32-126 0x4E00-0x4E50
```

### オプション一覧

| オプション | デフォルト | 説明 |
|---|---|---|
| `--font` / `-f` | (必須) | TTF/OTF フォントファイルパス |
| `--output` / `-o` | `./out` | 出力ディレクトリ |
| `--size` | `90` | サンプリングポイントサイズ |
| `--padding` | `9` | アトラスピクセル単位の SDF パディング |
| `--atlas-width` | `512` | アトラステクスチャ幅（2の累乗推奨） |
| `--atlas-height` | `512` | アトラステクスチャ高さ（2の累乗推奨） |
| `--oversample` | `4` | SDF生成前のオーバーサンプリング倍率 |
| `--charset` | (なし) | 対象 Unicode ブロック名 |
| `--codepoints` | (なし) | コードポイント範囲（例: `32-126`） |

### 対応名前付き文字セット

- `ASCII Printable` (U+0020–U+007E) ← デフォルト
- `ASCII` (U+0000–U+007F)
- `Basic Latin` (U+0000–U+007F)
- `Latin-1 Supplement` (U+0080–U+00FF)
- `Latin Extended-A` (U+0100–U+017F)
- `Latin Extended-B` (U+0180–U+024F)
- `CJK Unified Ideographs` (U+4E00–U+9FFF)

## SDF 生成アルゴリズム

1. **グリフラスタライズ** — `oversample` 倍のフォントサイズで各グリフを描画
2. **EDT (Euclidean Distance Transform)** — 8SSEDT（8点逐次符号付きユークリッド距離変換）で符号付き距離場を生成
   - 前向きパス: NW/N/NE/W 方向に伝播
   - 後向きパス: SE/S/SW/E 方向に伝播
3. **ダウンサンプル** — ボックスフィルタで 1/oversample に縮小
4. **アトラスパッキング** — シェルフアルゴリズムでアトラスに配置
5. **出力** — Unity YAML 形式で `.asset` ファイルを生成

## テスト

```bash
dotnet test
```

## 技術的考察：Unity非依存での実現可能性

### 可能な理由

TMP FontAsset の本質は以下の3要素の組み合わせです：

1. **SDF テクスチャ** — フォントの輪郭情報をグレースケール距離場として保存（画像処理で生成可能）
2. **グリフメトリクス** — 各文字の位置・サイズ情報（フォントファイルから直接取得可能）
3. **Unity YAML シリアライズ形式** — テキストベースの設定ファイル（仕様が公知なので直接生成可能）

Unity Editor が行っていることは「フォントファイルを読んで上記を生成し YAML に書き出す」だけであり、Unity ランタイムへの依存は不要です。

### 制限事項

- Unity の自動インポートには `.png.meta` ファイルの正確な設定が必要
- TMP バージョンによってスクリプト GUID が異なる場合がある（現在は `com.unity.textmeshpro` 3.x/4.x 対応）
- カーニングペア（`m_FontFeatureTable`）は現在未実装
- カラーフォント（SVG/COLR）未対応
