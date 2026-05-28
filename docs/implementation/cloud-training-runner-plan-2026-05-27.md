# クラウド学習Runner方針メモ 2026-05-27

## 現在の扱い

この文書は、Unity Linux build と ML-Agents trainer を
クラウド上で実行する案の検討記録である。

その後の方針変更により、EnvForge と EmbodiedLab の接続は Unity build
を直接クラウドへ投入する方式ではなく、 Scenario Bundle、Result Bundle、Replay
Log などの 明示的なデータ契約を通す方向に移った。

したがって、この文書は現在の主ロードマップではなく、 Unity/ML-Agents
をクラウド実行する必要が再浮上した場合の 参考記録として扱う。

## 背景

EnvForgeでは、Unity上のナビゲーション実験基盤をクラウド上で実行し、ML-Agentsによ
る学習結果を回収する仕組みを検討している。

ユーザがクラウドへ投入するものは、OpenGL付きのLinux
build済みUnityプロジェクトと、ML-Agents関連の設定・Pythonコード・実験設定である
。サービス側はそれらを受け取り、Google Cloud Compute Engine上のGPU
VMで実行し、学習済みモデル、checkpoint、ログ、エラー情報などをGoogle Cloud
Storageへ保存する。

現時点では、AWSとGoogle
Cloudを簡単に切り替えることや、複数クラウドを抽象化することは短期・中期目標に含
めない。まずはGoogle
Cloudの確認済み構成に固定し、EnvForgeの学習ジョブが安定して完走し、結果を確実に
回収できることを優先する。

## 現時点の大方針

- Google Cloud Compute Engineに一旦固定する。 -
  AWS/GCP抽象化は短期・中期目標に含めない。 - Unity描画がMesa
  llvmpipeに落ちる構成は採用しない。 - Unity Linux buildはhost VM上で実行する。
  - Python trainerはcontainer化を第一候補にする。 -
  実行中のログ、checkpoint、TensorBoard出力はlocal diskに書く。 -
  終了時に成果物をGoogle Cloud Storageへまとめてアップロードする。 - Python
  trainerのcontainer化は無条件採用ではなく、Host
  TrainerとのA/Bベンチで性能劣化を確認してから採用する。

## 採用候補構成

### Host Unity + Container Trainer

    GCE L4 vWS host
      NVIDIA vWS driver
      Xorg / virtual display
      Unity Linux build
      runner supervisor
      local working directory

    Docker container
      Python 3.10.12
      mlagents==1.1.0
      torch~=2.2.1
      training code
      ML-Agents YAML
      experiment configs
      mounted result directory

実行時は、Python trainerをcontainer内で先に起動し、ML-Agents
communicatorの待ち受けを作ってから、host VM上のUnity Linux buildを起動する。

    Upload job package
      ↓
    Prepare local working directory
      ↓
    Start trainer container
      ↓
    Start Unity build on host
      ↓
    Write logs/checkpoints/results to local disk
      ↓
    Collect artifacts
      ↓
    Upload artifacts to GCS
      ↓
    Return job status and result location

## この構成を好む理由

この構成では、Unity Linux buildをhost VM上で実行し、Python
trainerをcontainer内で実行する。理由は、EnvForgeの現在のクラウド学習では、Unity
側のOpenGL描画をNVIDIA rendererに確実に載せることが最重要条件だからである。

Colab T4ではtrainer PythonはCUDAを使えた一方、Unity描画はMesa
llvmpipeになった。GCE L4 vWSではUnity processがNVIDIA OpenGL
rendererを使い、`nvidia-smi`上でもgraphics
processとして確認できた。このため、Unity実行環境はcontainerに閉じ込めるより、NVI
DIA vWS driver、Xorg、DISPLAY、OpenGL設定をhost VM側で固定する方が安全である。

一方で、Python
trainer側は依存関係の再現性が重要である。現在のPython環境は`mlagents==1.1.0`、Py
thon
`>=3.10.1,<=3.10.12`、`torch~=2.2.1`、`onnx==1.15.0`の組み合わせに依存している。
この部分をcontainer化すれば、VM
imageにPython依存を深く焼き込まずに済み、trainer環境の更新や再現がしやすくなる。

また、短期・中期ではAWSとGoogle
Cloudの抽象化を目標にしない。したがって、まずGoogle Cloud Compute
Engineの確認済み構成に固定し、GPU
driverとUnity描画経路の安定性を優先する。クラウド間の切り替えや汎用runner設計よ
りも、EnvForgeの学習ジョブが確実に完走し、結果を回収できることを優先する。

