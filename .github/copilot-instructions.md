# Copilot Instructions for `exchange`

This repository is a **financial application** that handles **real money and cryptocurrency**.  
Treat correctness, auditability, and safety as mandatory.

## Business Model Boundary

1. This platform is a **broker model** (similar to Firi), not a matching-engine exchange.
2. Customers buy/sell against platform-provided quotes and internal inventory.
3. Do not design or introduce central limit order book, maker/taker matching, or matching-engine workflows unless explicitly requested.
4. External venue integrations are for hedging/rebalancing liquidity and risk, not customer-to-customer order matching.

## Quote and Hedge Execution Behavior

1. Quotes are for customer expectation and must be accurate at quote-time based on approved pricing feeds.
2. If internal platform inventory/capital can cover the buy, assign ownership from internal inventory and do not execute an external buy for that customer.
3. If internal inventory is insufficient, do not execute one external buy per customer; buffer external hedge demand and execute aggregated external hedge orders.
4. Aggregated external hedge execution must be triggered by configurable limits:
   - maximum buffer time
   - maximum number of buffered customer buys
5. Aggregated hedges execute at a new market price at execution-time; quoted customer price can be stale and this is acceptable within configured protection rules.
6. Enforce configurable maximum slippage protection. Reject customer execution when observed slippage versus quote exceeds configured limits.
7. Customer execution and external hedge execution are decoupled: customer execution uses the accepted quote (subject to slippage protection), while the later aggregated hedge uses then-current market execution price.
8. Hedge batch execution must not be a synchronous dependency for customer execution; customer ownership assignment should complete first, and hedge execution/retry is handled asynchronously.

## Architecture and Design Rules

1. **Strict separation of concerns is required**
   - Keep domain logic out of controllers, UI, infrastructure, and EF Core entities.
   - Application orchestration belongs in application/services layer.
   - Persistence concerns belong in infrastructure layer only.
   - Controllers must stay thin: map HTTP to application commands/queries and return responses only.
   - Do not define request/response DTO records or validation logic inside controller files; place API contracts in dedicated contract files and validation in application validators.

2. **Use Domain-Driven Design (DDD)**
   - Model business concepts explicitly with Entities, Value Objects, and Aggregates.
   - Protect invariants in the domain model; do not bypass invariants through direct state mutation.
   - Use domain events when cross-aggregate communication is needed.
   - Keep ubiquitous language consistent in naming.
   - Keep core crypto trading settings separate from external gateway/provider settings. Gateway configuration must be modeled as provider-extensible modules, not Kraken-specific fields embedded in core settings.

3. **Apply SOLID principles**
   - Small, focused classes with one responsibility.
   - Depend on abstractions; avoid tight coupling between layers.
   - Favor composition and explicit interfaces for boundaries.

## Data and Persistence

1. **EF Core is the persistence technology**.
2. **SQLite is the current database for now**.
3. **PostgreSQL is the production target in the future**.
4. Write EF Core configurations with provider portability in mind:
   - Avoid provider-specific SQL unless absolutely necessary.
   - Keep migrations and type mappings compatible with future PostgreSQL move.
5. Simulation/staging environments should run with a real database and real migrations.

## Simulation and Load Testing Mode

1. The system must support a **Simulation mode** via dependency injection.
2. In Simulation mode, keep the internal core real:
   - domain logic
   - risk/rebalancing logic
   - ledger/accounting flows
   - queues/retry behavior
   - persistence
3. Only replace external edges with simulation adapters:
   - exchange clients
   - bank/payment clients
   - wallet/blockchain clients
   - market data feeds (replay/synthetic)
4. Simulation adapters must support high-traffic and failure scenarios:
   - bursts and sustained load
   - latency spikes/timeouts
   - partial fills/slippage
   - rejected/cancelled orders
   - liquidity drops
5. Core financial invariants must be continuously verifiable during simulation:
   - no negative balances
   - no double-spend
   - reserve thresholds respected
   - ledger reconciliation is consistent

## Simulation Project Structure

1. Prefer a **dedicated simulation project per bounded context** (for example, `*.Infrastructure.Simulation`).
2. Real and simulation implementations must share the same application-facing interfaces/contracts.
3. Keep simulation logic out of domain and application core.
4. For very small integrations, colocating real + simulation implementations in the same infrastructure project is acceptable temporarily, but default to dedicated simulation projects as the codebase grows.

## Scalability Strategy

1. Start as a **modular monolith** (single Web API codebase) with strict bounded-context boundaries.
2. The Web API must be stateless so multiple instances can be scaled horizontally behind a load balancer.
3. Prefer asynchronous workflows for non-immediate operations, but avoid premature distributed complexity.
4. Design for messaging from day one without requiring a broker on day one:
   - define domain/integration events at boundaries
   - enforce idempotency keys for externally triggered operations
   - use retry policies and dead-letter style handling patterns
   - implement the Outbox pattern for reliable event publication
