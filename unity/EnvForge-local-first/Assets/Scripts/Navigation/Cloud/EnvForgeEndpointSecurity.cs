using System;
using EmbodiedLab.Unity;

namespace EnvForge.Navigation.Cloud
{
    internal static class EnvForgeEndpointSecurity
    {
        public static void Validate(EmbodiedLabEndpoints endpoints)
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            ValidateUri(endpoints.ApiBaseUri, "API");
            ValidateUri(endpoints.ResultWebSocketBaseUri, "result WebSocket");
        }

        private static void ValidateUri(Uri endpoint, string endpointName)
        {
            if (!string.IsNullOrEmpty(endpoint.UserInfo))
            {
                throw new ArgumentException($"{endpointName} URL cannot contain user information.");
            }

            bool insecure = endpoint.Scheme == Uri.UriSchemeHttp ||
                string.Equals(endpoint.Scheme, "ws", StringComparison.OrdinalIgnoreCase);
            if (insecure && !endpoint.IsLoopback)
            {
                throw new ArgumentException($"{endpointName} URL must use TLS unless it targets loopback.");
            }
        }
    }
}
