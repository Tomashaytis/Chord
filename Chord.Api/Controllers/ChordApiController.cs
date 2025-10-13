using Microsoft.AspNetCore.Mvc;
using Chord.Api.Services;
using System.Net;

namespace Chord.Api.Controllers;

[ApiController]
[Route("chord")]
public class ChordController(ChordNetworkNode networkNode) : ControllerBase
{
    public ChordNetworkNode NetworkNode { get; } = networkNode;

    [HttpGet("ping")]
    public IActionResult Ping() => Ok("alive");


    [HttpGet("find-successor")]
    public async Task<ActionResult<ChordClient.NetworkNodeDto>> FindSuccessor([FromQuery] int key)
    {
        var selfId = NetworkNode.Id;
        var succ = NetworkNode.Successor;

        bool InOC(int x, int a, int b)
        {
            if (a == b) return true;
            if (a < b) return a < x && x <= b;
            return x > a || x <= b;
        }

        if (InOC(key, selfId, succ.Id) || succ.Id == selfId)
            return Ok(succ);

        var nextHop = NetworkNode.PickNextHop(key);

        var next = await NetworkNode.ChordClient.FindSuccessorAsync(IPAddress.Parse(nextHop.Address), nextHop.Port, key, 5);

        if (next is null) 
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        return Ok(next);
    }

    [HttpPost("notify")]
    public IActionResult Notify([FromBody] ChordClient.NetworkNodeDto notifier)
    {
        var previousPred = NetworkNode.Predecessor;
        NetworkNode.Notify(notifier);
        return Ok(new { previousPredecessor = previousPred });
    }

    [HttpGet("info")]
    public ActionResult<object> GetNodeInfo() => Ok(NetworkNode.GetInfo());
}
