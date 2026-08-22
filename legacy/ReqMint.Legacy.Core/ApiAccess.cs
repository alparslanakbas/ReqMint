using System.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace ReqMint.Legacy.Core
{
    public class ApiAccess : IApiAccess
    {
        private readonly HttpClient _httpClient = new();

        public async Task<(string jsonResponse, bool isDownloadable)>
            CallApiAsync
            (
                string url,
                string content,
                HttpAction action = HttpAction.GET,
                bool formatOutput = true
            )
        {
            StringContent stringContent = new(content, Encoding.UTF8, "application/json");
            return await CallApiAsync(url,stringContent, action, formatOutput);
        }

            #region CallApi
            public async Task<(string jsonResponse, bool isDownloadable)>
            CallApiAsync
            (
                string url,
                HttpContent? content = null,
                HttpAction action = HttpAction.GET,
                bool formatOutput = true
            )
        {
            HttpResponseMessage? response;

            switch (action)
            {
                case HttpAction.GET:
                    response = await _httpClient.GetAsync(url);
                    break;
                case HttpAction.POST:
                    response = await _httpClient.PostAsync(url, content);
                    break;
                case HttpAction.PUT:
                    response = await _httpClient.PutAsync(url, content);
                    break;
                case HttpAction.PATCH:
                    response = await _httpClient.PatchAsync(url, content);
                    break;
                case HttpAction.DELETE:
                    response = await _httpClient.DeleteAsync(url);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action,null);
            }

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();

                var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

                // check the json datas array or object
                bool isDownloadable = jsonElement.ValueKind == JsonValueKind.Array || jsonElement.ValueKind == JsonValueKind.Object;

                if (formatOutput)
                {
                    string json = JsonSerializer.Serialize(jsonElement, new JsonSerializerOptions { WriteIndented = true });
                    return (json, isDownloadable);
                }

                return (jsonResponse, isDownloadable);
            }
            else
            {
                return ($"Error: {response.StatusCode}", false);
            }
        }
        #endregion

        #region Check Url Valid
        public bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            bool output = Uri.TryCreate(url, UriKind.Absolute, out Uri uriOutput)
                &&
                (uriOutput.Scheme == Uri.UriSchemeHttps);

            return output;
        }
        #endregion

    }
}
