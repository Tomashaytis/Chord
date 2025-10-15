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
            var url = $"http://{FormatHost(ip)}:{port}/chord/info?mode=basic"; 
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return await HttpClient.GetFromJsonAsync<NetworkNodeDto>(url, opts, cts.Token);
        }
        catch { return null; }
    }
    public async Task<NetworkNodeDto?> GetPredecessorAsync(IPAddress ip, int port, double timeoutSeconds = 5)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var url = $"http://{FormatHost(ip)}:{port}/chord/info";
            using var resp = await HttpClient.GetAsync(url, cts.Token);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(cts.Token), cancellationToken: cts.Token);
            if (!doc.RootElement.TryGetProperty("predecessor", out var pp) || pp.ValueKind != JsonValueKind.Object)
                return null;

            int id = pp.TryGetProperty("id", out var i1) && i1.TryGetInt32(out var iv) ? iv : -1;
            string? addr = pp.TryGetProperty("address", out var a1) ? a1.GetString() : null;
            int prt = pp.TryGetProperty("port", out var p1) && p1.TryGetInt32(out var pv) ? pv : -1;

            return (id >= 0 && addr is not null && prt >= 0) ? new NetworkNodeDto(id, addr, prt) : null;
        }
        catch { return null; }
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

    public async Task<bool> SetPredecessorAsync(IPAddress ip, int port, NetworkNodeDto? pred, double timeoutSeconds = 5)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var url = $"http://{FormatHost(ip)}:{port}/chord/set-predecessor";
            var resp = await HttpClient.PostAsJsonAsync(url, pred, cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> SetSuccessorAsync(IPAddress ip, int port, NetworkNodeDto succ, double timeoutSeconds = 5)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var url = $"http://{FormatHost(ip)}:{port}/chord/set-successor";
            var resp = await HttpClient.PostAsJsonAsync(url, succ, cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> StabilizeAsync(IPAddress ip, int port, double timeoutSeconds = 5)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var url = $"http://{FormatHost(ip)}:{port}/chord/stabilize";
            var resp = await HttpClient.PostAsync(url, content: null, cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> PingNodeAsync(IPAddress ip, int port, int timeoutSeconds = 2)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var url = $"http://{FormatHost(ip)}:{port}/chord/ping";
            using var resp = await HttpClient.GetAsync(url, cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }


}
