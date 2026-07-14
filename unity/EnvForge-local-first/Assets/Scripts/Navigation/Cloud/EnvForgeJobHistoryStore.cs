using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using EmbodiedLab.Contracts;
using EmbodiedLab.Unity;
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

        public EnvForgeJobRecordDto MostRecentRecord
        {
            get
            {
                JobHistoryDocument loaded = EnsureLoaded();
                SortJobsBySubmittedAtDescending(loaded.jobs);
                return loaded.jobs.Count > 0 ? loaded.jobs[0] : null;
            }
        }

        public EnvForgeJobRecordDto FindJob(string submissionId)
        {
            if (string.IsNullOrWhiteSpace(submissionId))
            {
                return null;
            }

            return EnsureLoaded().jobs.Find(job => job.submission_id == submissionId);
        }

        public EnvForgeJobRecordDto UpsertSubmittedJob(
            string submissionId,
            string cancelToken,
            ScenarioBundle scenario,
            string trainerSummary,
            string settingsName)
        {
            if (string.IsNullOrWhiteSpace(submissionId))
            {
                return null;
            }

            EnvForgeJobRecordDto record = GetOrCreate(submissionId);
            record.submission_id = submissionId;
            record.cancel_token = string.IsNullOrWhiteSpace(cancelToken) ? null : cancelToken;
            record.scenario_id = scenario?.ScenarioId;
            record.settings_name = string.IsNullOrWhiteSpace(settingsName)
                ? string.Empty
                : settingsName.Trim();
            record.submitted_at_utc = string.IsNullOrWhiteSpace(record.submitted_at_utc)
                ? DateTime.UtcNow.ToString("o")
                : record.submitted_at_utc;
            record.status = "submitted";
            record.trainer_summary = trainerSummary;
            record.training_timesteps = scenario?.Training?.Timesteps ?? 0;
            record.training_seed = scenario?.Training?.Seed ?? 0;
            record.scenario_bundle_json = scenario == null
                ? string.Empty
                : ScenarioBundleJson.Serialize(scenario);
            if (string.IsNullOrWhiteSpace(record.display_name) || IsDefaultJobDisplayName(record.display_name))
            {
                record.display_name = BuildDefaultDisplayName(record);
            }
            Save();
            return record;
        }

        public EnvForgeJobRecordDto UpsertResult(string submissionId, ResultDocument result)
        {
            if (string.IsNullOrWhiteSpace(submissionId) || result == null)
            {
                return null;
            }

            EnvForgeJobRecordDto record = GetOrCreate(submissionId);
            record.submission_id = submissionId;
            record.status = FormatStatus(result.Status);
            record.result_updated_at = result.UpdatedAt;
            record.history_updated_at_utc = DateTime.UtcNow.ToString("o");
            record.progress_phase = result.Progress == null
                ? null
                : FormatStatus(result.Progress.Phase);
            record.progress_current_step = result.Progress?.CurrentStep ?? 0;
            record.progress_total_steps = result.Progress?.TotalSteps ?? 0;
            record.display_name = string.IsNullOrWhiteSpace(record.display_name)
                ? BuildDefaultDisplayName(record)
                : record.display_name;

            ResultBundle resultBundle = result.ResultBundle;
            if (resultBundle != null)
            {
                record.scenario_id = string.IsNullOrWhiteSpace(resultBundle.ScenarioId)
                    ? record.scenario_id
                    : resultBundle.ScenarioId;
                ApplyTrainingSummary(record, resultBundle.Summary);
            }

            ApplyArtifactMetadata(record, EnvForgeResultArtifacts.Resolve(result));
            if (IsTerminalStatus(result.Status))
            {
                record.cancel_token = null;
            }

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

        public EnvForgeJobRecordDto SetDisplayName(string submissionId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(submissionId))
            {
                return null;
            }

            EnvForgeJobRecordDto record = GetOrCreate(submissionId);
            record.display_name = string.IsNullOrWhiteSpace(displayName)
                ? BuildDefaultDisplayName(record)
                : displayName.Trim();
            record.history_updated_at_utc = DateTime.UtcNow.ToString("o");
            Save();
            return record;
        }

        public bool RemoveJob(string submissionId)
        {
            if (string.IsNullOrWhiteSpace(submissionId))
            {
                return false;
            }

            JobHistoryDocument loaded = EnsureLoaded();
            int removed = loaded.jobs.RemoveAll(job => string.Equals(job?.submission_id, submissionId, StringComparison.Ordinal));
            if (removed <= 0)
            {
                return false;
            }

            Save();
            return true;
        }

        public void Save()
        {
            JobHistoryDocument loaded = EnsureLoaded();
            SortJobsBySubmittedAtDescending(loaded.jobs);
            string directory = Path.GetDirectoryName(historyPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(loaded, prettyPrint: true);
            string temporaryPath = historyPath + ".tmp";
            File.WriteAllText(temporaryPath, json);
            if (File.Exists(historyPath))
            {
                File.Replace(temporaryPath, historyPath, null);
            }
            else
            {
                File.Move(temporaryPath, historyPath);
            }
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

            SortJobsBySubmittedAtDescending(document.jobs);
            return document;
        }

        private EnvForgeJobRecordDto GetOrCreate(string submissionId)
        {
            JobHistoryDocument loaded = EnsureLoaded();
            EnvForgeJobRecordDto existing = loaded.jobs.Find(job => job.submission_id == submissionId);
            if (existing != null)
            {
                return existing;
            }

            EnvForgeJobRecordDto record = new() { submission_id = submissionId };
            record.display_name = BuildDefaultDisplayName(record);
            loaded.jobs.Insert(0, record);
            return record;
        }

        private static void SortJobsBySubmittedAtDescending(List<EnvForgeJobRecordDto> jobs)
        {
            if (jobs == null || jobs.Count <= 1)
            {
                return;
            }

            jobs.Sort(CompareJobsBySubmittedAtDescending);
        }

        private static int CompareJobsBySubmittedAtDescending(EnvForgeJobRecordDto left, EnvForgeJobRecordDto right)
        {
            DateTime leftSubmitted = ParseUtcDateTimeOrMin(left?.submitted_at_utc);
            DateTime rightSubmitted = ParseUtcDateTimeOrMin(right?.submitted_at_utc);
            int dateComparison = rightSubmitted.CompareTo(leftSubmitted);
            if (dateComparison != 0)
            {
                return dateComparison;
            }

            return string.Compare(right?.submission_id, left?.submission_id, StringComparison.Ordinal);
        }

        private static DateTime ParseUtcDateTimeOrMin(string value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime parsed))
            {
                return parsed.ToUniversalTime();
            }

            return DateTime.MinValue;
        }

        private static string BuildDefaultDisplayName(EnvForgeJobRecordDto record)
        {
            if (!string.IsNullOrWhiteSpace(record?.settings_name))
            {
                return record.settings_name;
            }

            if (record == null || string.IsNullOrWhiteSpace(record.submission_id))
            {
                return "Job";
            }

            return $"Job {record.submission_id.Substring(0, Math.Min(8, record.submission_id.Length))}";
        }

        private static bool IsDefaultJobDisplayName(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName) ||
                string.Equals(displayName, "Job", StringComparison.Ordinal) ||
                displayName.StartsWith("Job ", StringComparison.Ordinal);
        }

        private static void ApplyTrainingSummary(EnvForgeJobRecordDto record, TrainingSummary summary)
        {
            if (summary == null)
            {
                return;
            }

            record.training_timesteps = summary.TrainingTimesteps;
            record.training_seed = summary.TrainingSeed;
        }

        private static void ApplyArtifactMetadata(EnvForgeJobRecordDto record, ResultArtifacts artifacts)
        {
            if (artifacts == null)
            {
                return;
            }

            record.replay_artifact_path = artifacts.ReplayBundle?.Path;
            record.onnx_artifact_path = artifacts.OnnxModel?.Path ??
                artifacts.SentisModel?.Path ??
                artifacts.Model?.Path;
        }

        private static string FormatStatus(ResultStatus status)
        {
            return status.ToString().ToLowerInvariant();
        }

        private static bool IsTerminalStatus(ResultStatus status)
        {
            return status == ResultStatus.Completed ||
                status == ResultStatus.Failed ||
                status == ResultStatus.Cancelled;
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
        public string display_name;
        public string settings_name;
        public string cancel_token;
        public string scenario_id;
        public string submitted_at_utc;
        public string result_updated_at;
        public string history_updated_at_utc;
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
