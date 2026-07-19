# EmbodiedLab Unity SDK 移行記録

## 状態

`EmbodiedLab.Unity` の導入、EnvForge の呼び出し元移行、共有 ONNX Runtime binary の
package 所有への集約は実装済みである。
Unity 6000.3.11f1 で package resolve と batchmode compile を確認済みである。
submit、monitor、cancel、download、replay、履歴復元の実サービスを使う Play mode
確認は手動統合確認として残る。

## 目的

EnvForge 内の汎用的な EmbodiedLab client 機能を、独立した
`EmbodiedLab.Unity` UPM package へ移す。EnvForge は SDK の最初の利用者となり、
EnvForge 固有の編集、表示、Replay、推論機能に集中する。

## SDK へ移す責務

- Scenario / Result / Replay Bundle DTO と serializer
- submission 作成、training 開始、result fetch
- WebSocket result stream と HTTP 再同期
- artifact location の解決と download
- Replay Bundle manifest / chunk の取得、展開、parse
- API error、timeout、local cancellation、cloud job cancellation
- API endpoint と WebSocket endpoint の SDK 設定
- ONNX Runtime managed assembly と Windows x64 native binary の配布・versioning

現在の主な移行元は `Assets/Scripts/Navigation/Cloud` と
`Assets/Scripts/Navigation/Contracts` である。ただし、ディレクトリ単位で機械的に
移動せず、次の EnvForge 固有責務を切り分けて残す。

## EnvForge に残す責務

- Unity scene と world editor
- scene から Scenario Bundle を構築する処理
- Cloud panel、Job details、Library などの UI
- ユーザ向け job history と EnvForge 固有のローカル状態
- Replay player と scene 上の可視化
- ONNX Runtime 推論と操作 UI
- EnvForge 固有の保存先、表示文言、画面遷移

Replay Bundle の取得と parse は SDK に移すが、robot、wall、goal を scene 上に表示し、
再生位置やカメラを操作する処理は EnvForge に残す。

## 設計・移行原則

- 現在必要な最小限の公開 API だけを設計する。
- 将来の可能性だけを理由に抽象化、extension point、汎用 layer を追加しない。
- 重複実装、不要な互換 layer、到達不能な旧コードを残さない。
- 既存動作を contract fixture と test で固定してから、責務を段階的に切り出す。
- 各段階で対象リポジトリの test と lint を実行し、成功を次段階の開始条件とする。
- 公開 API、contract、責務境界などの設計判断が必要な場合は、2 から 3 案の
  メリットとデメリット、推奨案と推奨理由を提示する。
- 設計判断はユーザとの合意前に実装しない。

## 実施した移行

1. EmbodiedLab の JSON Schema と fixture で契約を固定した。
2. SDK の `EmbodiedLabJob`、`ScenarioBundleJson`、`EmbodiedLabReplay` を導入した。
3. submit と training start を `EmbodiedLabJob.SubmitAsync` に集約した。
4. job 監視は WebSocket-first とし、明示的な再同期だけ `RefreshAsync` を使う。
5. `SubmissionId` と cancellation capability token を EnvForge の履歴に保存し、
   再起動後の監視、取得、キャンセルを可能にした。
6. Replay manifest、選択した chunk、学習済みモデルの取得と parse を SDK へ移した。
7. EnvForge 内の旧 DTO、HTTP client、WebSocket client、downloader、serializer を削除した。
8. review 後の hardening として、Replay の入力順保持、job 切替時の Replay 状態破棄、
   chunk 間の前後移動と chunk-local log step 表示、history refresh と削除の競合防止、
   terminal job の cancellation token 破棄を追加した。
9. EnvForge の接続設定では非 loopback の平文 HTTP / WebSocket と userinfo 付き URL を
   UI と command-line automation の両方で拒否し、履歴から削除できるローカル artifact を
   対象 job の replay と model に限定した。
