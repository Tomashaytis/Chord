using System.Net;
using Chord.Api.Services;

namespace Chord.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var address = IPAddress.Parse(args[0]);
        var port = int.Parse(args[1]);
        var bootstrapNodeAddress = args.Length > 2 ? IPAddress.Parse(args[2]) : null;
        var bootstrapNodePort = args.Length > 3 ? int.Parse(args[3]) : throw new ArgumentException("Bootstrap node port not set");

        var capacity = 20;

        var httpClient = new HttpClient();
        var networkClient = new ChordClient(httpClient);
        var ChordNetworkNode = new ChordNetworkNode(capacity, networkClient, address, port);

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls($"http://localhost:{port}");

        builder.Services.AddSingleton(ChordNetworkNode);
        builder.Services.AddSingleton(networkClient);

        var app = builder.Build();

        app.MapGet("/chord/ping", () => "alive");
        app.MapGet("/chord/find-successor", (int key, ChordNetworkNode node) =>
        {
            var successor = node.FindSuccessor(key);
            return Results.Json(successor);
        });

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            Console.WriteLine($"Chord node {ChordNetworkNode.Id} started on port {address}:{port}");

            if (bootstrapNodeAddress != null)
            {
                var bootstrapNodeId = ChordNetworkNode.ComputeNodeId(bootstrapNodeAddress, bootstrapNodePort, capacity);
                ChordNetworkNode.Join(bootstrapNodeId);
            }
            else
            {
                ChordNetworkNode.Create();
            }
        });

        app.Run();
    }
}
