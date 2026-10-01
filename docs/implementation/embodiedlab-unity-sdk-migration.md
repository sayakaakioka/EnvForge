# EmbodiedLab Unity SDK 移行

## 現在の状態

EnvForge は `com.embodiedlab.unity` の main commit
`51af750807626c15da6bf873f6d010f7a78e6fa1` を固定している。

server-owned job lifecycle、size / SHA-256 を含む artifact 検証、identity と chunk
metadata を照合する Replay 読み込みへ移行済みである。Scenario builder は camera、
action、reward、PPO、resource 値をすべて JSON に明記し、local inference は Scenario の
observation / action 値と固定された policy mapping を使う。

移行前の旧 API や重複実装を compatibility layer として残さない。過去の移行経緯は
Git 履歴と pull request を参照する。

## 責務境界

`EmbodiedLab.Unity` が持つ責務:

- Scenario / Result / Replay Bundle の生成 DTO と serialization
- job submit、状態監視、明示的な再同期、cancel
- artifact metadata、size、SHA-256 の検証と download
- Replay Bundle manifest / chunk の逐次読み込み
- Result 内の ONNX artifact metadata と size / SHA-256 の検証
- Unity 2022.3.19f1 以降で利用できる package 境界

EnvForge が持つ責務:

- world editor と Scenario Bundle 構築
- Cloud / World / Replay / Library UI
- ユーザー向け job と map の履歴
- Replay の scene 表示と操作
- navigation 固有の ONNX session 入出力、observation / action mapping、local inference
- EnvForge 固有の保存先と画面遷移

Cloud、Result、Replay の SDK workflow は Windows x64、Ubuntu x64、macOS を対象とする。
同梱 native ONNX Runtime による local inference は現時点で Windows x64 のみを対象とし、
Ubuntu と macOS は native integration を追加してから個別に検証する。

## 到達済み

1. EmbodiedLab で server-owned lifecycle と最終データ契約を確定した。
2. `EmbodiedLab.Unity` の artifact / Replay 検証と tutorial を確定した。
3. EnvForge の package pin と canonical fixture を確定済み SDK main へ同期した。
4. `Assets/Scripts/Navigation/Cloud` の restore、artifact、Replay 呼び出しを新 API へ置き換えた。
5. 旧 artifact field、旧 Replay property、意味の違う action 解釈を削除した。
6. Scenario の camera、action、reward、training 値を local runtime と inference へ接続した。

## 次の検証

1. Unity 6.3 LTS Editor と Windows x64 standalone の local inference を検証する。
2. Ubuntu x64 と macOS は cloud / result / replay の package / application build を
   対象環境で個別に検証する。
3. Cloud の実ジョブで submit、monitor、verified download、Replay、local inference を目視確認する。

## 完了条件

- EnvForge は一つの submit 操作で job handle を受け取り、server 内部の dispatch 操作を扱わない。
- queued / running / terminal Result を一つの monitor から受け取る。
- model と Replay artifact は size と SHA-256 を確認してから利用する。
- Replay chunk を全件結合せず、必要な chunk を逐次読み込みできる。
- Scenario JSON の寸法、camera、reward、training 値を runtime が正本として使う。
- `policy.onnx` の metadata と Scenario の observation / action contract が一致しない場合は
  明確に失敗する。
- 旧 SDK 呼び出し、重複 DTO、旧 transport、不要な compatibility adapter が残らない。
- package / EditMode / PlayMode test と対象 platform build が成功する。

## 移行後に別途扱う項目

- 認証、private artifact、signed URL
- shared device 向けの cancel capability 保護 storage
- generated environment mode
- 複数 robot、動的障害物、高忠実度 simulation


## 2026-09-30: ResultSnapshot 移行（当時は SDK 公開待ち）

この節は旧 pin 時点の履歴。公開待ち・旧 pin の compile 不可は下記 2026-10-01 の更新で解消した。

- Cloud panel の結果保持、ResultUpdated、RefreshAsync / CancelAsync の戻り値を
  ResultSnapshot に、進捗表示を ResultProgressSnapshot に移行した。
