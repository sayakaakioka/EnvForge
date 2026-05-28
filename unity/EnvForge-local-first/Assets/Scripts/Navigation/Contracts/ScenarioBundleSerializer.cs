using UnityEngine;

namespace EnvForge.Navigation.Contracts
{
    public static class ScenarioBundleSerializer
    {
        public static string ToJson(ScenarioBundleDto scenarioBundle, bool prettyPrint = true)
        {
            return JsonUtility.ToJson(scenarioBundle, prettyPrint);
        }
    }
}
