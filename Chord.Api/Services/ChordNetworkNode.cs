using System.Net;
using System.Security.Cryptography;
using System.Text;
using Chord.Domain.Entities;

namespace Chord.Api.Services;

public class ChordNetworkNode(int capacity, ChordClient chordClient, IPAddress address, int port)
{
    public ChordNode LocalNode { get; private set; } = new ChordNode(ComputeNodeId(address, port, capacity), capacity);

    public ChordClient ChordClient { get; private set; } = chordClient;

    public IPAddress Address { get; private set; } = address;

    public int Port { get; private set; } = port;

    public string BaseUrl { get; private set; } = $"http://{address}:{port}";

    public int Id => LocalNode.Id;

    public static int ComputeNodeId(IPAddress address, int port, int capacity)
    {
        var input = $"{address}:{port}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Math.Abs(BitConverter.ToInt32(hash, 0)) % (1 << capacity);
    }
}
