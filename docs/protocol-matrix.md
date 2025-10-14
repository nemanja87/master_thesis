# Protocol & Security Matrix

This matrix summarises which security features are activated for REST and gRPC endpoints under each security profile S0–S5. All changes are centrally driven by the `SEC_PROFILE` environment variable and enforced consistently across gateway and downstream services.

## Legend

- **HTTP**: Plain HTTP (no TLS).
- **HTTPS**: TLS enforced (server certificate).
- **mTLS**: Client certificates required and validated.
- **JWT**: Bearer token validation via OpenIddict & JwtBearer handlers.
- **RBAC/Policy**: Per-method scope/role enforcement (REST policies & gRPC interceptors).

## Matrix

| Profile | Gateway (REST) | Gateway (gRPC) | Downstream REST | Downstream gRPC | Notes |
|---------|----------------|----------------|-----------------|-----------------|-------|
| S0 | HTTP, anonymous | HTTP/2 cleartext, anonymous | HTTP, no auth | gRPC cleartext, no auth | Best-effort dev harness; use only for baseline throughput. |
| S1 | HTTPS | HTTPS | HTTPS | HTTPS | TLS only; no token validation or client certs. |
| S2 | HTTPS + JWT | HTTPS + JWT | HTTPS + JWT | HTTPS + JWT | Default secure posture: bearer tokens required, no mTLS. |
| S3 | HTTPS + mTLS | HTTPS + mTLS | HTTPS + mTLS | HTTPS + mTLS | Certificate-based identity; tokens optional/ignored. |
| S4 | HTTPS + mTLS + JWT | HTTPS + mTLS + JWT | HTTPS + mTLS + JWT | HTTPS + mTLS + JWT | Dual-auth: client cert plus token validation; policies still disabled. |
| S5 | HTTPS + JWT + RBAC | HTTPS + JWT + RBAC | HTTPS + JWT + REST policies | HTTPS + JWT + RBAC interceptor | Highest assurance: tokens validated and per-operation scope/role enforcement added. mTLS optional; enable via env var to combine. |

### REST Behaviour
- When JWT required (S2, S4, S5), controllers/minimal APIs apply authorization middleware; S5 adds explicit policies (scope checks).
- When mTLS enabled (S3, S4, optional S5), services reject requests lacking valid client certificates.
- For S0–S1, no authentication/authorization middleware runs; gateway forwards all requests.

### gRPC Behaviour
- S5: gRPC interceptors require authenticated principal + scope/role per method.
- S2/S4: gRPC requires authentication but skips per-method scope enforcement.
- S0/S1/S3: Interceptors are bypassed; calls are treated as anonymous (though S3 enforces mTLS at transport).

## Configuration Notes

- `SEC_PROFILE` defaults to `S2` if unset.
- To augment TLS cert paths, set `Security:CertificatePath`, `Security:CertificatePassword` and `Security:Mtls=true` where needed.
- Additional environment flags (e.g., `AuthServer__Authority`) remain required for JWT flows.
