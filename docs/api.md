# API Notes

EnvForge talks to EmbodiedLab through JSON over HTTP. The current contract path is
Scenario Bundle in, Result Bundle and Replay Bundle out.

The actual endpoint URLs are configured in Unity with an `ApiSettings`
ScriptableObject. Local API settings assets should not be committed while the
backend is publicly reachable without abuse protection.

## Unity Configuration

Create an API settings asset in Unity:

```text
Create > EnvForge > API Settings
```

Configure these fields:

- `Base Url`: HTTP API base URL, without a required trailing slash.
- `WebSocket Url Template`: WebSocket URL containing `{submission_id}`.
  This is explicit because the API service and notification service may be
  deployed as separate Cloud Run services.
- `Model Download Directory Name`: local folder name below
  `Application.persistentDataPath`.

Example URL template shape:

```text
wss://example.com/ws/results/{submission_id}
```

## Submit Scenario

```http
POST /submissions
Content-Type: application/json
Accept: application/json
```

The request body is a Scenario Bundle. Its current source of truth is the
EmbodiedLab Pydantic `ScenarioBundle` model.

Minimal shape:

```json
{
  "schema_version": "scenario-bundle.v0",
  "scenario_id": "navigation_default",
  "created_by": {
    "tool": "EnvForge",
    "version": "0.1.0"
  },
  "compatibility": {
    "envforge_min_version": "0.1.0",
    "robot_version": "simple_robot.v0",
    "sensor_version": "basic_sensors.v0"
  },
  "world": {
    "coordinate_system": "envforge_xz_meters",
    "bounds": {
      "min": { "x": -5.0, "z": -5.0 },
      "max": { "x": 5.0, "z": 5.0 }
    },
    "static_walls": [],
    "static_obstacles": [],
    "goal": {
      "id": "goal_001",
      "position": { "x": 4.0, "z": 4.0 },
      "radius": 0.5
    }
  },
  "robot": {
    "type": "simple_robot",
    "start_pose": {
      "position": { "x": -4.0, "z": -4.0 },
      "rotation_y_degrees": 0.0
    },
    "action_space": {
      "type": "continuous",
      "layout": ["forward", "turn"]
    }
  },
  "sensors": [
    {
      "id": "front_camera",
      "type": "forward_camera",
      "width": 112,
      "height": 84,
      "semantic_mode": "traversable_vs_blocked"
    },
    {
      "id": "front_distance",
      "type": "distance_sensor",
      "range_meters": 5.0,
      "direction": "forward"
    }
  ],
  "reward": {
    "components": [
      {
        "name": "goal_reached",
        "type": "terminal_reward",
        "weight": 100.0
      },
      {
        "name": "goal_progress",
        "type": "distance_delta",
        "target": "goal_001",
        "weight": 0.1
      },
      {
        "name": "collision_penalty",
        "type": "collision",
        "weight": -50.0
      },
      {
        "name": "step_penalty",
        "type": "per_step",
        "weight": -0.01
      },
      {
        "name": "wide_angle_penalty",
        "type": "per_step",
        "weight": -0.1
      },
      {
        "name": "rear_angle_penalty",
        "type": "per_step",
        "weight": -5.0
      },
      {
        "name": "inactive_penalty",
        "type": "per_step",
        "weight": -0.1
      },
      {
        "name": "movement_threshold",
        "type": "per_step",
        "weight": 0.001
      },
      {
        "name": "turn_activity_threshold",
        "type": "per_step",
        "weight": 0.3
      }
    ]
  },
  "training": {
    "algorithm": "ppo",
    "timesteps": 5000,
    "seed": 10,
    "max_episode_steps": 512,
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

EmbodiedLab stores the EnvForge-facing result under `result_bundle`. Unity should
prefer `result_bundle` when it is present and treat the legacy top-level
`summary`, `artifacts`, and `error` fields as transport/status context.

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
  "result_bundle": {
    "schema_version": "result-bundle.v0",
    "scenario_id": "navigation_default",
    "job_id": "submission-123",
    "status": "completed",
    "compatibility": {
      "scenario_schema_version": "scenario-bundle.v0",
      "envforge_min_version": "0.1.0",
      "robot_version": "simple_robot.v0",
      "sensor_version": "basic_sensors.v0",
      "action_layout": ["forward", "turn"],
      "observation_layout": ["obs_0:image_chw_3x84x112", "obs_1:angle_distance"]
    },
    "summary": {
      "training_timesteps": 5000,
      "training_seed": 10,
      "success_rate": 0.82,
      "average_episode_reward": 6.4,
      "average_episode_steps": 118.5
    },
    "artifacts": {
      "model": {
        "storage": "gcs",
        "bucket": "embodiedlab-models",
        "path": "results/submission-123/model/policy.onnx",
        "format": "onnx"
      },
      "replay_bundle": {
        "storage": "gcs",
        "bucket": "embodiedlab-models",
        "path": "results/submission-123/replay/manifest.json",
        "format": "json",
        "schema_version": "replay-bundle.v0"
      }
    },
    "error": null
  },
  "updated_at": "2026-05-28T12:34:56.000000+00:00"
}
```

