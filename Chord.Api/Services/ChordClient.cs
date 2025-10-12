using System.Net;
using System.Net.Sockets;
using Chord.Domain.Entities;

namespace Chord.Api.Services;

public class ChordClient(HttpClient httpClient)
{
    public HttpClient HttpClient { get; private set; } = httpClient;

    public async Task<bool> PingNodeAsync(IPAddress ipAddress, int port, double timeout = 5)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

            string host = FormatHost(ipAddress);

            var response = await HttpClient.GetAsync($"http://{host}:{port}/chord/ping", cts.Token);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    public async Task<ChordNode?> FindSuccessorAsync(IPAddress ipAddress, int port, int key)
    {
        string host = FormatHost(ipAddress);
        var response = await HttpClient.GetAsync($"http://{host}:{port}/chord/find-successor?key={key}");
        return await response.Content.ReadFromJsonAsync<ChordNode>();
    }

    public static string FormatHost(IPAddress ipAddress)
    {
        return ipAddress.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{ipAddress}]" : ipAddress.ToString();
    }
}
