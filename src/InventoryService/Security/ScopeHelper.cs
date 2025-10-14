using System;
using System.Linq;
using System.Security.Claims;

namespace InventoryService.Security;

public static class ScopeHelper
{
    public static bool HasScope(ClaimsPrincipal user, string scope)
    {
        var scopes = user.FindAll("scope").Select(c => c.Value)
            .Concat(user.FindAll("scp").Select(c => c.Value))
            .SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);
    }
}
