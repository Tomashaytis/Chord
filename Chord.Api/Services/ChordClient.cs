using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Chord.Api.Services;

public class ChordClient(HttpClient httpClient)
{
    public HttpClient HttpClient { get; } = httpClient;

    public record NetworkNodeDto(int Id, string Address, int Port);

    public static string FormatHost(IPAddress ipAddress) =>
        ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"[{ipAddress}]" : ipAddress.ToString();

    public async Task<NetworkNodeDto?> GetInfoAsync(IPAddress ip, int port, double timeoutSeconds = 5)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var url = $"http://{FormatHost(ip)}:{port}/chord/info";
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return await HttpClient.GetFromJsonAsync<NetworkNodeDto>(url, opts, cts.Token);
        }
        catch
        {
            return null;
        }
    }

    public record NotifyResponse(NetworkNodeDto? PreviousPredecessor);

    public async Task<NetworkNodeDto?> NotifyAsync(IPAddress ip, int port, NetworkNodeDto notifier, double timeoutSeconds = 5)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var url = $"http://{FormatHost(ip)}:{port}/chord/notify";

            var resp = await HttpClient.PostAsJsonAsync(url, notifier, cts.Token);
            if (!resp.IsSuccessStatusCode) return null;

            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var body = await resp.Content.ReadFromJsonAsync<NotifyResponse>(opts, cts.Token);
            return body?.PreviousPredecessor;
        }
        catch
        {
            return null;
        }
    }

    public async Task<NetworkNodeDto?> FindSuccessorAsync(IPAddress ip, int port, int key, double timeoutSeconds = 5)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var url = $"http://{FormatHost(ip)}:{port}/chord/find-successor?key={key}";
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return await HttpClient.GetFromJsonAsync<NetworkNodeDto>(url, opts, cts.Token);
        }
        catch
        {
            return null;
        }
    }
}
