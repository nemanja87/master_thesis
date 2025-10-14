using Grpc.Core;
using Grpc.Core.Interceptors;
using InventoryService.Security;
using Shared.Security;

namespace InventoryService.Grpc;

public sealed class ScopeAuthorizationInterceptor : Interceptor
{
    private readonly bool _requireAuthentication;
    private readonly bool _enforceScopes;
    private const string RequiredScope = "inventory.write";

    public ScopeAuthorizationInterceptor(SecurityProfile profile)
    {
        _requireAuthentication = profile.RequiresJwt();
        _enforceScopes = profile.RequiresPerMethodPolicies();
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

        if (_enforceScopes && !ScopeHelper.HasScope(user, RequiredScope))
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Missing required scope 'inventory.write'."));
        }

        return await continuation(request, context);
    }
}
