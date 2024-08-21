using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;

namespace salessssssssss
{
    public partial class potts : ContentPage
    {
        private readonly SalesforceService _salesforceService;
        private string _accessToken;
        private string _instanceUrl;

        public ObservableCollection<SalesforceObject> SalesforceObjects { get; set; }

        public potts()
        {
            InitializeComponent();

            var httpClient = new HttpClient();
            _salesforceService = new SalesforceService(httpClient);

            SalesforceObjects = new ObservableCollection<SalesforceObject>();
            ObjectsCollectionView.ItemsSource = SalesforceObjects;
        }

        private async void OnFetchDataClicked(object sender, EventArgs e)
        {
            OutputLabel.Text = "";
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;
            ObjectsCollectionView.IsVisible = false;

            var username = UsernameEntry.Text;
            var password = PasswordEntry.Text;
            var consumerKey = ConsumerKeyEntry.Text;
            var consumerSecret = ConsumerSecretEntry.Text;

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(consumerKey) ||
                string.IsNullOrWhiteSpace(consumerSecret))
            {
                OutputLabel.Text = "Please fill in all fields.";
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
                return;
            }

            try
            {
                // Obtain access token and instance URL
                var tokenData = await _salesforceService.GetAccessTokenAsync(username, password, consumerKey, consumerSecret);
                var tokens = tokenData.Split('|');
                if (tokens.Length != 2)
                {
                    throw new Exception("Invalid token data received.");
                }
                _accessToken = tokens[0];
                _instanceUrl = tokens[1];

                // Retrieve all Salesforce objects
                await _salesforceService.RetrieveAllObjects(_instanceUrl, _accessToken);

                // Update the ObservableCollection with retrieved data
                SalesforceObjects.Clear();
                foreach (var salesforceObject in _salesforceService.SalesforceObjects)
                {
                    SalesforceObjects.Add(salesforceObject);
                }

                // Update UI visibility
                OutputLabel.Text = "Data fetched successfully!";
                ObjectsCollectionView.IsVisible = SalesforceObjects.Count > 0;
            }
            catch (Exception ex)
            {
                OutputLabel.Text = $"Error: {ex.Message}";
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            }
        }
    }
}