5. Prioritize financial correctness under concurrency before throughput:
   - deterministic transaction workflows
   - strict ledger consistency and reconciliation
   - exactly-once effect semantics via idempotent processing
6. Introduce external messaging infrastructure (for example MassTransit + broker) only when load/SLA/operational evidence justifies it, then migrate high-volume paths first (order execution, market ingestion, settlement).

## MassTransit Adoption Guidance

1. It is acceptable to adopt **MassTransit now** to reduce future transition cost.
2. In local/dev/simulation environments, prefer **in-memory transport** for fast setup.
3. Keep message contracts, consumers, idempotency behavior, retry policies, and error handling production-oriented even when transport is in-memory.
4. Treat in-memory transport as non-durable and single-process:
   - no durability guarantees
   - no true cross-instance distribution
   - behavior may differ from real broker failure modes
5. Keep transport configuration environment-driven so production can switch to a real broker (for example RabbitMQ or Azure Service Bus) without rewriting domain/application logic.

## Resilience Policy Guidance (Polly)

1. MassTransit and Polly serve different concerns and can be used together.
2. Use MassTransit middleware/retry/redelivery for message consumer flows.
3. Use Polly for outbound dependency calls outside MassTransit consumer pipelines (for example exchange APIs, blockchain RPC, bank/payment endpoints).
4. Prefer fail-safe financial defaults:
   - timeouts and circuit breaking are encouraged
   - retries are only allowed when the remote operation is idempotent and duplicate-safe
5. Keep Polly policies behind infrastructure/application boundaries; domain models must stay policy-agnostic.

## Caching Strategy and Invalidation

1. Keep caching behind infrastructure abstractions (no direct cache vendor usage from domain/application logic).
2. Current default is **L1 in-process memory cache**; design must allow adding **L2 Valkey distributed cache** later.
3. Use strongly typed constants/builders for cache scopes and keys; avoid magic strings.
4. Use cache-aside patterns with bounded TTLs (and optional jitter to avoid thundering herd behavior).
5. Prefer **versioned cache keys** for entity/aggregate reads.
6. Invalidate primarily through domain/integration events:
   - publish change events at write boundaries
   - invalidate in handlers (in-process now, message-bus-driven later)
7. Invalidation operations must be idempotent and retry-safe.
8. Do not use cache as source of truth for critical mutable financial state (balances/ledger correctness remains authoritative in persistence/domain logic).

## Frontend Client Boundary and Shared Libraries

1. Keep user and administration frontends as **separate deployable Angular clients**.
2. Include each frontend in Visual Studio via separate `.esproj` entries, while keeping Node tooling as the source of truth for frontend build/test commands.
3. Share only stable cross-client logic in workspace-level libraries (for example `libs/api-client`, `libs/contracts`, `libs/auth-core`, `libs/finance-core`, `libs/app-errors`).
4. Favor sharing **headless logic** over UI. Keep most screens/features/components app-specific unless they are truly generic.
5. Enforce one-way boundaries:
   - shared libraries must not import from app projects
   - expose library public APIs through `index.ts` (no deep imports)
   - use typed aliases and lint boundary rules to prevent coupling
6. Keep financial rules and amount handling in shared typed libraries so both clients apply the same validated behavior.

## Frontend API Communication Layer

1. Keep server communication in a shared frontend API client layer so administration and user clients reuse the same communication behavior.
2. Centralize request concerns in the shared API layer (timeouts, retries, error mapping, correlation IDs, and transport defaults) instead of duplicating per feature.
3. Keep logging extensible through pluggable hooks/providers at the API boundary so request/response logging and observability can be added or expanded later without refactoring feature code.
4. Keep authentication optional in local/testing mode for now, but design the shared API client to be auth-ready via injectable token providers/hooks so auth can be enabled later with minimal changes.
5. Feature services should depend on the shared API abstraction rather than direct raw HTTP calls to keep upgrade paths (auth, logging, tracing) consistent across clients.

## Financial Safety Requirements

1. Never use floating-point types (`float`, `double`) for money/amount calculations; use `decimal`.
2. All monetary and crypto operations must be explicit, validated, and deterministic.
3. Avoid hidden side effects in balance/ledger updates.
4. Prefer immutable value objects for money, currency, and amounts.
5. Do not swallow exceptions in financial flows; errors must be explicit and traceable.

## Code Change Expectations

1. Preserve backward compatibility unless explicitly asked to change behavior.
2. Add or update tests for business-critical logic, especially:
   - balance changes
   - fee calculations
   - transfer settlement
   - rounding behavior
3. Keep changes small, reviewable, and aligned to the domain model.
