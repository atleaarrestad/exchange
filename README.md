# exchange
Blockchain exchange

## Crypto Transactions bounded context

The crypto transfer workflow is isolated into dedicated projects:

- `Exchange.CryptoTransactions.Domain` for transfer invariants and value objects.
- `Exchange.CryptoTransactions.Application` for transfer orchestration contracts/services.
- `Exchange.CryptoTransactions.Infrastructure` for real gateway registration.
- `Exchange.CryptoTransactions.Infrastructure.Simulation` for simulation adapters.

Simulation mode is enabled through configuration (`Simulation:Enabled`) and swaps the blockchain gateway with the simulation adapter while keeping core transfer logic unchanged.

Validation is centralized in the application command validator. Domain invariants remain enforced in value objects/aggregates, and API error responses are normalized as `ProblemDetails`.

Idempotency receipts are persisted in SQLite (`CryptoTransactions:Idempotency:SqliteConnectionString`) so repeat requests with the same `(sourceAccountId, assetSymbol, idempotencyKey)` return the original receipt across process restarts and across concurrent API instances.
