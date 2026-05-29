# Final Navigation Handoff

## 目的

MVP 0からMVP 3までの開発を一旦区切り、他人に渡しやすい最終版の入口を用意する。

これまでのMVPごとのコードは実験履歴として残しつつ、通常の利用者が最初に見るべき
コード、シーン上のBootstrap、学習設定、推論モデルを最終版の名前に整理する。

## 最終版の入口

Unity側の最終版コードは以下に集約する。

```text
unity/EnvForge-local-first/Assets/Scripts/Navigation/
```

最終版のBootstrapは`EnvForgeNavigationLab.unity`内の`NavigationBootstrap`である。

`NavigationBootstrap`には`NavigationSceneBuilder`を付け、現在の主経路では
ローカル手動操作、EmbodiedLabへのクラウド送信、Replay Logの取得と再生を扱う。
Unity側でML-Agents componentを生成して trainer へ接続する経路は、現行の
EnvForge / EmbodiedLab クラウド導線からは外している。

Windows StandaloneのGraphics APIはProjectSettingsでDirect3D
11へ固定する。Snapdragon / Adreno環境ではDirect3D
12実行時にRenderTexture作成失敗からUnity
Editorがクラッシュするケースがあったためである。

将来ローカル推論を戻す場合は、ML-Agents前提ではなく、EmbodiedLabが返したモデルを
EnvForge側で読み込む独立した推論導線として再設計する。

## 開発履歴の扱い

MVPごとの実装は削除せず、以下へ退避する。

```text
unity/EnvForge-local-first/Assets/Scripts/DevelopmentHistory/
```

内訳は以下である。

| フォルダ | 役割 |
| --- | --- |
| `Mvp0` | 手動操作と最小ナビゲーション環境 |
| `Mvp1` | 最小ML-Agents adapter |
| `Mvp2` | セグメンテーション画像観測 |
| `Mvp3` | custom CNN / strict trainer 導入前後の開発版 |

これらは比較・参照用であり、最終版を動かすための推奨入口ではない。

## 学習設定

最終版のBehaviorNameは`NavigationFinal`とする。

学習設定:

```text
configs/ml-agents/navigation-final.yaml
```

学習スクリプト:

```text
scripts/train-navigation-final.sh
```

strict trainerの実装は以下に配置する。

```text
python/envforge_mlagents/navigation_strict/
```

この実装はMVP 3
strict版で検証した構造を継承しているが、最終版利用者が迷わないように最終版名へリ
ネームした。`NavigationFinal`のBehaviorNameとconfigを渡せば、最終版として学習で
きる。

## 到達済み

- 最終版コードを`Assets/Scripts/Navigation/`へ集約した -
  開発履歴を`Assets/Scripts/DevelopmentHistory/`へ退避した -
  `EnvForgeNavigationLab.unity`の有効Bootstrapを`NavigationBootstrap`にした -
  `NavigationFinal.onnx`を最終版の既定推論モデルとして配置した -
  `NavigationFinal`用のML-Agents configとtrain scriptを追加した -
  READMEの冒頭に最終版の起動・推論・学習手順を追加した - strict
  trainer実装を`python/envforge_mlagents/navigation_strict/`へリネームした -
  Windows StandaloneのGraphics APIをDirect3D 11へ固定した - 左上のdebug
  overlayタイトルを最終版名に更新した - 推論モデル割り当て時はML-Agents
  communicatorを無効化し、trainer接続試行を抑止するようにした

## 保留

- Unity
  Editor上で再import後、`NavigationSceneBuilder`のMonoScript参照が正しく解決され
  ることを確認する -
  `NavigationFinal.onnx`が旧BehaviorName由来のexportでも、`NavigationFinal`のBeh
  aviorParameters上で問題なく推論できることを複数episodeで確認する