- 履歴保存と artifact metadata 参照の境界でのみ ToDocument() を使い、既存の
  履歴ファイル形式と EnvForgeJobHistoryStore の wire DTO 入力を維持した。
- Result fixture を ONNX-only / opset 18、Replay fixture の reset step を
  action / reward / event がゼロの現行契約に同期した。4 fixture とも現行 SDK と
  byte 単位で一致する。固定 SHA の manifest / lock は変更していない。
- SDK の未公開 job lifecycle 変更を含む正式 SHA がまだないため、本体の古い pin
  のままでは今回のソースはコンパイルできない。SDK 公開後に manifest、lock、
  tests/test_embodiedlab_unity_boundary.py の SDK_REVISION を同時更新する。
  架空 SHA、ユーザー固有パス、互換 adapter は追加しない。
- 一時プロジェクトに EnvForge の Assets / ProjectSettings とインストール済み
  package cache をコピーし、現行 SDK を embedded package として検証した。
  SDK の EmbodiedLabJob / ResultSnapshot と EnvForge の C# ソースが元 checkout と
  同一であることを hash で確認した。本体の package 設定と既存 Library は未変更。
- 検証: EnvForge Python tests 10 件、SDK transport behavior tests 41 件、現行
  契約検証（EnvForge fixture、Replay 2 step）、C# whitespace、git diff --check は成功。
  Python Ruff は既存環境にないため未実行（追加インストールなし）。
- Unity 6000.3.11f1 の一時環境では全体 compile とローカル移行確認 7 件が成功。
  履歴再読込、scenario / cancel capability 保持、job restore、事前キャンセル済み
  wait、terminal 保存時の capability 除去、artifact metadata、snapshot 独立性を確認。
  既存 Unity package の obsolete API 警告あり。認証変更・再試行ループなし。
- 未実行: 実 cloud submit / monitor / download、UI Play Mode、実 ONNX 推論、
  Standalone build。41 件の SDK tests はローカルの模擬 transport による検証であり、
  実 deployment の E2E 成功を意味しない。
- SDK の 2022.3 対応は維持し、SDK runtime / package 要件は今回変更していない。
  直前の SDK 最終コード検証は 2022.3.19f1 / 6000.3.11f1 で各 23/23 成功。
  EnvForge 自体の Unity 6.3 対象は変えない。commit / push / 公開は未実施。


## 2026-10-01: SDK PR #39 merge 後の正式 pin 検証

- 承認済み head `3a249e3ddc99bf9fd444e6969b6ed9cb9e454dbe`、15 ファイル、
  CI 成功・merge 可を確認し、SDK PR #39 を Ready 化して squash merge した。
  確定 commit は `51af750807626c15da6bf873f6d010f7a78e6fa1`。
  squash commit と検証済み head の Git tree が同一であることも確認した。
- EnvForge の manifest / lock / 境界テスト SDK_REVISION / fixture README を
  確定 SHA へ揃えた。README の公開待ち説明を除去した。旧 pin による compile
  不可は解消済み。架空 SHA、ローカル絶対パス、SDK embedded 参照は本体にない。
- 一時プロジェクトから旧 embedded SDK を退避し、本体と同じ Git URL / SHA を
  Unity Package Manager で実解決した。生成された SDK lock entry は本体と一致し、
  source は git、hash は上記確定 SHA。取得された SDK の 4 fixture と本体の
  fixture は byte 一致した。他の依存は既存インストール済み package のコピーを使用。
- Unity 6000.3.11f1 の正式 Git pin で全体 compile とローカル履歴検証 7 件が成功、
  終了コード 0。新しい成功 marker とログで判定した。認証変更・同一失敗再試行なし。
- 更新後 EnvForge Python tests 10 件、取得済み SDK fixture に対する契約検証
  （Replay 2 step）、JSON parse、git diff --check は成功。
- 機能ソースは前段検証と同一。SDK 2022.3 対応と EnvForge 6.3 対象は維持。
  実 cloud E2E、画面 Play Mode、実 ONNX 推論、Standalone は今回未実行。
  Ruff 未導入という前段の制約も継続。EnvForge commit / push / PR 投稿は未承認・未実施。
