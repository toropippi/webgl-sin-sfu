# WebGL Sin() Bitplane Probe
GPU上で実行するsin() cos()関数には近似版と高精度版があります。近似版はNVIDIA GPUで言うところの**SFU (Special Function Unit)**で実行されます。  
このプログラムはそれを確認するためのもので、ブラウザ上で `sin(x)` を実行し、GPU 上で得られた結果を **ビット単位で読み戻す**デモです。  

---

## 🔗 実行

GitHub Pages からアクセスしてブラウザで実行できます：

👉 **[Live Demo](https://toropippi.github.io/webgl-sin-sfu-test/)**

対応ブラウザ：Chrome / Edge / Firefox / Safari  
（WebGL 2.0 対応必須）

---

## 入力値

以下の 9 個をテストしています。

### ビットパターン (16 進)
- `0x00000100`
- `0x007FFFFF`
- `0x807FFFFF`
- `0x80000000`
- `0x00000000`

### 浮動小数 (10 進)
- `0.0001230000052601099`
- `3.1415979862213135`
- `-2.3399999141693115`
- `114514.0`

## 🖼 出力画面の見方

![output](docs/screenshot.png)

- **タイトル行**  
  `WebGL sin() bitplane probe (32x9, 1bit/px, ARGB32)`  
  - 32x9 のビットプレーンとして GPU → CPU にデータを戻しています。  
  - 1bit/px なので、どの環境でもビット完全一致が保証されます。

- **環境情報**  
  - Unity の `SystemInfo.graphicsDeviceType` や `graphicsDeviceVersion` などを表示。
  - 例：`API=OpenGLES3, Name=ANGLE (NVIDIA GeForce RTX 5090), Vendor="WebKit"`

- **テーブル列の意味**
  | 列名        | 説明 |
  |-------------|------|
  | **idx**     | 入力のインデックス番号（0〜8） |
  | **in_dec**  | 入力値（10進） |
  | **in_hex**  | 入力値の 32bit ビットパターン |
  | **sin_dec** | GLSL `sin(x)` の結果（10進） |
  | **sin_hex** | その結果を 32bit として再解釈したビット列 |
  | **match (vendor)** | 既知の参照テーブルと **ビット完全一致**したベンダー名（NVIDIA/AMD/Intel） |

- **Overall vendor guess**  
  9 個の入力のすべてで同じベンダーと一致した場合に、そのベンダー名をまとめて表示します。

---

## ⚙ 実装ポイント

- **1bit/px エンコード**  
  GLSL シェーダーで `sin(x)` の結果を `asuint()` し、各ビットを 32px 横一列に出力。  
  CPU 側はピクセルを走査して 32bit 値を復元するため、浮動小数の丸めや UNorm 誤差の影響を受けません。

- **参照テーブル**  
  主要3ベンダー（AMD gfx1036, Intel UHD770, NVIDIA RTX5090）の既知結果を格納。  
  ビット完全一致ならベンダー名を表示します。

- **シェーダのストリッピング回避**  
  `Shader.Find("Hidden/WebGLSinProbe")` が null になるのを防ぐため、  
  プロジェクトに明示的に参照されるよう **Resources フォルダまたはマテリアル**に追加しています。

---

## 📜 ライセンス

MIT ライセンスで公開。





















# WebGL Sin SFU Test

Unity で作成した WebGL ビルドを使い、GPU シェーダー内の `sin()` 関数がどのように評価されるかを確認するためのサンプルです。  
特に  による実行と精度挙動を観察することを目的としています。

---

## 概要

- Unity (任意のバージョン、WebGL ビルド対応)
- シェーダーで `sin()` を計算
- 入力は CPU 側で生成し、uniform 変数として GPU に渡す
- 出力は GPU 側で計算され、結果を 3×3 テクスチャに格納
- CPU 側に読み戻して GUI 上に表示（WebGL 上でブラウザから確認可能）

---

---

## 使い方

1. Unity でプロジェクトを開く
2. ビルドターゲットを **WebGL** に設定
3. `Build` フォルダを作成しビルド
4. 出力を GitHub Pages に配置すると、ブラウザから結果を閲覧できます

---

## 公開ページ

（GitHub Pages 公開後にここに URL を追記してください）

---

## ライセンス

MIT License
