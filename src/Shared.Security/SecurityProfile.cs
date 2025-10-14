using System.Globalization;

namespace Shared.Security;

public enum SecurityProfile
{
    S0,
    S1,
    S2,
    S3,
    S4,
    S5
}

public static class SecurityProfileDefaults
{
    private const string ProfileEnvironmentVariable = "SEC_PROFILE";

    public static SecurityProfile ResolveCurrentProfile()
    {
        var raw = Environment.GetEnvironmentVariable(ProfileEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return SecurityProfile.S2;
        }

        if (Enum.TryParse(raw.Trim(), ignoreCase: true, out SecurityProfile profile))
        {
            return profile;
        }

        if (raw.StartsWith("S", true, CultureInfo.InvariantCulture) &&
            int.TryParse(raw.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) &&
            numeric is >= 0 and <= 5)
        {
            return (SecurityProfile)numeric;
        }

        return SecurityProfile.S2;
    }

    public static bool RequiresHttps(this SecurityProfile profile) => profile != SecurityProfile.S0;

    public static bool RequiresJwt(this SecurityProfile profile) =>
        profile is SecurityProfile.S2 or SecurityProfile.S4 or SecurityProfile.S5;

    public static bool RequiresMtls(this SecurityProfile profile) =>
        profile is SecurityProfile.S3 or SecurityProfile.S4;

    public static bool RequiresPerMethodPolicies(this SecurityProfile profile) =>
        profile == SecurityProfile.S5;
}
