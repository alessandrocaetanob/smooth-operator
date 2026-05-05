using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ConnectionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _audit;
        private readonly IAccessControlService _access;

        /// <summary>Maximum number of concurrent TCP probes issued by probe-bulk.</summary>
        private const int BulkProbeMaxConcurrency = 10;

        public ConnectionsController(AppDbContext context, IAuditService audit, IAccessControlService access)
        {
            _context = context;
            _audit = audit;
            _access = access;
        }

        private static ConnectionDto Project(Connection c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Protocol = c.Protocol,
            HostId = c.HostId,
            CredentialId = c.CredentialId,
            ConnectionGroupId = c.ConnectionGroupId,
            Settings = c.Settings,
            Tags = c.Tags.Select(t => t.Tag).ToList(),
            Host = c.Host == null ? null : new HostDto
            {
                Id = c.Host.Id,
                Name = c.Host.Name,
                Address = c.Host.Address
            },
            ConnectionGroup = c.ConnectionGroup == null ? null : new ConnectionGroupDto
            {
                Id = c.ConnectionGroup.Id,
                Name = c.ConnectionGroup.Name
            }
        };

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ConnectionDto>>> GetConnections()
        {
            var profile = await _access.GetCurrentProfileAsync(User);
            if (profile == null) return Unauthorized();

            // c.Users is only used by ApplyConnectionScope's Any(...) check, which EF
            // translates to a SQL EXISTS — no need to eager-load the full collection.
            var scopedQuery = _access.ApplyConnectionScope(
                _context.Connections
                    .Include(c => c.Host)
                    .Include(c => c.ConnectionGroup)
                    .Include(c => c.Tags),
                profile);

            var connections = await scopedQuery
                .OrderBy(c => c.Name)
                .ToListAsync();

            return Ok(connections.Select(Project));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ConnectionDto>> GetConnection(Guid id)
        {
            var profile = await _access.GetCurrentProfileAsync(User);
            if (profile == null) return Unauthorized();

            var connection = await _access.ApplyConnectionScope(
                    _context.Connections
                        .Include(c => c.Host)
                        .Include(c => c.ConnectionGroup)
                        .Include(c => c.Tags),
                    profile)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (connection == null) return NotFound();
            return Ok(Project(connection));
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.OwnerAdminOrTeamAdmin)]
        public async Task<ActionResult<ConnectionDto>> CreateConnection([FromBody] CreateConnectionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var profile = await _access.GetCurrentProfileAsync(User);
            if (profile == null) return Unauthorized();

            if (!_access.CanManageConnectionsInVault(profile, dto.ConnectionGroupId))
            {
                return Forbid();
            }

            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Protocol = dto.Protocol,
                HostId = dto.HostId,
                CredentialId = dto.CredentialId,
                ConnectionGroupId = dto.ConnectionGroupId,
                Settings = dto.Settings,
                Tags = dto.Tags
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(t => new ConnectionTag { Tag = t.Trim().ToLowerInvariant() })
                    .ToList()
            };

            _context.Connections.Add(connection);
            await _context.SaveChangesAsync();
            await _audit.WriteAsync("connection.created", "Connection", connection.Id.ToString(),
                new { connection.Name, connection.Protocol, connection.HostId, connection.ConnectionGroupId });

            return CreatedAtAction(nameof(GetConnection), new { id = connection.Id }, new ConnectionDto
            {
                Id = connection.Id,
                Name = connection.Name,
                Protocol = connection.Protocol,
                HostId = connection.HostId,
                CredentialId = connection.CredentialId,
                ConnectionGroupId = connection.ConnectionGroupId,
                Settings = connection.Settings,
                Tags = connection.Tags.Select(t => t.Tag).ToList()
            });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = AppRoles.OwnerAdminOrTeamAdmin)]
        public async Task<IActionResult> UpdateConnection(Guid id, [FromBody] CreateConnectionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var profile = await _access.GetCurrentProfileAsync(User);
            if (profile == null) return Unauthorized();

            var connection = await _context.Connections
                .Include(c => c.Tags)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (connection == null)
            {
                return NotFound();
            }

            var canManageCurrentVault = _access.CanManageConnectionsInVault(profile, connection.ConnectionGroupId);
            var canManageTargetVault = _access.CanManageConnectionsInVault(profile, dto.ConnectionGroupId);
            if (!canManageCurrentVault || !canManageTargetVault)
            {
                return Forbid();
            }

            connection.Name = dto.Name;
            connection.Protocol = dto.Protocol;
            connection.HostId = dto.HostId;
            connection.CredentialId = dto.CredentialId;
            connection.ConnectionGroupId = dto.ConnectionGroupId;
            connection.Settings = dto.Settings;

            // Replace tags
            _context.RemoveRange(connection.Tags);
            connection.Tags = dto.Tags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(t => new ConnectionTag { Tag = t.Trim().ToLowerInvariant(), ConnectionId = id })
                .ToList();

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ConnectionExists(id))
                {
                    return NotFound();
                }

                throw;
            }

            await _audit.WriteAsync("connection.updated", "Connection", id.ToString(),
                new { connection.Name, connection.Protocol, connection.ConnectionGroupId });
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoles.OwnerAdminOrTeamAdmin)]
        public async Task<IActionResult> DeleteConnection(Guid id)
        {
            var profile = await _access.GetCurrentProfileAsync(User);
            if (profile == null) return Unauthorized();

            var connection = await _context.Connections.FindAsync(id);
            if (connection == null)
            {
                return NotFound();
            }

            if (!_access.CanManageConnectionsInVault(profile, connection.ConnectionGroupId))
            {
                return Forbid();
            }

            _context.Connections.Remove(connection);
            await _context.SaveChangesAsync();
            await _audit.WriteAsync("connection.deleted", "Connection", id.ToString(),
                new { connection.Name, connection.ConnectionGroupId });
            return NoContent();
        }

        private bool ConnectionExists(Guid id)
        {
            return _context.Connections.Any(e => e.Id == id);
        }

        // ---- Last-connected timestamp per connection (current user) -----------------

        [HttpGet("my-last-connected")]
        public async Task<IActionResult> GetMyLastConnected()
        {
            var profile = await _access.GetCurrentProfileAsync(User);
            if (profile == null) return Unauthorized();

            var results = await _context.AuditLogs
                .Where(a => a.UserId == profile.UserId && a.Action == "connection.started")
                .GroupBy(a => a.ResourceId)
                .Select(g => new { ConnectionId = g.Key, LastConnectedAt = g.Max(a => a.Timestamp) })
                .ToListAsync();

            return Ok(results);
        }

        // ---- Host reachability probe ------------------------------------------------

        [HttpGet("{id}/probe")]
        public async Task<IActionResult> ProbeConnection(Guid id)
        {
            var profile = await _access.GetCurrentProfileAsync(User);
            if (profile == null) return Unauthorized();

            var connection = await _context.Connections
                .Include(c => c.Host)
                .Include(c => c.Users)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (connection == null) return NotFound();
            if (!_access.CanUseConnection(profile, connection)) return Forbid();
            if (connection.Host == null) return BadRequest("Connection has no host.");

            var defaultPort = (connection.Protocol ?? "rdp").ToLowerInvariant() switch
            {
                "ssh" => 22,
                "vnc" => 5900,
                "telnet" => 23,
                _ => 3389
            };
            var settings = ParseConnectionSettings(connection.Settings);
            var port = int.TryParse(settings.GetValueOrDefault("port"), out var p) ? p : defaultPort;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var reachable = await TcpProbeAsync(connection.Host.Address, port, cts.Token);

            return Ok(new { reachable });
        }

        // ---- Batch Host reachability probe ------------------------------------------

        // Maximum simultaneous outbound TCP probe sockets per batch request.
        private const int ProbeBatchConcurrency = 20;

        [HttpPost("probe-batch")]
        public async Task<IActionResult> ProbeBatch([FromBody] List<Guid> ids)
        {
            var profile = await _access.GetCurrentProfileAsync(User);
            if (profile == null) return Unauthorized();

            // Apply access scope before materialising so the response shape does not
            // reveal whether an unauthorised connection ID exists in the database.
            var connections = await _access.ApplyConnectionScope(
                    _context.Connections
                        .Include(c => c.Host)
                        .Include(c => c.Users),
                    profile)
                .Where(c => ids.Contains(c.Id))
                .ToListAsync();

            // Cap simultaneous outbound TCP connects to avoid port exhaustion.
            using var semaphore = new SemaphoreSlim(ProbeBatchConcurrency);

            // Access control is already enforced by ApplyConnectionScope above;
            // per-connection CanUseConnection checks are therefore not needed here.
            var probeTasks = connections.Select(async connection =>
            {
                if (connection.Host == null)
                {
                    return new KeyValuePair<Guid, bool>(connection.Id, false);
                }

                var defaultPort = (connection.Protocol ?? "rdp").ToLowerInvariant() switch
                {
                    "ssh" => 22,
                    "vnc" => 5900,
                    "telnet" => 23,
                    _ => 3389
                };
                var settings = ParseConnectionSettings(connection.Settings);
                var port = int.TryParse(settings.GetValueOrDefault("port"), out var p) ? p : defaultPort;

                await semaphore.WaitAsync();
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    var reachable = await TcpProbeAsync(connection.Host.Address, port, cts.Token);
                    return new KeyValuePair<Guid, bool>(connection.Id, reachable);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(probeTasks);
            return Ok(results.ToDictionary(r => r.Key, r => r.Value));
        }

        private static async Task<bool> TcpProbeAsync(string host, int port, CancellationToken ct)
        {
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(host, port, ct);
                return true;
            }
            catch (Exception) { return false; }
        }

        [HttpPost("probe-bulk")]
        public async Task<IActionResult> ProbeConnectionsBulk([FromBody] List<Guid> ids)
        {
            var profile = await _access.GetCurrentProfileAsync(User);
            if (profile == null) return Unauthorized();

            if (ids == null || ids.Count == 0) return Ok(new Dictionary<Guid, string>());

            // 1. Find which requested IDs exist in the database.
            var existingConnections = await _context.Connections
                .Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, c.Protocol, c.Settings, c.HostId })
                .ToListAsync();

            var existingSet = existingConnections.Select(c => c.Id).ToHashSet();

            // 2. Apply scope filter at the DB level — avoids loading the Users collection
            // into memory; the scope query translates to a SQL EXISTS sub-select.
            var accessibleIds = await _access
                .ApplyConnectionScope(_context.Connections.Where(c => ids.Contains(c.Id)), profile)
                .Select(c => c.Id)
                .ToHashSetAsync();

            // 3. Load host addresses for accessible connections only (separate query avoids
            // an implicit INNER JOIN on Include that would filter out connections with a
            // missing host FK target).
            var accessibleConnections = existingConnections
                .Where(c => accessibleIds.Contains(c.Id))
                .ToList();

            var hostIds = accessibleConnections.Select(c => c.HostId).Distinct().ToList();
            var hostLookup = await _context.Hosts
                .Where(h => hostIds.Contains(h.Id))
                .Select(h => new { h.Id, h.Address })
                .ToDictionaryAsync(h => h.Id);

            var results = new System.Collections.Concurrent.ConcurrentDictionary<Guid, string>();

            // Build a O(1) lookup from existing connections so the classification loop below
            // doesn't perform O(n) linear scans per entry.
            var existingById = existingConnections.ToDictionary(c => c.Id);

            // Classify IDs that are not probe candidates up-front so the response is
            // semantically unambiguous (clients can distinguish 'host is down' from
            // 'this ID is invalid / inaccessible / misconfigured').
            foreach (var id in ids)
            {
                if (!existingSet.Contains(id))
                    results[id] = "not_found";
                else if (!accessibleIds.Contains(id))
                    results[id] = "forbidden";
                else if (!hostLookup.ContainsKey(existingById[id].HostId))
                    results[id] = "no_host";
            }

            // Probe accessible connections that have a host, with a concurrency cap to
            // prevent a large batch from exhausting socket / thread-pool resources.
            var probeCandidates = accessibleConnections
                .Where(c => hostLookup.ContainsKey(c.HostId))
                .ToList();

            using var semaphore = new SemaphoreSlim(BulkProbeMaxConcurrency, BulkProbeMaxConcurrency);

            var tasks = probeCandidates.Select(conn => Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var defaultPort = (conn.Protocol ?? "rdp").ToLowerInvariant() switch
                    {
                        "ssh" => 22,
                        "vnc" => 5900,
                        "telnet" => 23,
                        _ => 3389
                    };
                    var settings = ParseConnectionSettings(conn.Settings);
                    var port = int.TryParse(settings.GetValueOrDefault("port"), out var p) ? p : defaultPort;
                    var hostAddress = hostLookup[conn.HostId].Address;

                    using var probeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    var reachable = await TcpProbeAsync(hostAddress, port, probeCts.Token);
                    results[conn.Id] = reachable ? "up" : "down";
                }
                finally
                {
                    semaphore.Release();
                }
            })).ToList();

            await Task.WhenAll(tasks);

            return Ok(results);
        }

        // ---- Connection file download -----------------------------------------------

        // Generates a native client connection file (.rdp, .sh script, or .vnc).
        // No password is embedded — the user provides it in their native client.
        [HttpGet("{id}/file")]
        public async Task<IActionResult> DownloadConnectionFile(Guid id, [FromQuery] string? format)
        {
            var profile = await _access.GetCurrentProfileAsync(User);
            if (profile == null) return Unauthorized();

            var connection = await _context.Connections
                .Include(c => c.Host)
                .Include(c => c.Credential)
                .Include(c => c.Users)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (connection == null) return NotFound();
            if (!_access.CanUseConnection(profile, connection)) return Forbid();
            if (connection.Host == null) return BadRequest("Connection has no host configured.");

            var protocol = (connection.Protocol ?? "rdp").ToLowerInvariant();
            var resolvedFormat = (format ?? protocol).ToLowerInvariant();

            var settings = ParseConnectionSettings(connection.Settings);
            var defaultPort = protocol switch { "ssh" => 22, "vnc" => 5900, "telnet" => 23, _ => 3389 };
            var port = settings.TryGetValue("port", out var pStr) && int.TryParse(pStr, out var pNum) ? pNum : defaultPort;

            var host = connection.Host.Address;
            var username = connection.Credential?.Username ?? string.Empty;
            var name = connection.Name;

            return resolvedFormat switch
            {
                "rdp" => GenerateRdpFile(name, host, port, username, settings),
                "ssh" => GenerateSshFile(name, host, port, username),
                "vnc" => GenerateVncFile(name, host, port, username),
                _ => BadRequest($"Unsupported format '{resolvedFormat}'. Supported: rdp, ssh, vnc.")
            };
        }

        private FileContentResult GenerateRdpFile(string name, string host, int port, string username, Dictionary<string, string> settings)
        {
            settings.TryGetValue("domain", out var domain);
            var sb = new StringBuilder();
            sb.AppendLine("screen mode id:i:2");
            sb.AppendLine("use multimon:i:0");
            sb.AppendLine("desktopwidth:i:1920");
            sb.AppendLine("desktopheight:i:1080");
            sb.AppendLine("session bpp:i:32");
            sb.AppendLine("compression:i:1");
            sb.AppendLine("keyboardhook:i:2");
            sb.AppendLine("audiocapturemode:i:0");
            sb.AppendLine("videoplaybackmode:i:1");
            sb.AppendLine("connection type:i:7");
            sb.AppendLine("networkautodetect:i:1");
            sb.AppendLine("bandwidthautodetect:i:1");
            sb.AppendLine("displayconnectionbar:i:1");
            sb.AppendLine("enableworkspacereconnect:i:0");
            sb.AppendLine("redirectclipboard:i:1");
            sb.AppendLine("redirectprinters:i:1");
            sb.AppendLine("autoreconnection enabled:i:1");
            sb.AppendLine("authentication level:i:2");
            sb.AppendLine("prompt for credentials:i:0");
            sb.AppendLine("negotiate security layer:i:1");
            sb.AppendLine("remoteapplicationmode:i:0");
            sb.AppendLine($"full address:s:{host}:{port}");
            if (!string.IsNullOrEmpty(username)) sb.AppendLine($"username:s:{username}");
            if (!string.IsNullOrEmpty(domain)) sb.AppendLine($"domain:s:{domain}");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var safeName = SanitizeFileName(name);
            return File(bytes, "application/x-rdp", $"{safeName}.rdp");
        }

        private FileContentResult GenerateSshFile(string name, string host, int port, string username)
        {
            var sb = new StringBuilder();
            sb.AppendLine("#!/bin/bash");
            sb.AppendLine($"# Connection: {name}");
            sb.AppendLine($"# Generated by Smooth Operator");
            sb.AppendLine();
            var safeHost = ShellEscape(host);
            var userAtHost = string.IsNullOrEmpty(username) ? safeHost : $"{ShellEscape(username)}@{safeHost}";
            sb.AppendLine($"ssh -p {port} {userAtHost}");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var safeName = SanitizeFileName(name);
            return File(bytes, "application/x-sh", $"{safeName}.sh");
        }

        private FileContentResult GenerateVncFile(string name, string host, int port, string username)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Connection]");
            sb.AppendLine($"Host={host}");
            sb.AppendLine($"Port={port}");
            if (!string.IsNullOrEmpty(username)) sb.AppendLine($"Username={username}");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var safeName = SanitizeFileName(name);
            return File(bytes, "application/x-vnc", $"{safeName}.vnc");
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (var c in name)
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.Length > 0 ? sb.ToString() : "connection";
        }

        // Wraps a value in single quotes and escapes embedded single quotes for bash.
        private static string ShellEscape(string value) =>
            "'" + value.Replace("'", "'\\''") + "'";

        private static Dictionary<string, string> ParseConnectionSettings(string? settingsJson)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(settingsJson)) return dict;
            try
            {
                using var doc = JsonDocument.Parse(settingsJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return dict;
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                        JsonValueKind.Number => prop.Value.ToString(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => string.Empty
                    };
                }
            }
            catch { /* ignore malformed JSON */ }
            return dict;
        }
    }
}
