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

