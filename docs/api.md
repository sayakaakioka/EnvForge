# EmbodiedLab Integration

EnvForge uses the `com.embodiedlab.unity` package for the shared EmbodiedLab
contracts and cloud job lifecycle. EnvForge does not maintain a second HTTP,
WebSocket, artifact-download, or contract-serialization implementation.

## Package

The Unity project pins a tested `EmbodiedLab.Unity` merge commit in
`Packages/manifest.json`:

```json
{
  "com.embodiedlab.unity":
    "https://github.com/sayakaakioka/EmbodiedLab.Unity.git#<commit>"
}
```

Update the commit only after the SDK contract and transport checks pass and the
EnvForge project resolves and compiles with the new revision.

## Unity Configuration

Create an API settings asset in Unity:

```text
Create > EnvForge > API Settings
```

Configure these fields:

- `Base Url`: the HTTP API base URL.
- `Web Socket Base Url`: the notification service base URL, without a job path.

The HTTP API and notification service can use different hosts, so EnvForge does
not derive one endpoint from the other. The SDK appends the current result-stream
path and submission ID to the WebSocket base URL.

Do not commit bearer tokens, cancellation capability tokens, signed URLs, or
private endpoint assets.

## Runtime Flow

EnvForge builds an `EmbodiedLab.Contracts.ScenarioBundle` from the current scene
and passes it to the SDK:

```csharp
var endpoints = new EmbodiedLabEndpoints(apiBaseUrl, webSocketBaseUrl);
EmbodiedLabJob job = await EmbodiedLabJob.SubmitAsync(
    endpoints,
    scenario,
    cancellationToken);
```

`SubmitAsync` creates the submission and starts training as one operation. The
Cloud panel then uses `WaitForCompletionAsync`. Monitoring is WebSocket-first;
HTTP reads are used by explicit `RefreshAsync` calls and SDK recovery after a
failed, disconnected, or silent stream. EnvForge does not run periodic HTTP
polling.

The current result states are:

- `queued`
- `starting`
- `running`
- `cancelling`
- `cancelled`
- `completed`
- `failed`

Cloud cancellation is explicit:

```csharp
if (job.CanCancel)
{
    ResultDocument result = await job.CancelAsync(cancellationToken);
}
```

A C# `CancellationToken` only stops the local SDK operation. `CancelAsync`
requests cancellation of the remote training job.

## Persistence and Cancellation Capability

EnvForge stores the submission ID and cancellation capability token in its local
job history. It restores the SDK handle after a restart:

```csharp
EmbodiedLabJob job = EmbodiedLabJob.Restore(
    endpoints,
    savedSubmissionId,
    savedCancelToken);
```

The capability token must not be logged or included in user-facing errors.
Restoring a job without the token still supports monitoring and artifact
downloads, but `CanCancel` is false.

## Results and Artifacts

After a `completed` result, EnvForge uses the SDK to download:

- the Replay Bundle manifest;
- selected compressed replay chunks;
- the preferred trained model, normally `policy.onnx`.

The Replay Bundle remains lazy. EnvForge downloads and reads `manifest.json`,
selects a chunk, and downloads only that chunk:

```csharp
await job.DownloadReplayBundleAsync(manifestPath, cancellationToken);
ReplayBundleManifest manifest = EmbodiedLabReplay.ReadManifest(manifestPath);

await job.DownloadReplayChunkAsync(chunk, chunkPath, cancellationToken);
IReadOnlyList<ReplayLogStep> steps = EmbodiedLabReplay.ReadSteps(chunkPath);
```

Scenario JSON and bundled replay JSON Lines use `ScenarioBundleJson` and
`EmbodiedLabReplay.ParseSteps`. The generated contract types come from the
versioned JSON Schemas in EmbodiedLab; EnvForge-specific duplicate DTOs are not
kept.

Artifacts are saved below `Application.persistentDataPath`, not inside the
repository. EnvForge keeps local paths and presentation state in its own job
history because those are frontend concerns.

## Command-Line Submission

The unattended Unity submission flow accepts:

- `-envforgeSubmitJob <output-json-path>`
- `-envforgeApiBaseUrl <http-base-url>`
- `-envforgeWebSocketBaseUrl <websocket-base-url>`
- `-envforgeScenarioId <scenario-id>`
- `-envforgeTrainingPreset <smoke|mvp>`
- `-envforgeWorldVariant <variant>`

It uses the same `EmbodiedLabJob.SubmitAsync` path as the interactive Cloud
panel. The output includes the resolved API and WebSocket base URLs, submission
ID, scenario JSON, and scene summary.

## Current Boundary

EnvForge owns scene authoring, UI, local history, replay visualization, and local
inference. `EmbodiedLab.Unity` owns shared contracts, serialization, cloud job
transport, monitoring, cancellation, and artifact retrieval. The EmbodiedLab
backend remains the source of truth for server behavior and JSON Schemas.

Authentication and private artifact access are not implemented yet. They will be
designed only after a concrete backend contract and usage requirement exist.
