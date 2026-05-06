using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.Connections.Queries
{
    public sealed record DownloadConnectionFileQuery(Guid ConnectionId, string? Format, ClaimsPrincipal User)
        : IRequest<ConnectionFileDto>;

    public sealed class DownloadConnectionFileQueryHandler : IRequestHandler<DownloadConnectionFileQuery, ConnectionFileDto>
    {
        private readonly IAppDbContext _context;
        private readonly IAccessControlService _access;

        public DownloadConnectionFileQueryHandler(IAppDbContext context, IAccessControlService access)
        {
            _context = context;
            _access = access;
        }

        public async Task<ConnectionFileDto> Handle(DownloadConnectionFileQuery request, CancellationToken cancellationToken)
        {
            var profile = await _access.GetCurrentProfileAsync(request.User);
            if (profile == null)
                throw new UnauthorizedException("User profile not found.");

            var connection = await _context.Connections
                .AsNoTracking()
                .Include(c => c.Host)
                .Include(c => c.Credential)
                .Include(c => c.Users)
                .FirstOrDefaultAsync(c => c.Id == request.ConnectionId, cancellationToken);

            if (connection == null)
                throw new NotFoundException("Connection not found.");
            if (!_access.CanUseConnection(profile, connection))
                throw new ForbiddenException("Access denied.");
            if (connection.Host == null)
                throw new BadRequestException("Connection has no host configured.");

            var protocol = (connection.Protocol ?? "rdp").ToLowerInvariant();
            var resolvedFormat = (request.Format ?? protocol).ToLowerInvariant();

            var settings = ProbeConnectionQueryHandler.ParseConnectionSettings(connection.Settings);
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
                _ => throw new BadRequestException($"Unsupported format '{resolvedFormat}'. Supported: rdp, ssh, vnc.")
            };
        }

        private static string StripNewlines(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\r", "").Replace("\n", "");
        }

        private static ConnectionFileDto GenerateRdpFile(string name, string host, int port, string username,
            Dictionary<string, string> settings)
        {
            settings.TryGetValue("domain", out var domain);

            name = StripNewlines(name);
            host = StripNewlines(host);
            username = StripNewlines(username);
            domain = StripNewlines(domain);

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

            return new ConnectionFileDto
            {
                Content = Encoding.UTF8.GetBytes(sb.ToString()),
                ContentType = "application/x-rdp",
                FileName = $"{SanitizeFileName(name)}.rdp"
            };
        }

        private static ConnectionFileDto GenerateSshFile(string name, string host, int port, string username)
        {
            name = StripNewlines(name);
            host = StripNewlines(host);
            username = StripNewlines(username);

            var sb = new StringBuilder();
            sb.AppendLine("#!/bin/bash");
            sb.AppendLine($"# Connection: {name}");
            sb.AppendLine("# Generated by Smooth Operator");
            sb.AppendLine();
            var safeHost = ShellEscape(host);
            var userAtHost = string.IsNullOrEmpty(username) ? safeHost : $"{ShellEscape(username)}@{safeHost}";
            sb.AppendLine($"ssh -p {port} {userAtHost}");

            return new ConnectionFileDto
            {
                Content = Encoding.UTF8.GetBytes(sb.ToString()),
                ContentType = "application/x-sh",
                FileName = $"{SanitizeFileName(name)}.sh"
            };
        }

        private static ConnectionFileDto GenerateVncFile(string name, string host, int port, string username)
        {
            name = StripNewlines(name);
            host = StripNewlines(host);
            username = StripNewlines(username);

            var sb = new StringBuilder();
            sb.AppendLine("[Connection]");
            sb.AppendLine($"Host={host}");
            sb.AppendLine($"Port={port}");
            if (!string.IsNullOrEmpty(username)) sb.AppendLine($"Username={username}");

            return new ConnectionFileDto
            {
                Content = Encoding.UTF8.GetBytes(sb.ToString()),
                ContentType = "application/x-vnc",
                FileName = $"{SanitizeFileName(name)}.vnc"
            };
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (var c in name)
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.Length > 0 ? sb.ToString() : "connection";
        }

        private static string ShellEscape(string value) =>
            "'" + value.Replace("'", "'\\''") + "'";
    }
}
