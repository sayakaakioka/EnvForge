# サブエージェント運用

## 目的

親エージェントが方針と統合を担当し、調査やレビューを短命の専門タスクへ
分担する。最終判断、ユーザへの確認、編集、commit、push、PR は親エージェントが
管理する。

## 開始前

親エージェントは次を行う。

1. Google Drive で正確な query `AGENTS.md` を使って検索し、title が
   `AGENTS.md` の最新ファイルを読む。
2. リポジトリの `AGENTS.md`、`docs/vision`、関連する
   `docs/implementation` を読む。
3. 変更範囲、触らない範囲、検証方法を確定する。
4. 同じファイルを複数の実装担当へ同時に割り当てない。

## 役割

### Context Scout

Phase の開始時に、関連する docs、Unity project、package、fixture を読み、
「現状」「変更候補」「リスク」「未確認事項」を返す。ファイルは変更しない。

### Implementation Worker

方針と編集範囲が確定している小さな実装を担当し、「変更ファイル」「実装内容」
「検証結果」「残件」を返す。他の担当者の変更を revert しない。

### Review Scout

実装後に、仕様、Unity scene / package、ONNX 入出力、Replay 契約、UI、test gap を
確認し、severity 順の finding と residual risk を返す。ファイルは変更しない。

### Security Scout

cloud、認証、upload / download、外部 process が関係する差分について、secret、
`.env`、capability token、GCS、path、破壊的操作を確認する。ファイルは変更しない。

### Refactor / Docs Worker

機能と検証が一段落した後に、重複、命名、責務、現在の docs との不整合を確認する。

## 禁止事項

- サブエージェントへ commit、push、PR 作成を任せない。
- `.env` を変更させない。
- reviewer に修正を任せない。
- 広すぎる探索を依頼しない。
- 親エージェントが結果を検証せずに統合しない。

## レビュー依頼に含める内容

- 今回の目的と確定済み方針
- 対象差分または対象ファイル
- 特に見る契約、platform、UI、security 境界
- 出力形式を severity 順の finding、test gap、residual risk に限定すること
- read-only であること
