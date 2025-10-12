using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace Chord.Domain.Entities;

public class ChordNode
{
    public int Id { get; private set; }

    public int Capacity { get; private set; }

    public FingerEntry[] Fingers { get; private set; }

    public ChordNode? Predecessor { get; private set; }

    public ChordNode Successor { get; private set; }

    public int NextFingerIndex { get; private set; }

    public ChordNode(int nodeId, int capacity)
    {
        Capacity = capacity;
        NextFingerIndex = 0;

        Id = nodeId;

        Fingers = new FingerEntry[capacity];
        InitFingerTable();

        Predecessor = null;
        Successor = this;
    }

    public static int ComputeFingerId(int id, int i, int capacity)
    {
        return (id + (1 << i)) % (1 << capacity);
    }

    public void InitFingerTable()
    {
        for (int i = 0; i < Capacity; i++)
        {
            int start = ComputeFingerId(Id, i, Capacity);
            int end = ComputeFingerId(Id, i + 1, Capacity);

            Fingers[i] = new FingerEntry(start, new FingerInterval(start, end), this);
        }
    }

    public ChordNode FindSuccessor(int keyId)
    {
        if (IsKeyInRange(keyId, Id, Successor.Id))
            return Successor;

        var node = ClosestPrecedingFinger(keyId);
        return node.FindSuccessor(keyId);
    }

    public ChordNode ClosestPrecedingFinger(int keyId)
    {
        for (int i = Capacity; i >= 0; i--)
            if (IsKeyInRange(Fingers[i].Node.Id, Id, keyId, inclusiveEnd: false))
                return Fingers[i].Node;

        return this;
    }

    public void Create()
    {
        Predecessor = null;
        Successor = this;
    }

    public void Join(ChordNode node)
    {
        Predecessor = null;
        Successor = node.FindSuccessor(Id);
    }

    public void Stabilize()
    {
        var node = Successor.Predecessor;
        
        if (node != null && Id < node.Id && node.Id <= Successor.Id)
            Successor = node;

        Successor.Notify(this);
    }

    public void Notify(ChordNode node)
    {
        if (Predecessor == null || IsKeyInRange(node.Id, Predecessor.Id, Id))
            Predecessor = node;
    }

    public void CheckPredecessor()
    {
        if (Predecessor != null && !Predecessor.IsAlive())
            Predecessor = null;
    }

    public void FixFingers()
    {
        NextFingerIndex = (NextFingerIndex + 1) % Capacity;

        Fingers[NextFingerIndex].Node = FindSuccessor(Id + (1 << NextFingerIndex));
    }

    public bool IsAlive()
    {
        return true;
    }

    public static bool IsKeyInRange(int id, int start, int end, bool inclusiveStart = false, bool inclusiveEnd = true)
    {
        bool first = inclusiveStart ? start <= id : start < id;
        bool second = inclusiveEnd ? id <= end : id < end;
        return first && second;
    }
}