10. Result Document の artifact metadata は `result_bundle.artifacts` だけを参照し、
    旧 top-level `artifacts` 用の adapter は SDK と EnvForge の両方から削除した。
11. SDK package を package-owned ONNX Runtime を含む merge commit へ更新し、EnvForge の
    `Assets/Plugins/ONNXRuntime` を削除した。EnvForge は navigation 固有の推論契約、
    observation 生成、motion、UI のみを所有する。

## 学習環境モードとの関係

EnvForge は Scenario Bundle で `fixed` または `generated` mode を選択できるようにする。
既存の固定マップは `fixed` の既定動作として維持する。`generated` は宣言的な生成規則を
設定する別の選択肢であり、4 分割した領域へのランダムな壁パーツ配置はその一例である。

SDK は mode と生成規則の DTO、serialization、compatibility check を担当する。
生成規則の検証と episode ごとの実行は EmbodiedLab が担当し、EnvForge や SDK が
任意コードをクラウドへ送って実行する機能は当面扱わない。

## 検証

- UPM package は package-owned ONNX Runtime を含む `EmbodiedLab.Unity` の merge commit
  `b96a46779bef8ed24af77d9aecf49f94150d8afa` を指定した Git URL で固定している。
- SDK 側では contract、transport、facade、scenario/replay API の test と lint が成功した。
- Unity 6000.3.11f1 の batchmode で package resolve と `Assembly-CSharp`、
  `Assembly-CSharp-Editor` の compile が成功した。
- SDK package の Unity Test Runner は 8 件成功した。
- EnvForge consumer boundary test 3 件で、SDK pin、package dependency、ローカル ONNX Runtime
  重複の不在、旧 adapter の不在、nested artifact 参照を検証する。EnvForge 利用側の
  EditMode runner は対象 test が 0 件であり、job 切替、
  履歴競合、Replay 順序の Unity behavior test は引き続き不足している。
- 2026-07-19 に Unity 6000.3.11f1 で package resolve と batchmode compile を再実行し、
  `b96a46779bef8ed24af77d9aecf49f94150d8afa` の package plugin だけが import されること、
  duplicate assembly / native plugin warning がないことを確認した。
- 同日に実際の download 済み `policy.onnx` を使い、Editor Play Mode と Windows x64
  Standalone の両方で camera observation、ONNX 実行、action 適用を 3 step 実行した。
  Standalone 出力には managed assembly、native runtime、shared provider が各 1 個だけ含まれる。
- EnvForge の replay fixture は生成済み契約の必須項目を満たす。
- 旧実装への参照が残っていないことと JSON / Markdown / C# syntax を静的確認する。
- Play mode での submit、monitor、cancel、download、replay、履歴復元は、実サービスへ
  接続できる環境で最終確認する。

## スコープ外・保留事項

- job history の汎用部分を将来 SDK へ移すかは、複数フロントエンドの要求が確認できるまで
  EnvForge 側に保留する。
- 認証、private artifact、signed URL は今回実装しない。具体的な backend contract と
  利用要件が決まった時点で設計し、将来用途だけの拡張点は先に追加しない。
- SDK 全体での WebSocket message、artifact download、gzip 展開後サイズの上限は未実装で
  ある。EnvForge 側だけでは他の SDK 利用者を保護できないため、SDK 側の別 PR で扱う。
- SDK の endpoint 自体が非 loopback の平文 HTTP / WebSocket を拒否する変更は、既存利用者
  への影響を確認して SDK 側の別 PR で扱う。EnvForge 利用時は今回追加した設定検証で拒否する。
- running job の cancellation capability token は再起動後の cancel に必要な間だけ
  `job-history.json` に平文保存し、terminal result 受信時に破棄する。現在は単一ユーザ端末と
  OS アカウント境界を前提とする。共有端末を対象にする場合は OS protected storage を設計する。
- `fixed` / `generated` の選択と宣言的な生成規則は、この移行とは別の設計・実装にする。
