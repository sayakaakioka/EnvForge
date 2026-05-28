# EnvForge / EmbodiedLab 契約実装 Phase 3

## 目的

Phase 3 では、EnvForge 側で生成した Scenario Bundle を EmbodiedLab に送る
導線を作り始める。

最初の目標は、クラウド送信 UI を作ることではなく、EnvForge で生成する JSON が
EmbodiedLab の Pydantic model で検証できることを確認することである。

## 今回の範囲

今回の最小範囲は以下である。

- EnvForge 側に canonical Scenario Bundle fixture を置く。
- EnvForge の固定ナビゲーションシナリオの default source を明示する。
- EmbodiedLab 側に同じ JSON fixture を置き、`ScenarioBundle` で検証する。

fixture は以下に置く。

    fixtures/scenario-bundles/navigation_default.json

この fixture は、現在の `NavigationSceneBuilder` が生成する固定ナビゲーション
シナリオを表す。座標系は `envforge_xz_meters`、床は中心原点であり、
`world.bounds` は `x = -8..8`、`z = -6..6` である。

## この順序を採用する理由

EnvForge から EmbodiedLab へ HTTP 送信する前に、契約の JSON 形状が一致している
ことを固定 fixture で確認する。これにより、Unity UI、認証、ネットワーク、
ジョブ起動の問題と、データ契約の問題を切り分けやすくする。

EmbodiedLab 側の Pydantic model は当面の source of truth である。
EnvForge 側の C# DTO はこの model に追従する境界であり、fixture はその追従確認の
ための最小サンプルである。

## 保留事項

- Unity Editor から fixture を自動生成する menu item または batchmode method。
- EnvForge の fixture と EmbodiedLab の copied fixture の同期方法。
- Scenario Bundle を POST する API client 境界。
- API URL や認証情報を Unity project asset に持たせる方法。
- 送信 UI とジョブ状態表示。
- Replay Log parser。これは `JsonUtility` 前提を崩さず、専用 parser として
  別タスクで設計する。
