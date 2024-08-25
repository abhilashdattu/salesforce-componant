using System;
using System.Net.Http;
using System.Text;
using System.Web;
using System.Security.Cryptography;
using Microsoft.Maui.Controls;
using System.Text.Json;
using System.Diagnostics;

namespace salessssssssss
{
    public partial class MainPage : ContentPage
    {
        private const string ClientId = "3MVG9VMBZCsTL9hl3P_LmcoOFsczZnvzFddU.Zspi83mq844SBzoi2Dv91palPwJESqxp1i7GASp60fm.K2A7";
        private const string RedirectUri = "http://localhost:8080/";
        private const string SalesforceAuthUrl = "https://login.salesforce.com/services/oauth2/authorize";
        private const string SalesforceTokenUrl = "https://login.salesforce.com/services/oauth2/token";
        private string _codeVerifier;

        public MainPage()
        {
            InitializeComponent();
            StartOAuthProcess();
        }

        private void StartOAuthProcess()
        {
            _codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(_codeVerifier);
            Debug.WriteLine("COde Challenge", codeChallenge);

            var authUrl = $"{SalesforceAuthUrl}?response_type=code&client_id={ClientId}&redirect_uri={RedirectUri}&code_challenge={codeChallenge}&code_challenge_method=S256";

            SalesforceWebView.Source = authUrl;
        }

        private void OnNavigated(object sender, WebNavigatedEventArgs e)
        {
            var uri = new Uri(e.Url);
            var queryParams = HttpUtility.ParseQueryString(uri.Query);

            if (queryParams["code"] != null)
            {
                var authorizationCode = queryParams["code"];
                GetAccessTokenAsync(authorizationCode);
            }
        }

        private async void GetAccessTokenAsync(string authorizationCode)
        {
            using (var httpClient = new HttpClient())
            {
                var parameters = new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "client_id", ClientId },
                    { "redirect_uri", RedirectUri },
                    { "code_verifier", _codeVerifier },
                    { "code", authorizationCode }
                };

                var content = new FormUrlEncodedContent(parameters);
                var response = await httpClient.PostAsync(SalesforceTokenUrl, content);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                // Parse JSON response to get access token and instance URL
                var jsonDoc = JsonDocument.Parse(jsonResponse);
                Debug.WriteLine("Data", jsonDoc);
                var accessToken = jsonDoc.RootElement.GetProperty("access_token").GetString();
                var instanceUrl = jsonDoc.RootElement.GetProperty("instance_url").GetString();

                // Use accessToken and instanceUrl to fetch Salesforce objects
                await FetchSalesforceObjectsAsync(instanceUrl, accessToken);
            }
        }

        private async Task FetchSalesforceObjectsAsync(string instanceUrl, string accessToken)
        {
            using (var httpClient = new HttpClient())
            {
                var requestUrl = $"{instanceUrl}/services/data/v59.0/sobjects";
                var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.SendAsync(request);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                // Handle JSON response and display objects (e.g., in a ListView)
                // ...
            }
        }

        private string GenerateCodeVerifier()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private string GenerateCodeChallenge(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                var challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
                return Convert.ToBase64String(challengeBytes)
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');
            }
        }
    }
}