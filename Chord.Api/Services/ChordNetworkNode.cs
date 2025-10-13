using System.Net;
using System.Security.Cryptography;
using System.Text;
using Chord.Domain.Entities;

namespace Chord.Api.Services;

public class ChordNetworkNode
{
    public ChordNode LocalNode { get; }
    public ChordClient ChordClient { get; }
    public IPAddress Address { get; }
    public int Port { get; }

    public int Id => LocalNode.Id;
    private readonly int _capacity;

    public ChordClient.NetworkNodeDto? Predecessor { get; private set; }
    public ChordClient.NetworkNodeDto Successor { get; private set; }

    public ChordNetworkNode(int capacity, ChordClient chordClient, IPAddress address, int port)
    {
        _capacity = capacity;
        ChordClient = chordClient;
        Address = address;
        Port = port;
        LocalNode = new ChordNode(ComputeNodeId(address, port, capacity), capacity);

        var self = new ChordClient.NetworkNodeDto(LocalNode.Id, address.ToString(), port);
        Successor = self;
        Predecessor = null;
    }

    public static int ComputeNodeId(IPAddress address, int port, int capacity)
    {
        var input = $"{address}:{port}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Math.Abs(BitConverter.ToInt32(hash, 0)) % (1 << capacity);
    }

    public void Create()
    {
        Predecessor = null;
        Successor = new ChordClient.NetworkNodeDto(LocalNode.Id, Address.ToString(), Port);
        LocalNode.Create();

        Console.WriteLine($"[RING] Create: node={LocalNode.Id} at {Address}:{Port}");
        Console.WriteLine($"[RING] State: predecessor=null, successor={Successor.Id}");

    }

    public async Task JoinAsync(IPAddress bootstrapAddress, int bootstrapPort)
    {
        await Task.Delay(500);

        var succ = await ChordClient.FindSuccessorAsync(bootstrapAddress, bootstrapPort, LocalNode.Id, timeoutSeconds: 5);

        if (succ is null)
        {
            var bootstrap = await ChordClient.GetInfoAsync(bootstrapAddress, bootstrapPort, timeoutSeconds: 5)
                            ?? throw new InvalidOperationException("Bootstrap unavailable.");
            succ = bootstrap;
        }

        Successor = succ;

        var prevPred = await ChordClient.NotifyAsync(
            IPAddress.Parse(succ.Address), succ.Port,
            new ChordClient.NetworkNodeDto(LocalNode.Id, Address.ToString(), Port));

        Predecessor = prevPred ?? succ;

        if (prevPred is not null && prevPred.Id != LocalNode.Id)
        {
            await ChordClient.NotifyAsync(IPAddress.Parse(prevPred.Address), prevPred.Port,
                new ChordClient.NetworkNodeDto(LocalNode.Id, Address.ToString(), Port));
        }
    }

    public void Notify(ChordClient.NetworkNodeDto notifier)
    {

        if (Predecessor == null || InIntervalOpenClosed(notifier.Id, Predecessor.Id, Id))
            Predecessor = notifier;

        if (InIntervalOpenClosed(notifier.Id, Id, Successor.Id) || Successor.Id == Id)
            Successor = notifier;
    }

    public object GetInfo() => new
    {
        id = LocalNode.Id,
        address = Address.ToString(),
        port = Port,
        predecessor = Predecessor,
        successor = Successor
    };

    private static bool InIntervalOpenClosed(int x, int a, int b)
    {
        if (a == b) return true;
        if (a < b) return a < x && x <= b;
        return x > a || x <= b;
    }
}
