# Threat Model

## Assets

- **Access Tokens**: JWT bearer tokens issued by AuthServer; enable API access under S2, S4, S5.
- **Client Certificates**: Used for mTLS (S3/S4/S5). Provide mutual authentication.
- **Order/Inventory Data**: Stored in-memory; inventory stock counts and order metadata.
- **Results Data**: Persisted in PostgreSQL; includes benchmark configurations and metrics.
- **Prometheus/Grafana Telemetry**: Monitoring data reflecting system performance.
- **BenchRunner Config & Scripts**: Temporary files containing benchmark payloads and tokens.

## Actors

- **Bench Operator**: Authorized user triggering benchmarks via UI or CLI.
- **Service Account Clients**: `bench-runner`, `service-order`, etc., using client credentials.
- **Malicious External User**: Attempts to call gateway endpoints without proper auth.
- **Compromised Internal Service**: Injected code or stolen certs attempting lateral movement.
- **Rogue Prometheus/Grafana User**: Attempts to glean secrets from metrics.

## Trust Boundaries

1. Internet → Gateway: TLS termination; optional mTLS and JWT gating.
2. Gateway → Downstream Services: Internal network; still requires TLS/mTLS/JWT depending on profile.
3. Services → Postgres: Credentials in environment; enforce network segmentation.
4. BenchRunner → ResultsService: Trusted client but must supply valid tokens under S2+.

## Threat Scenarios & Mitigations

| Threat | Description | Mitigation | Verification |
|--------|-------------|------------|--------------|
| Token replay | Stolen JWT used to call gateway | Short-lived tokens, HTTPS enforcement (S1+), optional mTLS | Automated security tests (expired token, wrong audience) |
| Missing scope access | Client missing required scope attempts privileged action | Policies/interceptors in S5 enforce scope; earlier profiles allow only authentication gating | `SecurityTests` to assert 403/permission denied |
| MITM on transport | Traffic interception between client and gateway | HTTPS enforced S1+, mTLS S3+; default dev profiles warn for S0 | Compose run with S2+ |
| Rogue internal caller | Service without proper cert or token hits downstream | mTLS & JWT combos in S4/S5, authorization policies | Integration tests (TODO) |
| Database exposure | Direct Postgres access by attacker | Compose network isolation; credentials stored separately; encryption-at-rest out of scope | Manual review |
| No-auth fallback misuse | S0 profile used accidentally in production | `SEC_PROFILE` default S2; documentation; pipeline guard rails recommended | Deployment checklist |
| JWT `alg=none` abuse | Crafted unsigned token | Security tests ensure handler rejects `alg=none` (`SecurityTests/Tokens/JwtValidationTests`) | Automated |

## Security Testing Summary

- **Unit Tests**: `ScopeHelperTests`, `JwtValidationTests`, `JwtNegativeTests` ensure claims parsing & token validation.
- **Security Tests (todo)**: Expand to hit live endpoints once integration harness is complete.
- **Manual Pen Tests**: Validate that gateway correctly enforces cert/token requirements under each profile before publishing findings.

## Future Improvements

- Automate profile-specific integration tests (use Testcontainers to spin up stack with SEC_PROFILE permutations).
- Add intrusion detection/alerting into Grafana (e.g., unauthorized attempts panel).
- Rotate tokens/certs automatically and document in runbook.
