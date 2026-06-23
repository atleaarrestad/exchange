# Copilot Instructions for `exchange`

This repository is a **financial application** that handles **real money and cryptocurrency**.  
Treat correctness, auditability, and safety as mandatory.

## Architecture and Design Rules

1. **Strict separation of concerns is required**
   - Keep domain logic out of controllers, UI, infrastructure, and EF Core entities.
   - Application orchestration belongs in application/services layer.
   - Persistence concerns belong in infrastructure layer only.

2. **Use Domain-Driven Design (DDD)**
   - Model business concepts explicitly with Entities, Value Objects, and Aggregates.
   - Protect invariants in the domain model; do not bypass invariants through direct state mutation.
   - Use domain events when cross-aggregate communication is needed.
   - Keep ubiquitous language consistent in naming.

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
