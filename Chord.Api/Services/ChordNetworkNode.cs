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

    public void SetPredecessor(ChordClient.NetworkNodeDto? pred)
    {
        Predecessor = pred;
    }

    public void SetSuccessor(ChordClient.NetworkNodeDto succ)
    {
        Successor = succ;
    }

    public async Task LeaveAsync()
    {
        var pred = Predecessor;
        var succ = Successor;

        if (succ.Id == Id && pred is null)
        {
            Predecessor = null;
            Successor = new ChordClient.NetworkNodeDto(Id, Address.ToString(), Port);
            _fingers.Clear();
            return;
        }

        if (pred is not null && succ is not null && succ.Id != Id)
        {
            await ChordClient.SetSuccessorAsync(IPAddress.Parse(pred.Address), pred.Port, succ);
            await ChordClient.SetPredecessorAsync(IPAddress.Parse(succ.Address), succ.Port, pred);

            await ChordClient.StabilizeAsync(IPAddress.Parse(pred.Address), pred.Port);
            await ChordClient.StabilizeAsync(IPAddress.Parse(succ.Address), succ.Port);
        }
        else if (pred is not null)
        {
            await ChordClient.SetSuccessorAsync(IPAddress.Parse(pred.Address), pred.Port,
                new ChordClient.NetworkNodeDto(Id, Address.ToString(), Port));
            await ChordClient.StabilizeAsync(IPAddress.Parse(pred.Address), pred.Port);
        }
        else if (succ is not null && succ.Id != Id)
        {
            await ChordClient.SetPredecessorAsync(IPAddress.Parse(succ.Address), succ.Port, null);
            await ChordClient.StabilizeAsync(IPAddress.Parse(succ.Address), succ.Port);
        }

        Predecessor = null;
        Successor = new ChordClient.NetworkNodeDto(Id, Address.ToString(), Port);
        _fingers.Clear();
    }

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
            await ChordClient.NotifyAsync(IPAddress.Parse(prevPred.Address), prevPred.Port,new ChordClient.NetworkNodeDto(LocalNode.Id, Address.ToString(), Port));
        }
    }

    public void Notify(ChordClient.NetworkNodeDto notifier)
    {

        if (Predecessor == null || InIntervalOpenClosed(notifier.Id, Predecessor.Id, Id))
            Predecessor = notifier;

        if (InIntervalOpenClosed(notifier.Id, Id, Successor.Id) || Successor.Id == Id)
            Successor = notifier;
    }

    public object GetInfo(int? key = null)
    {
        return new
        {
            id = LocalNode.Id,
            address = Address.ToString(),
            port = Port,
            predecessor = Predecessor,
            successor = Successor
        };
    }
    private static bool InIntervalOpenClosed(int x, int a, int b)
    {
        if (a == b) return true;
        if (a < b) return a < x && x <= b;
        return x > a || x <= b;
    }

    private readonly Dictionary<int, ChordClient.NetworkNodeDto> _fingers = new();

    public async Task BuildFingersAsync()
    {
        for (int i = 0; i < _capacity; i++)
        {
            int start = (LocalNode.Id + (1 << i)) & ((1 << _capacity) - 1);
            var succ = await ChordClient.FindSuccessorAsync(IPAddress.Parse(Successor.Address), Successor.Port, start, 5);

            if (succ != null) _fingers[i] = succ;
        }
    }

    public ChordClient.NetworkNodeDto PickNextHop(int key)
    {
        for (int i = _capacity - 1; i >= 0; i--)
        {
            if (_fingers.TryGetValue(i, out var f))
            {
                if (InIntervalOpenClosed(f.Id, Id, key))
                    return f;
            }
        }
        return Successor; 
    }

    public async Task StabilizeOnceAsync()
    {
        var succ = Successor;
        if (succ.Id == Id) return; 

        var x = await ChordClient.GetPredecessorAsync(IPAddress.Parse(succ.Address), succ.Port, 5);
        if (x is not null && InIntervalOpenClosed(x.Id, Id, succ.Id))
        {
            Successor = x; 
        }

 
        await ChordClient.NotifyAsync(IPAddress.Parse(Successor.Address), Successor.Port,
            new ChordClient.NetworkNodeDto(LocalNode.Id, Address.ToString(), Port));

    
        await BuildFingersAsync();
    }


}
