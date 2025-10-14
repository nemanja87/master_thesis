using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;

namespace Gateway.Infrastructure;

internal sealed class HandshakeTracker
{
    private long _handshakeCount;
    private long _clientCertificateCount;
    private ILogger? _logger;

    public long HandshakeCount => Interlocked.Read(ref _handshakeCount);
    public long ClientCertificateCount => Interlocked.Read(ref _clientCertificateCount);

    public void Initialize(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger("Gateway.HandshakeTracker");
    }

    public void RecordHandshake(ConnectionContext connection, SslServerAuthenticationOptions sslOptions)
    {
        var count = Interlocked.Increment(ref _handshakeCount);
        _logger?.LogInformation(
            "TLS handshake #{Count} configured for {RemoteEndPoint}; ClientCertRequired={RequiresClientCertificate}; AllowedProtocols={Protocols}",
            count,
            connection.RemoteEndPoint,
            sslOptions.ClientCertificateRequired,
            sslOptions.EnabledSslProtocols);
    }

    public void RecordClientCertificate(X509Certificate2? certificate, SslPolicyErrors errors)
    {
        if (certificate is null)
        {
            return;
        }

        var clientCount = Interlocked.Increment(ref _clientCertificateCount);
        _logger?.LogInformation(
            "Validated client certificate #{ClientCount} subject {Subject}; policy errors {Errors}",
            clientCount,
            certificate.Subject,
            errors);
    }

    public void LogSummary()
    {
        var logger = _logger;
        if (logger is null)
        {
            return;
        }

        var totalHandshakes = HandshakeCount;
        var totalClientCertificates = ClientCertificateCount;

        logger.LogInformation(
            "Gateway shutting down after {Handshakes} TLS handshakes with {ClientCertificates} client certificates validated.",
            totalHandshakes,
            totalClientCertificates);
    }
}
