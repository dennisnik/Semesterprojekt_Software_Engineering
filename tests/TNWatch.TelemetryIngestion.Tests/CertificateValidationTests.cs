using System.Net.Security;
using TNWatch.TelemetryIngestion.Mqtt;

namespace TNWatch.TelemetryIngestion.Tests;

public class CertificateValidationTests
{
    [Fact]
    public void Valid_certificate_is_accepted()
        => Assert.True(MqttIngestionWorker.ValidateCertificate(SslPolicyErrors.None, null, tolerateUnknownRevocation: false));

    [Theory]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable)]
    [InlineData(SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch)]
    public void Hostname_and_missing_certificate_errors_are_always_rejected(SslPolicyErrors errors)
        => Assert.False(MqttIngestionWorker.ValidateCertificate(errors, null, tolerateUnknownRevocation: true));

    [Fact]
    public void Chain_errors_are_rejected_when_tolerance_is_off_or_chain_missing()
    {
        Assert.False(MqttIngestionWorker.ValidateCertificate(SslPolicyErrors.RemoteCertificateChainErrors, null, tolerateUnknownRevocation: false));
        Assert.False(MqttIngestionWorker.ValidateCertificate(SslPolicyErrors.RemoteCertificateChainErrors, null, tolerateUnknownRevocation: true));
    }
}
