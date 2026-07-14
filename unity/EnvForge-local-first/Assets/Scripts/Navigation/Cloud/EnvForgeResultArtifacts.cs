using EmbodiedLab.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EnvForge.Navigation.Cloud
{
    internal static class EnvForgeResultArtifacts
    {
        public static ResultArtifacts Resolve(ResultDocument result)
        {
            if (result?.ResultBundle?.Artifacts != null)
            {
                return result.ResultBundle.Artifacts;
            }

            if (result?.Artifacts is ResultArtifacts typedArtifacts)
            {
                return typedArtifacts;
            }

            if (result?.Artifacts is not JObject jsonArtifacts)
            {
                return null;
            }

            try
            {
                return jsonArtifacts.ToObject<ResultArtifacts>();
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
