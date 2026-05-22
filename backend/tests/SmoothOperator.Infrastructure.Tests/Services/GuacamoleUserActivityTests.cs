using System.Text;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for <see cref="GuacamoleProxyService.ContainsUserActivity"/> — the
/// filter that decides whether a client→guacd payload counts as genuine user
/// activity (and so resets the idle-timeout clock) or is merely protocol
/// keepalive traffic that must be ignored.
/// </summary>
public class GuacamoleUserActivityTests
{
    /// <summary>Encodes elements into a single Guacamole <c>LEN.value,…;</c> instruction.</summary>
    private static string Instr(params string[] elements)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < elements.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(elements[i].Length).Append('.').Append(elements[i]);
        }
        return sb.Append(';').ToString();
    }

    private static byte[] Payload(params string[] instructions)
        => Encoding.UTF8.GetBytes(string.Concat(instructions));

    [Fact]
    public void KeyInstruction_IsUserActivity()
        => Assert.True(GuacamoleProxyService.ContainsUserActivity(Payload(Instr("key", "65", "1"))));

    [Fact]
    public void MouseInstruction_IsUserActivity()
        => Assert.True(GuacamoleProxyService.ContainsUserActivity(Payload(Instr("mouse", "100", "200", "1"))));

    [Theory]
    [InlineData("touch")]
    [InlineData("clipboard")]
    public void OtherInputInstructions_AreUserActivity(string opcode)
        => Assert.True(GuacamoleProxyService.ContainsUserActivity(Payload(Instr(opcode, "x"))));

    [Fact]
    public void SizeInstruction_IsNotUserActivity()
        // `size` is display-geometry negotiation, not human input. The client emits
        // it automatically on display init, browser-window resize, and — for SSH
        // and other terminal protocols — in a continuous resize feedback loop with
        // guacd. Counting it as activity keeps an idle session alive forever.
        => Assert.False(GuacamoleProxyService.ContainsUserActivity(Payload(Instr("size", "1024", "768", "96"))));

    [Fact]
    public void SyncHeartbeat_IsNotUserActivity()
        => Assert.False(GuacamoleProxyService.ContainsUserActivity(Payload(Instr("sync", "1234567890123"))));

    [Fact]
    public void NopKeepAlive_IsNotUserActivity()
        => Assert.False(GuacamoleProxyService.ContainsUserActivity(Payload(Instr("nop"))));

    [Fact]
    public void MultipleKeepAliveInstructions_AreNotUserActivity()
        => Assert.False(GuacamoleProxyService.ContainsUserActivity(
            Payload(Instr("sync", "1"), Instr("nop"), Instr("ack", "0", "ok", "0"))));

    [Fact]
    public void KeepAliveFollowedByInput_IsUserActivity()
    {
        // A payload can carry several concatenated instructions; user input
        // anywhere in it counts, even after keepalive traffic.
        var payload = Payload(Instr("sync", "1"), Instr("key", "65", "1"));
        Assert.True(GuacamoleProxyService.ContainsUserActivity(payload));
    }

    [Fact]
    public void EmptyPayload_IsNotUserActivity()
        => Assert.False(GuacamoleProxyService.ContainsUserActivity([]));

    [Fact]
    public void MalformedPayload_IsNotUserActivity()
        => Assert.False(GuacamoleProxyService.ContainsUserActivity(Encoding.UTF8.GetBytes("not-a-guac-instruction")));
}
