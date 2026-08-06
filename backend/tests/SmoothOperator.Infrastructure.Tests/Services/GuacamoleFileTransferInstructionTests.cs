using System.Net.WebSockets;
using System.Text;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using StackExchange.Redis;
using Xunit;

namespace SmoothOperator.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for <c>GuacamoleProxyService.ObserveClientToServerFileTransfer</c> — the
/// client→guacd half of the file-transfer instruction observer, which tracks `put`
/// (upload-stream open) and `blob` (upload bytes) instructions into
/// <c>GuacSessionRequest.PendingUploads</c> without materializing unrelated traffic.
/// </summary>
public class GuacamoleFileTransferInstructionTests
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

    private static GuacamoleProxyService.GuacSessionRequest BuildRequest(FileTransferPolicy policy) => new()
    {
        WebSocket = System.Net.WebSockets.WebSocket.CreateFromStream(Stream.Null, new WebSocketCreationOptions()),
        Connection = new Connection { Id = Guid.NewGuid(), Name = "c", Protocol = "rdp", HostId = Guid.NewGuid() },
        SessionId = "sess-1",
        User = new User { Id = Guid.NewGuid(), Name = "u", Email = "u@x", IsActive = true },
        IpAddress = "10.0.0.1",
        DbContext = null!, // not touched by ObserveClientToServerFileTransfer
        RedisDb = null!,   // not touched by ObserveClientToServerFileTransfer
        SettingsOverrides = null,
        FileTransferPolicy = policy,
    };

    [Fact]
    public void Disabled_NeverTracksPutEvenIfPresent()
    {
        var req = BuildRequest(FileTransferPolicy.Disabled);
        var payload = Payload(Instr("put", "0", "5", "1", "text/plain", "a.txt"));

        GuacamoleProxyService.ObserveClientToServerFileTransfer(req, payload);

        Assert.Empty(req.PendingUploads);
    }

    [Fact]
    public void Put_TracksNewUploadStreamByIndex()
    {
        var req = BuildRequest(FileTransferPolicy.Both);
        var payload = Payload(Instr("put", "0", "7", "text/plain", "a.txt"));

        GuacamoleProxyService.ObserveClientToServerFileTransfer(req, payload);

        var pending = Assert.Single(req.PendingUploads);
        Assert.Equal(7, pending.Key);
        Assert.Equal("a.txt", pending.Value.Name);
        Assert.Equal(0, pending.Value.Bytes);
    }

    [Fact]
    public void Blob_AccumulatesDecodedByteCountForTrackedStream()
    {
        var req = BuildRequest(FileTransferPolicy.Both);
        GuacamoleProxyService.ObserveClientToServerFileTransfer(
            req, Payload(Instr("put", "0", "3", "text/plain", "a.txt")));

        // "SGVsbG8=" decodes to "Hello" (5 bytes); "V29ybGQ=" decodes to "World" (5 bytes).
        GuacamoleProxyService.ObserveClientToServerFileTransfer(req, Payload(Instr("blob", "3", "SGVsbG8=")));
        GuacamoleProxyService.ObserveClientToServerFileTransfer(req, Payload(Instr("blob", "3", "V29ybGQ=")));

        Assert.Equal(10, req.PendingUploads[3].Bytes);
    }

    [Fact]
    public void Blob_ForUntrackedStream_IsIgnored()
    {
        // No `put` was ever observed for stream 9 — this is ordinary unrelated traffic
        // (e.g. a display-update tile), which must not create a phantom transfer.
        var req = BuildRequest(FileTransferPolicy.Both);

        GuacamoleProxyService.ObserveClientToServerFileTransfer(req, Payload(Instr("blob", "9", "SGVsbG8=")));

        Assert.Empty(req.PendingUploads);
    }

    [Fact]
    public void MultipleConcatenatedInstructions_AreAllObserved()
    {
        var req = BuildRequest(FileTransferPolicy.Both);
        var payload = Payload(
            Instr("sync", "123"),
            Instr("put", "0", "1", "text/plain", "notes.txt"),
            Instr("blob", "1", "SGVsbG8="),
            Instr("nop"));

        GuacamoleProxyService.ObserveClientToServerFileTransfer(req, payload);

        var pending = Assert.Single(req.PendingUploads);
        Assert.Equal("notes.txt", pending.Value.Name);
        Assert.Equal(5, pending.Value.Bytes);
    }

    [Fact]
    public void EmptyPayload_DoesNothing()
    {
        var req = BuildRequest(FileTransferPolicy.Both);
        GuacamoleProxyService.ObserveClientToServerFileTransfer(req, []);
        Assert.Empty(req.PendingUploads);
    }

    [Fact]
    public void End_ForTrackedUploadStream_RemovesFromPendingAndInvokesCallback()
    {
        var req = BuildRequest(FileTransferPolicy.Both);
        GuacamoleProxyService.ObserveClientToServerFileTransfer(
            req, Payload(Instr("put", "0", "4", "text/plain", "big.txt")));
        GuacamoleProxyService.ObserveClientToServerFileTransfer(req, Payload(Instr("blob", "4", "SGVsbG8=")));

        GuacamoleProxyService.PendingFileTransfer? completed = null;
        GuacamoleProxyService.ObserveClientToServerFileTransfer(
            req, Payload(Instr("end", "4")), transfer => completed = transfer);

        Assert.Empty(req.PendingUploads);
        Assert.NotNull(completed);
        Assert.Equal("big.txt", completed!.Name);
        Assert.Equal(5, completed.Bytes);
    }

    [Fact]
    public void End_ForUntrackedStream_NeverInvokesCallback()
    {
        // No `put` was ever observed for stream 9 — an `end` for ordinary display
        // traffic (or a download stream, tracked separately in PendingDownloads)
        // must never be mistaken for a completed upload.
        var req = BuildRequest(FileTransferPolicy.Both);
        var invoked = false;

        GuacamoleProxyService.ObserveClientToServerFileTransfer(
            req, Payload(Instr("end", "9")), _ => invoked = true);

        Assert.False(invoked);
    }

    [Fact]
    public void Disabled_NeverInvokesEndCallbackEvenIfPresent()
    {
        var req = BuildRequest(FileTransferPolicy.Disabled);
        req.PendingUploads[4] = new GuacamoleProxyService.PendingFileTransfer { Name = "a.txt" };
        var invoked = false;

        GuacamoleProxyService.ObserveClientToServerFileTransfer(
            req, Payload(Instr("end", "4")), _ => invoked = true);

        Assert.False(invoked);
    }
}
