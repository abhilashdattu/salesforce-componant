using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Failed to retrieve objects. ErrorCode: {response.StatusCode}, Message: {errorResponse}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var allObjectsData = JsonDocument.Parse(jsonResponse);

                SalesforceObjects?.Clear();

                if (!allObjectsData.RootElement.TryGetProperty("sobjects", out var sobjectsArray))
                {
                    throw new Exception("No objects found in response.");
                }

                foreach (var sobject in sobjectsArray.EnumerateArray())
                {
                    var name = sobject.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() : "N/A";
                    var label = sobject.TryGetProperty("label", out var labelProperty) ? labelProperty.GetString() : "N/A";

                    // Get detailed entity information
                    var entityDetails = await GetEntityDetailsAsync(instanceUrl, accessToken, name);

                    if (entityDetails == "No records found.")
                    {
                        // Set all values to "N/A" if no records are found
                        SalesforceObjects.Add(new SalesforceObject
                        {
                            Label = label,
                            ApiName = name,
                            Type = name.EndsWith("__c") ? "Custom" : "Standard",
                            Description = "N/A",
                            DeploymentStatus = "N/A",
                            ChildRelationshipsCount = 0,
                            Fields = 0,
                            ValidationResults = 0,
                            RecordTypeInfos = 0,
                            LastModifiedBy = "N/A",
                            LastModifiedDate = "N/A"
                        });
                    }
                    else
                    {
                        // Parse the details returned from GetEntityDetailsAsync
                        var detailsArray = entityDetails.Split(',');

                        var deploymentStatus = detailsArray.Length > 0 ? detailsArray[0].Split(':')[1].Trim() : "N/A";
                        var description = detailsArray.Length > 2 ? detailsArray[2].Split(':')[1].Trim() : "N/A";
                        var lastModifiedBy = detailsArray.Length > 3 ? detailsArray[3].Split(':')[1].Trim() : "N/A";
                        var lastModifiedDateFromDetails = detailsArray.Length > 1 ? detailsArray[1].Split(':')[1].Trim() : "N/A"; // Renamed variable

                        var describeRequestUrl = $"{instanceUrl}/services/data/v59.0/sobjects/{name}/describe";
                        var describeRequest = new HttpRequestMessage(HttpMethod.Get, describeRequestUrl);
                        describeRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                        var describeResponse = await _httpClient.SendAsync(describeRequest);
                        var describeJsonResponse = await describeResponse.Content.ReadAsStringAsync();
                        var describeData = JsonDocument.Parse(describeJsonResponse);

                        var isCreateable = describeData.RootElement.TryGetProperty("createable", out var createableProperty) && createableProperty.GetBoolean();
                        var isUpdateable = describeData.RootElement.TryGetProperty("updateable", out var updateableProperty) && updateableProperty.GetBoolean();
                        var isLayoutable = describeData.RootElement.TryGetProperty("layoutable", out var layoutableProperty) && layoutableProperty.GetBoolean();
                        var isSearchable = describeData.RootElement.TryGetProperty("searchable", out var searchableProperty) && searchableProperty.GetBoolean();

                        if (isCreateable && isUpdateable && isLayoutable && isSearchable)
                        {
                            var childRelationshipsCount = describeData.RootElement
                                .TryGetProperty("childRelationships", out var childRelationshipsArray)
                                ? childRelationshipsArray.GetArrayLength()
                                : 0;

                            var fieldsCount = describeData.RootElement
                                .TryGetProperty("fields", out var fieldsArray)
                                ? fieldsArray.GetArrayLength()
                                : 0;

                            var validationResultsCount = await GetValidationRulesCount(instanceUrl, accessToken, name);

                            var recordTypeInfosCount = describeData.RootElement
                                .TryGetProperty("recordTypeInfos", out var recordTypeProperty)
                                ? recordTypeProperty.GetArrayLength()
                                : 0;

                            SalesforceObjects.Add(new SalesforceObject
                            {
                                Label = label,
                                ApiName = name,
                                Type = name.EndsWith("__c") ? "Custom" : "Standard", // Set type based on the name
                                Description = description,
                                DeploymentStatus = deploymentStatus,
                                ChildRelationshipsCount = childRelationshipsCount,
                                Fields = fieldsCount,
                                ValidationResults = validationResultsCount,
                                RecordTypeInfos = recordTypeInfosCount,
                                LastModifiedBy = lastModifiedBy,
                                LastModifiedDate = lastModifiedDateFromDetails // Updated to use renamed variable
                            });
                        }
                    }
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

        private async Task<string> GetEntityDetailsAsync(string instanceUrl, string accessToken, string developerName)
        {
            var query = $"SELECT DeploymentStatus, Description, LastModifiedDate, LastModifiedBy.Name FROM EntityDefinition WHERE DeveloperName = '{developerName}'";
            var requestUrl = $"{instanceUrl}/services/data/v57.0/tooling/query/?q={Uri.EscapeDataString(query)}";

            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Initialize default values
            var deploymentStatus = "N/A";
            var lastModifiedDate = "N/A";
            var description = "N/A";
            var lastModifiedBy = "N/A";

            try
            {
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Raw JSON Response: {jsonResponse}");

                    var jsonData = JsonDocument.Parse(jsonResponse);
                    var root = jsonData.RootElement;

                    Debug.WriteLine($"Raw Root Response: {root}");

                    if (root.TryGetProperty("records", out var recordsArray) && recordsArray.ValueKind == JsonValueKind.Array)
                    {
                        if (recordsArray.GetArrayLength() > 0)
                        {
                            var record = recordsArray[0];

                            if (record.TryGetProperty("DeploymentStatus", out var deploymentStatusProperty))
                            {
                                deploymentStatus = deploymentStatusProperty.GetString();
                            }

                            if (record.TryGetProperty("LastModifiedDate", out var lastModifiedDateProperty))
                            {
                                lastModifiedDate = lastModifiedDateProperty.GetString();
                                Debug.WriteLine($"LastModifiedDate: {lastModifiedDate}"); // Log the date value
                            }

                            if (record.TryGetProperty("Description", out var descriptionProperty))
                            {
                                description = descriptionProperty.GetString();
                            }

                           
                        }
                        else
                        {
                            return "No records found.";
                        }
                    }
                    else
                    {
                        return "No records found.";
                    }

                    return $"Deployment Status: {deploymentStatus}, " +
                           $"Last Modified Date: {lastModifiedDate}, " +
                           $"Description: {description}, " +
                           $"Last Modified By: {lastModifiedBy}";
                }
                else
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Failed to retrieve entity details. ErrorCode: {response.StatusCode}, Message: {errorResponse}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception: {ex}");
                return "An error occurred while retrieving records.";
            }
        }

    }
}
