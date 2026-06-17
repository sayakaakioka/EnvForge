# Codex から Unity batchmode を起動する手順

この文書は、Codex やサブエージェントから EnvForge の Unity Editor を batchmode
で起動するときのローカル運用手順を記録する。

## 背景

EnvForge の Unity project はリポジトリ内の以下に置かれている。

    unity\EnvForge-local-first

Unity Editor は Unity Hub 管理の Unity 6.3 LTS `6000.3.11f1` を使う。
Editor と Hub の実体 path はローカル環境ごとに異なるため、Makefile には
絶対 path を固定しない。`check_unity` を使う場合は、必要に応じて
`UNITY_HUB` と `UNITY_EDITOR` を環境変数または make 変数で指定する。

Codex から Editor を直接 batchmode 起動したところ、Unity Hub から手動で project
を開く場合は正常に動く一方で、次のような失敗が出た。

- `Unity.Licensing.Client.exe` が Windows dialog で例外を出す。 - Unity log に
  `Connection to channel LicenseClient-<Windowsユーザ名> refused` が出る。 -
  Editor 同梱の Licensing Client 起動後に assertion failure が出る。 - Unity
  Package Manager local server へ接続できず、batchmode が失敗する。

これは project や UPM package の破損ではなく、Codex から Editor を直叩き
した時点で Unity Hub 側のライセンス状態が十分に初期化されていないことが
主因と見なす。

## 採用する起動方針

当面は standalone の `unity` CLI を導入せず、Unity Hub headless CLI で
ライセンス状態を初期化してから、Unity Editor を直接 batchmode 起動する。

この方針を採用する理由は、現在必要なのが Codex / サブエージェントからの
ローカル検証であり、既にこの手順で `-batchmode -quit -nographics` が return code
`0` で完走することを確認できているためである。

standalone の `unity` CLI は、将来 CI や複数マシンでの自動化を整える段階で
再評価する。現時点では、追加導入による PATH、認証、Hub との関係の不確実性を
増やさないことを優先する。

## 標準手順

Unity batchmode は Makefile 経由で実行する。ただし、この Windows 環境では
Codex から Unity Editor を直接 batchmode 起動すると `Unity.Licensing.Client.exe`
の例外が再発するため、`check_unity` の既定動作は Unity Editor を起動しない
path 解決のみとする。

`check_unity` は、Unity Editor を次の順で探す。

1. `UNITY_HUB` / `UNITY_EDITOR` で明示された path または command。
2. PATH 上の command。
3. Unity Hub 管理下の Editor install path。

通常は以下でよい。この場合、Unity Editor は起動しない。

    make check_unity

見つからない場合や別 version を使う場合は、ローカル環境の path を make 変数で渡す。

    make check_unity \
      UNITY_HUB="C:\Program Files\Unity Hub\Unity Hub.exe" \
      UNITY_EDITOR="C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe"

`UNITY_PROJECT_PATH` は既定で `unity\EnvForge-local-first`、`UNITY_LOG_FILE` は
既定で `unity-batchmode.log` とする。どちらもリポジトリ相対 path であり、
必要なら make 変数で上書きする。

`UNITY_CHECK_TIMEOUT` は既定で 240 秒とする。Unity Editor process が timeout
しても、log に batchmode 成功が記録されている場合は成功扱いにする。

Unity Hub の headless 初期化は既定では実行しない。必要な場合だけ
`UNITY_INITIALIZE_HUB=1` を指定する。Unity Hub 側の挙動は環境差が大きく、
Codex から呼ぶと終了待ちが不安定になる場合があるためである。

Unity Editor を起動せずに path 解決だけを確認したい場合は、
`UNITY_RESOLVE_ONLY=1` を指定する。これは現在の既定動作と同じである。

    make check_unity UNITY_RESOLVE_ONLY=1

Unity Editor の batchmode 起動は、必要性を確認したうえで明示的に
`UNITY_RUN_BATCHMODE=1` を指定した場合だけ実行する。

    make check_unity UNITY_RUN_BATCHMODE=1

成功時は Unity log の末尾付近に以下が出る。

    Batchmode quit successfully invoked - shutting down!
    Exiting batchmode successfully now!
    Application will terminate with return code 0

## Windows Standalone ビルド

Codex から修正後の動作確認を閉じたループで行うため、Windows Standalone
ビルド用の入口を用意する。既定の出力先は以下である。

    artifacts\builds\windows\EnvForge.exe

Make が使える環境では以下を実行する。

    make build_unity_windows

この環境の PowerShell から直接実行する場合は以下を使う。

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\build-unity-windows.ps1

必要に応じて、Unity Hub 初期化、出力先、ログ、timeout は引数または Makefile
変数で上書きする。

    make build_unity_windows UNITY_INITIALIZE_HUB=1
    make build_unity_windows UNITY_BUILD_OUTPUT=artifacts\builds\windows\EnvForge.exe

ビルドは Unity Editor の batchmode で
`EnvForge.Editor.EnvForgeBuild.BuildWindows` を `-executeMethod` として呼び出す。
Build Settings で有効な scene を使い、Windows Standalone 64-bit を生成する。

2026-06-15 時点では、Codex から以下の流れを確認済みである。

- Unity Hub / Unity Editor の path 解決に成功した。
- `-batchmode -quit -nographics` の空起動が return code `0` で完走した。
- Windows Standalone ビルドが成功し、`artifacts\builds\windows\EnvForge.exe`
  が作成された。

## 失敗時の確認

同じ例外が再発した場合は、まず失敗した batch 起動が残した Editor 同梱の
Licensing Client が残っていないか確認する。

    Get-Process |
      Where-Object {
        $_.ProcessName -like '*Unity*' -or
        $_.ProcessName -like '*Licens*'
      } |
      Select-Object Id, ProcessName, Path

Hub 側の `Unity.Licensing.Client.exe` は、典型的には以下の path で動く。

    C:\Program Files\Unity Hub\UnityLicensingClient_V1\Unity.Licensing.Client.exe

Editor 同梱の Licensing Client は、典型的には以下の path で動く。

    C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Data\Resources\Licensing\Client\Unity.Licensing.Client.exe

失敗後に残りやすいのは Editor 同梱側である。Hub 側ではなく Editor 同梱側の
残骸だけを止めてから、再度 Hub headless CLI の初期化手順を実行する。

    Stop-Process -Id <Editor同梱LicensingClientのPID> -Force

`-accessToken` や `-hubSessionId` は Unity Hub の log に出る場合があるが、
これらを手作業で再利用しない。認証情報を command line やドキュメントへ
持ち出さず、Hub headless CLI による初期化を使う。

## 保留

- この手順を Makefile や helper script に包むかどうか。 - standalone `unity` CLI
  を導入するかどうか。 - CI 上で同じ方針が通用するかどうか。 - GUI の Unity Hub
  が起動していない状態でも安定するかどうか。
