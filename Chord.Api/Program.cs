using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Chord.Api.Services;

namespace Chord.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length < 2)
            throw new ArgumentException("Usage: dotnet run <bindAddress> <port> [bootstrapAddress] [bootstrapPort]");

        var bindAddress = IPAddress.Parse(args[0]);
        var bindPort = int.Parse(args[1]);
        var hasBootstrap = args.Length >= 4;
        IPAddress? bootstrapAddress = hasBootstrap ? IPAddress.Parse(args[2]) : null;
        int bootstrapPort = hasBootstrap ? int.Parse(args[3]) : 0;

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls($"http://{ChordClient.FormatHost(bindAddress)}:{bindPort}");


        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        builder.Services.AddSingleton(new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10),
            DefaultRequestVersion = new Version(2, 0),
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        });

        builder.Services.AddControllers();
        builder.Services.AddSingleton<ChordClient>();
        builder.Services.AddSingleton(sp =>
        {
            var client = sp.GetRequiredService<ChordClient>();
            return new ChordNetworkNode(capacity: 20, chordClient: client, address: bindAddress, port: bindPort);
        });

        var app = builder.Build();
        app.MapControllers();

        app.Lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                var node = app.Services.GetRequiredService<ChordNetworkNode>();
                node.LeaveAsync().GetAwaiter().GetResult();
                Console.WriteLine($"[SHUTDOWN] Node {node.Id} left the ring gracefully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SHUTDOWN] Leave failed: {ex.Message}");
            }
        });

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var node = app.Services.GetRequiredService<ChordNetworkNode>();
            var stopToken = app.Lifetime.ApplicationStopping;

            _ = Task.Run(async () =>
            {
                var me = node.Id;
                Console.WriteLine($"[BOOT] Node {me} at {node.Address}:{node.Port} started");

                await Task.Delay(500, stopToken);

                if (hasBootstrap && bootstrapAddress is not null)
                {
                    Console.WriteLine($"[BOOT] Join via {bootstrapAddress}:{bootstrapPort}");
                    try
                    {
                        await node.JoinAsync(bootstrapAddress, bootstrapPort);
                        Console.WriteLine($"[BOOT] Join OK: pred={(node.Predecessor?.Id.ToString() ?? "null")}, succ={node.Successor.Id}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BOOT] Join failed: {ex.Message}");
                        node.Create();
                        Console.WriteLine($"[BOOT] Fallback -> created ring for {me}");
                    }
                }
                else
                {
                    node.Create();
                    Console.WriteLine($"[BOOT] Created ring for {me}");
                }

                _ = Task.Run(async () =>
                {
                    while (!stopToken.IsCancellationRequested)
                    {
                        try
                        {
                            await node.StabilizeOnceAsync();
                            await node.BuildFingersAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[STABILIZE] {ex.Message}");
                        }

                        try
                        {
                            await Task.Delay(5000, stopToken);
                        }
                        catch (OperationCanceledException) { break; }
                    }
                }, stopToken);

                _ = Task.Run(async () =>
                {
                    var client = app.Services.GetRequiredService<ChordClient>();
                    while (!stopToken.IsCancellationRequested)
                    {
                        try
                        {
                            var succ = node.Successor;

                            if (succ != null && succ.Id != node.Id)
                            {
                                var ok = await client.PingNodeAsync(IPAddress.Parse(succ.Address), succ.Port, 2);
                                if (!ok)
                                {
                                    Console.WriteLine($"[SPLICE] Successor {succ.Id} unreachable → splice");
                                    await node.SpliceOutByIdAsync(succ.Id);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SPLICE] {ex.Message}");
                        }

                        try
                        {
                            await Task.Delay(2000, stopToken);
                        }
                        catch (OperationCanceledException) { break; }
                    }
                }, stopToken);
            });
        });

        await app.RunAsync();
    }
}
