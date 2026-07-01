using System;
using System.Collections.Generic;
using System.IO;
using EnvForge.Navigation.Contracts;
using UnityEngine;

namespace EnvForge.Navigation.Cloud
{
    public sealed class EnvForgeJobHistoryStore
    {
        private const string DirectoryName = "EnvForge";
        private const string FileName = "job-history.json";

        private readonly string historyPath;
        private JobHistoryDocument document;

        public EnvForgeJobHistoryStore(string persistentDataPath)
        {
            string root = string.IsNullOrWhiteSpace(persistentDataPath)
                ? DirectoryName
                : Path.Combine(persistentDataPath, DirectoryName);
            historyPath = Path.Combine(root, FileName);
        }

        public string HistoryPath => historyPath;

        public IReadOnlyList<EnvForgeJobRecordDto> Jobs => EnsureLoaded().jobs;

        public EnvForgeJobRecordDto Latest => Jobs.Count > 0 ? Jobs[0] : null;

        public EnvForgeJobRecordDto UpsertSubmittedJob(
            string submissionId,
            ScenarioBundleDto scenario,
            string trainerSummary)
        {
            if (string.IsNullOrWhiteSpace(submissionId))
            {
                return null;
            }

            EnvForgeJobRecordDto record = GetOrCreate(submissionId);
            record.submission_id = submissionId;
            record.scenario_id = scenario?.scenario_id;
            record.submitted_at_utc = string.IsNullOrWhiteSpace(record.submitted_at_utc)
                ? DateTime.UtcNow.ToString("o")
                : record.submitted_at_utc;
            record.status = "submitted";
            record.trainer_summary = trainerSummary;
            record.training_timesteps = scenario?.training?.timesteps ?? 0;
            record.training_seed = scenario?.training?.seed ?? 0;
            record.scenario_bundle_json = scenario == null
                ? string.Empty
                : ScenarioBundleSerializer.ToJson(scenario, prettyPrint: false);
            Save();
            return record;
        }

        public EnvForgeJobRecordDto UpsertResult(string submissionId, ResultDocumentDto result)
        {
            if (string.IsNullOrWhiteSpace(submissionId) || result == null)
            {
                return null;
            }

            EnvForgeJobRecordDto record = GetOrCreate(submissionId);
            record.submission_id = submissionId;
            record.status = result.status;
            record.progress_phase = result.progress?.phase;
            record.progress_current_step = result.progress?.current_step ?? 0;
            record.progress_total_steps = result.progress?.total_steps ?? 0;

            ResultBundleDto resultBundle = result.result_bundle;
            if (resultBundle != null)
            {
                record.scenario_id = string.IsNullOrWhiteSpace(resultBundle.scenario_id)
                    ? record.scenario_id
                    : resultBundle.scenario_id;
                ApplyTrainingSummary(record, resultBundle.summary);
            }

            ResultArtifactsDto artifacts = result.artifacts ?? resultBundle?.artifacts;
            ApplyArtifactMetadata(record, artifacts);
            Save();
            return record;
        }

        public EnvForgeJobRecordDto SetLocalReplayBundlePaths(string submissionId, string manifestPath, string replayPath)
        {
            EnvForgeJobRecordDto record = GetOrCreate(submissionId);
            record.local_replay_manifest_path = manifestPath;
            record.local_replay_chunk_path = replayPath;
            Save();
            return record;
        }

        public EnvForgeJobRecordDto SetLocalReplayManifestPath(string submissionId, string manifestPath)
        {
            EnvForgeJobRecordDto record = GetOrCreate(submissionId);
            record.local_replay_manifest_path = manifestPath;
            Save();
            return record;
        }

        public EnvForgeJobRecordDto SetLocalOnnxPath(string submissionId, string localPath)
        {
            EnvForgeJobRecordDto record = GetOrCreate(submissionId);
            record.local_onnx_path = localPath;
            Save();
            return record;
        }

        public void Save()
        {
            JobHistoryDocument loaded = EnsureLoaded();
            string directory = Path.GetDirectoryName(historyPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(historyPath, JsonUtility.ToJson(loaded, prettyPrint: true));
        }

        private JobHistoryDocument EnsureLoaded()
        {
            if (document != null)
            {
                return document;
            }

            if (!File.Exists(historyPath))
            {
                document = new JobHistoryDocument();
                return document;
            }

            string json = File.ReadAllText(historyPath);
            document = JsonUtility.FromJson<JobHistoryDocument>(json) ?? new JobHistoryDocument();
            if (document.jobs == null)
            {
                document.jobs = new List<EnvForgeJobRecordDto>();
            }

            return document;
        }

        private EnvForgeJobRecordDto GetOrCreate(string submissionId)
        {
            JobHistoryDocument loaded = EnsureLoaded();
            EnvForgeJobRecordDto existing = loaded.jobs.Find(job => job.submission_id == submissionId);
            if (existing != null)
            {
                loaded.jobs.Remove(existing);
                loaded.jobs.Insert(0, existing);
                return existing;
            }

            EnvForgeJobRecordDto record = new() { submission_id = submissionId };
            loaded.jobs.Insert(0, record);
            return record;
        }

        private static void ApplyTrainingSummary(EnvForgeJobRecordDto record, TrainingSummaryDto summary)
        {
            if (summary == null)
            {
                return;
            }

            record.training_timesteps = summary.training_timesteps;
            record.training_seed = summary.training_seed;
        }

        private static void ApplyArtifactMetadata(EnvForgeJobRecordDto record, ResultArtifactsDto artifacts)
        {
            if (artifacts == null)
            {
                return;
            }

            record.replay_artifact_path = artifacts.replay_bundle?.path;
            record.onnx_artifact_path = artifacts.onnx_model?.path;
        }

        [Serializable]
        private sealed class JobHistoryDocument
        {
            public List<EnvForgeJobRecordDto> jobs = new();
        }
    }

    [Serializable]
    public sealed class EnvForgeJobRecordDto
    {
        public string submission_id;
        public string scenario_id;
        public string submitted_at_utc;
        public string status;
        public string trainer_summary;
        public int training_timesteps;
        public int training_seed;
        public string progress_phase;
        public int progress_current_step;
        public int progress_total_steps;
        public string replay_artifact_path;
        public string onnx_artifact_path;
        public string local_replay_manifest_path;
        public string local_replay_chunk_path;
        public string local_onnx_path;
        public string scenario_bundle_json;
    }
}
