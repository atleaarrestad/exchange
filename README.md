# exchange
Blockchain exchange

## Frontend (Angular)

Frontend apps are included in Visual Studio through `.esproj` entries in `Exchange.slnx`:

- User client: `Frontend/broker-web` (`broker-web.esproj`)
- Admin client: `Frontend/broker-admin` (`broker-admin.esproj`)
- Shared libs root: `Frontend/libs`

Run from either frontend folder:

- Run locally: `npm run start`
- Build: `npm run build`
- Test: `npm run test`

## Crypto Transactions bounded context

The crypto transfer workflow is isolated into dedicated projects:

- `Exchange.CryptoTransactions.Domain` for transfer invariants and value objects.
- `Exchange.CryptoTransactions.Application` for transfer orchestration contracts/services.
- `Exchange.CryptoTransactions.Infrastructure` for real gateway registration.
- `Exchange.CryptoTransactions.Infrastructure.Simulation` for simulation adapters.

Simulation mode is enabled through configuration (`Simulation:Enabled`) and swaps the blockchain gateway with the simulation adapter while keeping core transfer logic unchanged.

Validation is centralized in the application command validator. Domain invariants remain enforced in value objects/aggregates, and API error responses are normalized as `ProblemDetails`.

Idempotency receipts are persisted in PostgreSQL (`CryptoTransactions:Idempotency:ConnectionString`) so repeat requests with the same `(sourceAccountId, assetSymbol, idempotencyKey)` return the original receipt across process restarts and across concurrent API instances.

Transfer submission now enforces a funds reservation boundary before the blockchain call. In simulation, an in-memory reservation gateway tracks per-account available balances and rejects transfers that would overdraw.

Outbound blockchain submission is wrapped with a Polly timeout policy to prevent unbounded external call duration while preserving idempotency-first handling of unknown outcomes.

Kraken can be enabled as a real blockchain transfer gateway via `CryptoTransactions:Gateways:Kraken` configuration. When enabled, the infrastructure layer uses Kraken private API signing and withdrawal/status endpoints for BTC/ETH while keeping the same application contracts.
