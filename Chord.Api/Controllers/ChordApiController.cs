using Microsoft.AspNetCore.Mvc;
using Chord.Domain.Entities;
using Chord.Api.Services;

namespace Chord.Api.Controllers;

[ApiController]
[Route("chord")]
public class ChordController(ChordNetworkNode networkNode) : ControllerBase
{
    public ChordNetworkNode NetworkNode { get; private set; } = networkNode;

    [HttpGet("ping")]
    public IActionResult Ping() => Ok("alive");

    [HttpGet("find-successor")]
    public ActionResult<ChordNode> FindSuccessor([FromQuery] int key)
    {
        var successor = NetworkNode.LocalNode.FindSuccessor(key);
        return Ok(successor);
    }

    [HttpPost("notify")]
    public IActionResult Notify([FromBody] ChordNode notifier)
    {
        NetworkNode.LocalNode.Notify(notifier);
        return Ok();
    }

    [HttpGet("info")]
    public ActionResult<object> GetNodeInfo()
    {
        return new
        {
            NetworkNode.Id,
            Predecessor = NetworkNode.LocalNode.Predecessor?.Id,
            Successor = NetworkNode.LocalNode.Successor.Id,
            Fingers = NetworkNode.LocalNode.Fingers.Select(f => new {
                f.Start,
                f.Interval,
                NodeId = f.Node.Id
            })
        };
    }
}