## Replay Bundle

Replay Bundle is a manifest artifact referenced by
`result_bundle.artifacts.replay_bundle`. The manifest lists gzip-compressed
JSON Lines chunks for training and evaluation trajectories.

Manifest shape:

```json
{
  "schema_version": "replay-bundle.v0",
  "job_id": "submission-123",
  "scenario_id": "navigation_default",
  "total_timesteps": 1500000,
  "chunks": [
    {
      "phase": "train",
      "policy_mode": "stochastic",
      "checkpoint_step": 10000,
      "start_step": 1,
      "end_step": 10000,
      "path": "train/chunk_000000.jsonl.gz",
      "format": "jsonl.gz",
      "step_count": 10000
    },
    {
      "phase": "eval",
      "policy_mode": "deterministic",
      "checkpoint_step": 1500000,
      "path": "eval/checkpoint_01500000.jsonl.gz",
      "format": "jsonl.gz",
      "step_count": 20000,
      "episode_count": 20,
      "success_rate": 0.95,
      "avg_reward": 82.4,
      "avg_steps": 118.5
    }
  ]
}
```

Each JSONL row inside a chunk is one `replay-log.v0` step.

Example line:

```json
{
  "schema_version": "replay-log.v0",
  "scenario_id": "navigation_default",
  "job_id": "submission-123",
  "phase": "eval",
  "checkpoint_step": 1500000,
  "env_index": 0,
  "policy_mode": "deterministic",
  "episode_id": "eval_env_00_episode_000001",
  "step_index": 1,
  "time_seconds": 0.1,
  "robot": {
    "position": { "x": -3.98, "z": -4.0 },
    "rotation_y_degrees": 0.0
  },
  "action": {
    "values": [
      {
        "name": "forward",
        "value": 0.2
      },
      {
        "name": "turn",
        "value": 0.0
      }
    ]
  },
  "reward": {
    "total": 0.04,
    "components": [
      {
        "name": "goal_progress",
        "value": 0.05
      },
      {
        "name": "step_penalty",
        "value": -0.01
      }
    ]
  },
  "events": [],
  "sensors": [
    {
      "id": "front_distance",
      "type": "distance_meters",
      "value": 5.0
    }
  ],
  "terminated": false,
  "termination_reason": null
}
```

EnvForge should replay these structured chunks locally rather than depending on
video output. Replay step v0 intentionally uses arrays of named values instead
of arbitrary JSON objects for action values, reward components, and sensor
summaries so Unity can parse it with `JsonUtility`.

## WebSocket Notifications

After training starts successfully, Unity opens the configured WebSocket URL if
`WebSocket Url Template` is set.

The server should send JSON messages with the same shape as `GET
/results/{submission_id}`. Unity handles these messages the same way as HTTP
result responses.

Expected flow:

1. User submits a Scenario Bundle.
2. User starts training.
3. Unity connects to the WebSocket URL for the returned `submission_id`.
4. Server sends `queued`, `starting`, `running`, `completed`, or `failed` result
   JSON.
5. On `completed`, Unity downloads the model and Replay Bundle artifacts when
   available.

If the WebSocket URL is missing or the connection fails, Unity reports the result
stream failure and does not provide a manual refresh path. Unity does not infer
the WebSocket URL from the HTTP API base URL.

## Artifact Download

Unity supports direct download from:

- Public HTTP or HTTPS artifact paths.
- Public Google Cloud Storage objects represented as:

```json
{
  "storage": "gcs",
  "bucket": "embodiedlab-models",
  "path": "results/submission-123/model/policy.onnx",
  "format": "onnx"
}
```

For GCS, Unity constructs:

```text
https://storage.googleapis.com/{bucket}/{path}
```

This requires the object to be publicly readable. If the bucket/object is
private, the backend should return a signed download URL or expose a server-side
download endpoint.

Downloaded files are saved below:

```text
Application.persistentDataPath/{Model Download Directory Name}/
```

For example, an artifact path of:

```text
results/submission-123/model/policy.onnx
```

is saved as:

```text
Application.persistentDataPath/Models/results/submission-123/model/policy.onnx
```

## Public Repository Safety

Do not commit local API settings while the backend has no authentication, quota,
rate limit, or job abuse protection.

The repository `.gitignore` should keep these local files out of Git:

```text
unity/EnvForge-local-first/Assets/Settings/ApiSettings.asset
unity/EnvForge-local-first/Assets/Settings/ApiSettings.asset.meta
```

Endpoint URLs are not secrets by themselves, but publishing live unauthenticated
endpoints can invite unwanted training jobs and backend cost. Never commit API
keys, bearer tokens, signed URLs, or private endpoints.
