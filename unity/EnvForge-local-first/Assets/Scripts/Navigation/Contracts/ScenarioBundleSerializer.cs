using UnityEngine;

namespace EnvForge.Navigation.Contracts
{
    public static class ScenarioBundleSerializer
    {
        public static string ToJson(ScenarioBundleDto scenarioBundle, bool prettyPrint = true)
        {
            return JsonUtility.ToJson(scenarioBundle, prettyPrint);
        }

        public static ScenarioBundleDto FromScenarioBundleJson(string json)
        {
            return JsonUtility.FromJson<ScenarioBundleDto>(json);
        }

        public static ResultBundleDto FromResultBundleJson(string json)
        {
            return JsonUtility.FromJson<ResultBundleDto>(json);
        }

        public static ResultDocumentDto FromResultDocumentJson(string json)
        {
            return JsonUtility.FromJson<ResultDocumentDto>(json);
        }

        public static ReplayBundleManifestDto FromReplayBundleManifestJson(string json)
        {
            return JsonUtility.FromJson<ReplayBundleManifestDto>(json);
        }
    }
}
