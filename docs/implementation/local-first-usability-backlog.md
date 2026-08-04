# Local-first active backlog

## 目的

この文書には未完了のユーザー課題だけを残す。完了済み項目と実装経緯は Git 履歴と
pull request を参照する。

SDK 公開 API と EnvForge 移行は
`docs/implementation/embodiedlab-unity-sdk-migration.md` を正本とする。

## Critical

### UX-031 / PHYS-002: Run AI episode lifecycle

- Replay と Run AI を排他的にする。
- Run AI 開始時は replay の途中状態を引き継がず、学習時と同じ規則で start pose を選ぶ。
- goal 到達と wall collision を episode terminal event として扱い、同じ model で次の start へ進む。
- 完了確認は、連続5回以上で開始位置が変わり、goal / collision 後に再開し、壁に接触したまま
  滑り続けないことを Unity 上で目視する。

## High

### ARCH-001: SDK 公開 API への再移行

- `EmbodiedLab.Unity` の新しい server-owned lifecycle と artifact / Replay API に追従する。
- EnvForge の旧 SDK 呼び出しと重複 code を削除する。
- Windows、Ubuntu、macOS target を個別に検証する。

### UX-013 / UX-018 / UX-029 / UX-035 / UX-037 / UX-038: Library

- map、job、Result、Replay、model を名前付きで一覧、選択、更新、再利用、削除できるようにする。
- job settings の再実行、検索、絞り込み、stale 状態を整理する。
- map と job の管理場所をスマホ UI の中で一貫させる。
- cloud artifact の破壊的削除は通常の local history 削除と分離し、保持台帳を必ず照合する。

### UX-015 / UX-016 / UX-027: Mobile interaction

- keyboard、right click、mouse wheel に依存しない主要操作を用意する。
- スマホ幅での panel、touch target、情報量、pan / zoom、wall rotation を整理する。
- 暫定 `Rot` button と角度 slider / input の役割を目視確認して確定する。

### UX-032: Wall connection

- robot と goal の保護を維持したまま、wall endpoint / side を隙間なく接続できる snap を設計する。
- 90度以外の微調整を維持する。

### UX-034: Replay layout

- compact / details で主要操作の順序、幅、button size を揃える。
- World details 表示中も Replay UI を表示する。
- 文字切れと touch 操作性を Unity で目視確認する。

### UX-039: Text input isolation

- 全 text / numeric input を共通 input blocker へ登録する。
- 入力中の矢印、Backspace、Delete、Enter、文字入力を world / camera / shortcut へ渡さない。
- Settings、Library、World の全入力欄で再発しないことを目視確認する。

### UX-005 / UX-009: Dense wall selection and map reset

- 密集した wall でも狙った wall を安定して選択できる操作を設計する。
- 現在の map を安全に破棄し、平面、境界、wall、start、goal、主要設定を既定値へ戻す。
- reset 前の確認と、保存済み map / 投入済み job への影響範囲を明示する。

### UX-040: Goal randomization

- 学習と Run AI で、必要に応じて goal position を試行ごとに変更できるようにする。
- wall、境界、start と安全距離を避ける sampling rule を Scenario 契約の明示項目にする。
- EmbodiedLab、EmbodiedLab.Unity、EnvForge が同じ seed と規則を使用し、UI と Replay で
  goal source と現在位置を確認できるようにする。

## Medium

### UX-002: Wall duplication

- 既存 wall を複製し、位置、長さ、角度だけを調整できるようにする。
- スマホを主対象として、keyboard shortcut に依存しない導線を選ぶ。

### UX-007: Result update flicker

- WebSocket による状態更新時のちらつきを再現し、更新頻度、再描画範囲、panel state、
  job history 保存を切り分ける。
- 接続 lifecycle の問題と表示更新の問題を分けて修正する。

### VIS-002: Walls in angle view

- angle view でも wall の高さ、厚み、奥行き、端点を自然な実寸で表示する。
- camera、field of view、mesh scale、material / outline、selection overlay を切り分けて確認する。

### UX-022: Panel consistency

- compact / details の同じ操作を、同じ順序、label、近い size に揃える。
- Cloud、World、Replay を横断して最終確認する。

### UX-033 / UX-017: User status and diagnostics

- 通常 UI にはユーザー向け状態だけを表示する。
- stream / fetch 診断は Editor-only overlay に分離する。
- error がない通常実行では overlay を出さず、Library の Fetch は HTTP fetch だけを行う。

## 運用上の制約

- cloud resource を削除する前に `cloud-result-retention.md` と JSON 台帳を確認する。
- human visual confirmation が完了条件の項目を、自動 test だけで Done にしない。
- 新しい UI 項目を追加する前に、既存 panel の情報量とスマホでの操作価値を確認する。
