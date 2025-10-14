using Xunit;

namespace SecurityTests.ClientCertificates;

public sealed class MtlsEnforcementTests
{
    [Fact(Skip = "Requires mTLS-enabled gateway environment")]
    public void MissingClientCertificate_IsRejectedByGateway()
    {
        Assert.True(true, "Placeholder for infrastructure-dependent test");
    }
}