ただし、ML-AgentsではUnityとtrainerが頻繁に通信するため、host processとcontainer
processの分離が性能に影響する可能性がある。そのため、この構成は無条件採用ではな
く、Host Unity + Host
TrainerとのA/Bベンチで性能劣化が許容範囲か確認してから採用する。

## 主要リスク

- ML-Agents通信が律速の場合、container分離で性能低下する可能性がある。 - Docker
  bridge networkを使うと、通信遅延やNAT由来のオーバーヘッドが出る可能性がある。
  - `--network host`を使う場合、port衝突やcontainer
  isolation低下に注意が必要である。 - Unity OpenGL/NVIDIA rendererはhost
  VMのdriver、Xorg、DISPLAY設定に強く依存する。 -
  Unityをcontainer内へ入れる構成は、OpenGL/Vulkan graphics
  capabilityやX11連携の失敗点が増えるため、現時点では優先しない。 - GCS
  FUSEや逐次アップロードを実行中に使うと、I/Oが律速になる可能性がある。 - VM
  image、NVIDIA vWS driver、Unity build、Python container
  imageのバージョン対応を記録しないと、後から再現しづらくなる。 -
  長時間ジョブでは、VM停止、quota、disk容量、途中失敗時のpartial
  result回収が問題になり得る。

## 性能検証計画

### 比較対象

    A. Host Unity + Host Trainer
    B. Host Unity + Container Trainer, Docker bridge network
    C. Host Unity + Container Trainer, host network

### 記録する指標

- 100000 trainer steps完了までのwall-clock時間 - trainer steps/sec - Unity
  CPU使用率 - trainer CPU使用率 - GPU utilization - GPU memory usage - Unity
  log上のrenderer - `nvidia-smi`上のUnity process種別 - trainer log - Unity log
  - resource usage log

### 採用判断基準

    CがA比で95%以上:
      trainer container化を採用する。

    CがA比で85%以上95%未満:
      再現性・運用性のメリットと性能低下を比較して判断する。
      長時間学習でも再確認する。

    CがA比で85%未満:
      trainer container化は保留する。
      Host Trainer構成、通信設定、観測生成コスト、環境並列化を再検討する。

    Bだけ遅くCが速い:
      host network前提でtrainer container化を採用候補にする。

## 成功条件

- Unity logでrendererがNVIDIA系になっている。 - Unity
  processが`nvidia-smi`にgraphics processとして表示される。 - Mesa
  llvmpipe経路になっていない。 - trainerを先に起動し、Unity buildがML-Agents
  communicatorへ接続できる。 -
  `--envforge-train`相当の起動指定により、Unityが学習接続モードで動く。 - 100000
  trainer stepsが完走する。 - ONNX、checkpoint、TensorBoard logs、trainer
  log、Unity log、resource usage log、run manifest snapshotがlocal
  disk上に揃う。 - ジョブ終了時に成果物をGCSへまとめて保存できる。 -
  エラー時にも、原因調査に必要なログとerror reportをGCSへ保存できる。

## ジョブパッケージ案

    job-package/
      manifest.json
      unity-build/
        EnvForge.x86_64
        EnvForge_Data/
      configs/
        ml-agents/
          navigation-final.yaml
        experiments/
          *.json
      python/
        envforge_mlagents/
        pyproject.toml
        uv.lock
      scripts/
        train.sh
        evaluate.sh

`manifest.json`には、少なくとも以下を含める。

- job id - Unity executable path - Unity version - required graphics mode -
  BehaviorName - ML-Agents Unity package version - Python ML-Agents version -
  Python version - trainer entrypoint - ML-Agents config path - experiment
  config path - seed - expected output paths - result GCS prefix

## 成果物案

    results/
      manifest.snapshot.json
      status.json
      model/
        *.onnx
      checkpoints/
      tensorboard/
      logs/
        trainer.log
        unity.log
        resource-usage.log
      errors/
        error-report.json

エラー時にも、`status.json`、`trainer.log`、`unity.log`、`resource-usage.log`、`
error-report.json`は可能な限り保存する。

## 保留事項

- GCE Machine Imageにどこまで焼き込むか。 - runner supervisorの責務範囲。 - job
  manifestの正式スキーマ。 - container imageのbuild、tag、registry運用。 - host
  networkを使う場合のport割り当て。 - 同一VM上で複数ジョブを並列実行するか。 -
  長時間ジョブ中断時のcheckpoint保存と再開方針。 -
  GCS上のprefix設計とアクセス権限。 - 成果物の保持期間。 -
  ユーザ提供コードを実行する場合のsandboxingとsecret保護。 -
  将来的にUnityもcontainer化するかどうか。
