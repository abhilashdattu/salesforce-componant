using System.Collections.ObjectModel;
using System.Threading.Tasks;
using salessssssssss;

public interface ISalesforceService
{
    Task<string> GetAccessTokenAsync(string username, string password, string consumerKey, string consumerSecret);
    Task RetrieveAllObjects(string instanceUrl, string accessToken);
    ObservableCollection<SalesforceObject> SalesforceObjects { get; }
}
