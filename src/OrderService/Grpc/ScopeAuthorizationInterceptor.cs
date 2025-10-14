using Grpc.Core;
using Grpc.Core.Interceptors;
using OrderService.Security;
using Shared.Security;

namespace OrderService.Grpc;

public sealed class ScopeAuthorizationInterceptor : Interceptor
{
    private readonly bool _requireAuthentication;
    private readonly bool _enforceMethodPolicies;

    private static readonly IReadOnlyDictionary<string, MethodRequirement> Requirements =
        new Dictionary<string, MethodRequirement>(StringComparer.OrdinalIgnoreCase)
        {
            ["/shared.contracts.orders.OrderService/Create"] = new("orders.write", new[] { "Operator", "Administrator" }),
            ["/shared.contracts.orders.OrderService/Get"] = new("orders.read", Array.Empty<string>())
        };

    public ScopeAuthorizationInterceptor(SecurityProfile profile)
    {
        _requireAuthentication = profile.RequiresJwt();
        _enforceMethodPolicies = profile.RequiresPerMethodPolicies();
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;

        if (_requireAuthentication && user?.Identity?.IsAuthenticated != true)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required."));
        }

        if (_enforceMethodPolicies && Requirements.TryGetValue(context.Method, out var requirement))
        {
            if (!ScopeHelper.HasScope(user, requirement.Scope))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, $"Missing scope '{requirement.Scope}'."));
            }

            if (requirement.Roles.Count > 0 && !requirement.Roles.Any(user.IsInRole))
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Missing required role."));
            }
        }

        return await continuation(request, context);
    }

    private sealed record MethodRequirement(string Scope, IReadOnlyList<string> Roles);
}
