# API Notes

EnvForge talks to the backend through JSON over HTTP, plus an optional WebSocket notification channel for training status updates.

The actual endpoint URLs are configured in Unity with an `ApiSettings` ScriptableObject. The local `ApiSettings.asset` should not be committed while the backend is publicly reachable without abuse protection.

## Unity Configuration

Create an API settings asset in Unity:

```text
Create > EnvForge > API Settings
```

Configure these fields:

- `Base Url`: HTTP API base URL, without a required trailing slash.
- `WebSocket Url Template`: WebSocket URL containing `{submission_id}`.
- `Model Download Directory Name`: local folder name below `Application.persistentDataPath`.

Example URL template shape:

```text
wss://example.com/ws/results/{submission_id}
```

## Submit

```http
POST /submissions
Content-Type: application/json
Accept: application/json
```

Request body:

```json
{
  "environment": {
    "size": [4, 4],
    "obstacles": [
      { "x": 1, "y": 1 }
    ],
    "goal": { "x": 3, "y": 3 },
    "robot_start": { "x": 0, "y": 0 }
  },
  "robot": {
    "type": "simple"
  },
  "training": {
    "algorithm": "ppo",
    "timesteps": 5000,
    "seed": 10,
    "max_steps": 50,
    "n_steps": 32,
    "batch_size": 32,
    "gamma": 0.99,
    "learning_rate": 0.0003,
    "ent_coef": 0.0,
    "eval_episodes": 20
  }
}
```

Success response:

```json
{
  "status": "accepted",
  "submission_id": "submission-123"
}
```

## Start Training

```http
POST /submissions/{submission_id}/train
Accept: application/json
```

Success response:

```json
{
  "status": "accepted",
  "submission_id": "submission-123"
}
```

Failure response:

```json
{
  "detail": "Submission not found"
}
```

or:

```json
{
  "detail": "Failed to start trainer job"
}
```

## Get Result

```http
GET /results/{submission_id}
Accept: application/json
```

Result states:

- `queued`
- `starting`
- `running`
- `completed`
- `failed`

Example completed response:

```json
{
  "submission_id": "submission-123",
  "status": "completed",
  "progress": {
    "phase": "completed",
    "current_step": 5000,
    "total_steps": 5000,
    "message": "Training completed"
  },
  "summary": {
    "policy": "ppo",
    "score": 0.95,
    "grid_width": 4,
    "grid_height": 4,
    "episodes": 20,
    "obstacle_count": 1,
    "goal": { "x": 3, "y": 3 },
    "robot_start": { "x": 0, "y": 0 },
    "robot_type": "simple",
    "success_rate": 1.0,
    "avg_reward": 0.95,
    "avg_steps": 6.1,
    "training_timesteps": 5000,
    "training_seed": 10
  },
  "error": null,
  "artifacts": {
    "model": {
      "storage": "gcs",
      "bucket": "my-model-bucket",
      "path": "models/submission-123/policy.zip"
    }
  },
  "updated_at": "2026-04-24T12:34:56.000000+00:00"
}
```

When `GET /results/{submission_id}` returns JSON, Unity logs the raw JSON as a normal log. If the JSON cannot be retrieved or parsed, Unity treats it as an error.

## WebSocket Notifications

After training starts successfully, Unity opens the configured WebSocket URL if `WebSocket Url Template` is set.

The server should send JSON messages with the same shape as `GET /results/{submission_id}`. Unity handles these messages the same way as HTTP result responses.

Expected flow:

1. User submits a map and training settings.
2. User starts training.
3. Unity connects to the WebSocket URL for the returned `submission_id`.
4. Server sends `queued`, `starting`, `running`, `completed`, or `failed` result JSON.
5. On `completed`, Unity downloads the model artifact.

If the WebSocket URL is missing or the connection fails, Unity falls back to HTTP polling when auto polling is enabled.

## Model Artifacts

Current Unity behavior supports direct download from:

- Public HTTP or HTTPS artifact paths.
- Public Google Cloud Storage objects represented as:

```json
{
  "storage": "gcs",
  "bucket": "my-model-bucket",
  "path": "models/submission-123/policy.zip"
}
```

For GCS, Unity constructs:

```text
https://storage.googleapis.com/{bucket}/{path}
```

This requires the object to be publicly readable. If the bucket/object is private, the backend should return a signed download URL or expose a server-side download endpoint.

Downloaded files are saved below:

```text
Application.persistentDataPath/{Model Download Directory Name}/
```

For example, an artifact path of:

```text
models/submission-123/policy.zip
```

is saved as:

```text
Application.persistentDataPath/Models/models/submission-123/policy.zip
```

## Public Repository Safety

Do not commit local API settings while the backend has no authentication, quota, rate limit, or job abuse protection.

The repository `.gitignore` should keep these local files out of Git:

```text
unity/EnvForge/Assets/Settings/ApiSettings.asset
unity/EnvForge/Assets/Settings/ApiSettings.asset.meta
```

Endpoint URLs are not secrets by themselves, but publishing live unauthenticated endpoints can invite unwanted training jobs and backend cost. Never commit API keys, bearer tokens, signed URLs, or private endpoints.
