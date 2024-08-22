using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace salessssssssss
{
    public class SalesforceService : ISalesforceService
    {
        private readonly HttpClient _httpClient;
        private const string DOMAIN_NAME = "https://login.salesforce.com"; // Hardcoded domain name

        public ObservableCollection<SalesforceObject> SalesforceObjects { get; set; }

        public SalesforceService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            SalesforceObjects = new ObservableCollection<SalesforceObject>();
        }

        public async Task<string> GetAccessTokenAsync(string username, string password, string consumerKey, string consumerSecret)
        {
            var parameters = new Dictionary<string, string>
            {
                { "grant_type", "password" },
                { "client_id", consumerKey },
                { "client_secret", consumerSecret },
                { "username", username },
                { "password", password }
            };

            var content = new FormUrlEncodedContent(parameters);

            try
            {
                var response = await _httpClient.PostAsync($"{DOMAIN_NAME}/services/oauth2/token", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var tokenData = JsonDocument.Parse(jsonResponse);
                    var accessToken = tokenData.RootElement.GetProperty("access_token").GetString();
                    var instanceUrl = tokenData.RootElement.GetProperty("instance_url").GetString();

                    return $"{accessToken}|{instanceUrl}";
                }
                else
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Failed to obtain access token. Response: {errorResponse}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Exception occurred while trying to obtain access token: {ex.Message}");
            }
        }

        public async Task RetrieveAllObjects(string instanceUrl, string accessToken)
        {
            var requestUrl = $"{instanceUrl}/services/data/v59.0/sobjects";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            try
            {
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var allObjectsData = JsonDocument.Parse(jsonResponse);

                    if (SalesforceObjects == null)
                    {
                        SalesforceObjects = new ObservableCollection<SalesforceObject>();
                    }
                    else
                    {
                        SalesforceObjects.Clear();
                    }

                    if (allObjectsData.RootElement.TryGetProperty("sobjects", out var sobjectsArray))
                    {
                        foreach (var sobject in sobjectsArray.EnumerateArray())
                        {
                            var name = sobject.GetProperty("name").GetString();
                            var label = sobject.GetProperty("label").GetString();

                            // Fetch additional details for filtering
                            var describeRequestUrl = $"{instanceUrl}/services/data/v59.0/sobjects/{name}/describe";
                            var describeRequest = new HttpRequestMessage(HttpMethod.Get, describeRequestUrl);
                            describeRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                            var describeResponse = await _httpClient.SendAsync(describeRequest);
                            var describeJsonResponse = await describeResponse.Content.ReadAsStringAsync();
                            var describeData = JsonDocument.Parse(describeJsonResponse);

                            // Check object properties to determine if it should be included
                            var isCreateable = describeData.RootElement.GetProperty("createable").GetBoolean();
                            var isUpdateable = describeData.RootElement.GetProperty("updateable").GetBoolean();
                            var isLayoutable = describeData.RootElement.GetProperty("layoutable").GetBoolean();
                            var isSearchable = describeData.RootElement.GetProperty("searchable").GetBoolean();

                            if (isCreateable && isUpdateable && isLayoutable && isSearchable)
                            {
                                var childRelationshipsCount = describeData.RootElement
                                    .GetProperty("childRelationships")
                                    .GetArrayLength();

                                var fieldsCount = describeData.RootElement
                                    .GetProperty("fields")
                                    .GetArrayLength();

                                var validationResultsCount = await GetValidationRulesCount(instanceUrl, accessToken, label);

                                var recordTypeInfosCount = describeData.RootElement
                                    .TryGetProperty("recordTypeInfos", out var recordTypeProperty)
                                    ? recordTypeProperty.GetArrayLength()
                                    : 0;

                                var isCustomObject = name.EndsWith("__c");

                                SalesforceObjects.Add(new SalesforceObject
                                {
                                    Label = label,
                                    ApiName = name,
                                    Type = isCustomObject ? "Custom" : "Standard",
                                    Description = sobject.TryGetProperty("description", out var descriptionProperty) ? descriptionProperty.GetString() : "N/A",
                                    LastModified = sobject.TryGetProperty("lastModifiedDate", out var lastModifiedProperty) ? lastModifiedProperty.GetString() : "N/A",
                                    ChildRelationshipsCount = childRelationshipsCount,
                                    Fields = fieldsCount,
                                    ValidationResults = validationResultsCount,
                                    RecordTypeInfos = recordTypeInfosCount
                                });
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("No objects found in response.");
                    }
                }
                else
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Failed to retrieve objects. ErrorCode: {response.StatusCode}, Message: {errorResponse}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Exception occurred while trying to retrieve objects: {ex.Message}");
            }
        }

        private async Task<int> GetValidationRulesCount(string instanceUrl, string accessToken, string objectName)
        {
            // Ensure objectName is URL-safe
            var query = $"SELECT Id, ValidationName FROM ValidationRule WHERE EntityDefinition.DeveloperName = '{objectName}'";
            var requestUrl = $"{instanceUrl}/services/data/v59.0/tooling/query/?q={Uri.EscapeDataString(query)}";

            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            try
            {
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var jsonData = JsonDocument.Parse(jsonResponse);

                    if (jsonData.RootElement.TryGetProperty("totalSize", out var totalSizeProperty))
                    {
                        return totalSizeProperty.GetInt32();
                    }
                    else
                    {
                        throw new Exception("TotalSize not found in validation rules response.");
                    }
                }
                else
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Failed to retrieve validation rules. ErrorCode: {response.StatusCode}, Message: {errorResponse}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Exception occurred while trying to retrieve validation rules: {ex.Message}");
            }
        }
    }
}
