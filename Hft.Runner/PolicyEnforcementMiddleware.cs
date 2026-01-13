using System;
using System.Net.Http;
using System.Threading.Tasks;

// Place in Hft.Infra or Hft.Runner
namespace Hft.Runner
{
    internal sealed class PolicyEnforcementMiddleware : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _governanceUri;

        public PolicyEnforcementMiddleware(string governanceUrl)
        {
            if (!Uri.TryCreate(governanceUrl, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("Invalid URL", nameof(governanceUrl));
            }
            _governanceUri = uri;
            _httpClient = new HttpClient();
        }

        public bool CheckAuthorization(string strategyName)
        {
            try
            {
                var requestUri = new Uri(_governanceUri, $"policies/{strategyName}");
                var response = _httpClient.GetAsync(requestUri).ConfigureAwait(false).GetAwaiter().GetResult();
                
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                 // Log?
                 return false; 
            }
            catch (Exception)
            {
                 return false; 
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
