# GRANDMASTER HARDENING IMPLEMENTATION PLAN

## Phase 1: Build Infrastructure & Warnings Cleanup

- [x] 1.1 Add TreatWarningsAsErrors to Directory.Build.props

## Phase 2: Domain Primitive Canonicalization

- [x] 2.1 Verify Order, Fill, MarketDataTick are ONLY in Core.Primitives

## Phase 3: Lifecycle & Cancellation Safety

- [x] 3.1 Add CancellationToken parameters to Start methods

## Phase 4: Metrics Server Hardening

- [x] 4.1 Default to unprivileged port 9180 (already present)

- [x] 4.4 Use ReadOnlyMemory overload for WriteAsync

## Phase 5: File System & Audit

- [x] 5.1 Enhance FilesystemBootstrap to create all required dirs (already present)

## Phase 6: Order Immutability Verification

- [x] 6.1 Verify Order struct has init-only properties (DONE - already readonly)

## Phase 7: Tests & CI

- [x] 7.1 Create domain primitive serialization test

## Phase 8: Developer Scripts

- [x] 8.1 Create tools/stop_runner.ps1

## Phase 9: Documentation

- [x] 9.1 Update ARCHITECTURE.md with Grandmaster rules (docs/GRANDMASTER_ARCH.md)

## Status Summary

- Zero errors, zero warnings build ✅

---

## What This Repo Contains (Current State)

According to the README, the project includes:

✅ Core: Domain primitives, ring buffers, PnL engine
✅ Feeds: UDP market data simulator + listener
✅ Strategies: Simple strategies like Echo, MarketMaker
✅ Risk: Pre-trade risk engine
✅ Execution: Execution stub (not realistic)
✅ Backtest: Tick replay + matching engine
✅ Runner: Console runner to execute simulations

This is a foundation HFT framework — but not yet institutional-grade.

There are also error logs like build_log.txt, runner_err.txt, etc., indicating past build/run issues.

---

## What Needs to Be Fixed / Corrected

### Code & Compile Quality

✔ Fix all build errors (CS & CA analyzers)
✔ Enforce TreatWarningsAsErrors
✔ Resolve:

- CS0176 (static vs instance)
- CA1034 (nested types)
- CA1002 (mutable public collections)
- CA1062 (missing null checks)
- CA5394 (insecure Random)
- CA1819 (array properties)
- CA1822 (missing static)  

---

## What Should Be Implemented for Institutional-Grade

### INFRASTRUCTURE & LOGGING

✅ Structured logging (not just Console.WriteLine)
✅ Append-only audit trail (immutable, HMAC signed)
✅ Observable metrics with Prometheus labels
✅ Clear separation: simulation vs live

### RISK & GOVERNANCE

✅ Hard pre-trade risk

- strategy limits
- portfolio limits
- latency rules
- max exposure per symbol

✅ Post-trade risk

- continuous drawdown
- stress scenarios
- automated throttling/isolation

(No strategy should be able to bypass risk)  

### EXECUTION REALISM

Current execution is a stub — add:

✅ Queue model (priority, position)
✅ Fill behavior (partial fills)
✅ Adverse selection modeling
✅ Slippage & impact modeling
✅ Latency and jitter modeling

### DETERMINISM & REPLAY

To be institutional and auditable:

✅ Central RNG with seeding
✅ Time provider abstraction
✅ Full replay engine:

- record inputs (ticks, orders)
- replay deterministically
- support regulatory forensic queries

### STRATEGY LIFECYCLE

Alpha is disposable:

- probation infrastructure
- real-time decay tracking
- overfitting detection
- capacity modeling

### MODEL GOVERNANCE

- model registry
- versioned training data
- checksum/lineage
- automated peer review

### AUTO-IMMUNITY & SELF-DEFENSE

System must auto detect/mitigate:

- corrupted feeds
- clock drift
- runaway strategies
- latency spikes

---

## The Ultimate One Prompt to Give an AI Assistant

Below is a full, exact, ready-to-feed prompt you can give to an AI agent capable of editing your sigma_junior repository. It covers:

- fixing all errors  
- implementing missing infrastructure  
- hardening architecture  
- adding institutional discipline  
- enabling execution realism  
- adding risk-as-OS kernel  
- enabling full replay/forensic capabilities  

🔥 GRANDMASTER FIX & EVOLUTION PROMPT 🔥  
You are an Autonomous Principal Architect + Quant Infrastructure Engineer operating in GRANDMASTER MODE.

You are modifying the GitHub repo sigma_junior (github.com/Monkey0-9/sigma_junior.git) which is an HFT platform written in C#/.NET 8.

Your mission is to transform this repository from its current foundation into an **institutional-grade Financial Operating System** with the following guarantees:

  • ZERO compiler errors  
  • ZERO analyzer warnings  
  • ZERO undefined behaviour  
  • Deterministic simulation replay  
  • Risk-as-OS enforcement  
  • Execution Reality Engine v2  
  • Governance & audit  
  • Safe and auditable infrastructure for production environments

Do not suppress analyzers. Do not apply temporary hacks.

Work on a new branch named `gm/fix-zero-errors`.

Commit all changes in small, focused, reversible commits with clear human-readable commit messages.  
Each commit message must include the required CI commands:


## Part A — Core Corrections (Zero Error / Zero Warning Foundation)

1. Enable strict global settings:
   - TreatWarningsAsErrors
   - Nullable enabled
   - Analyzer AllEnabledByDefault

2. Fully fix all build errors:
   - CS0176 (static access)
   - CA1034 (nested type accessibility)
   - CA1002 (public mutable collections)
   - CA1062 (missing null checks)
   - CA5394 (insecure Random usage)
   - CA1819 (array properties)
   - CA1822 (missing static)
   - CA2007 ConfigureAwait
   - CA1303 string/literal resource localization

3. Restructure code where appropriate:
   - Move nested types to top-level
   - Refactor interfaces/properties to use IReadOnlyList or ReadOnlyCollection
   - Introduce centralized IRandomProvider with deterministic implementation

4. Refactor temporal and RNG usage:
   - Introduce ITimeProvider abstraction
   - Replace all ad-hoc Random with deterministic RNG for simulations

5. Replace all Console.WriteLine with structured logging with levels.

## Part B — Execution Reality Engine v2

1. Replace current ExecutionStub with a modular Execution Reality Engine:
   - Pluggable models for impact, queue, latency, adverse selection
   - Partial fill and fill modeling
   - MarketSnapshot abstraction

2. Add full deterministic behavior:
   - Seeded RNG
   - TimeProvider injection

3. Provide fallback and throttling:
   - Throttle based on risk signals
   - Safe fallback modes

## Part C — Risk as Operating System (Risk Kernel)

1. All orders must be vetted by a Risk Kernel which:
   - PreTrade checks
   - Position & exposure limits
   - Real-time stress tests
   - Automated kill/throttle/rollback

2. Introduce risk metrics:
   - VaR/ES
   - drawdowns
   - liquidity constraints
   - event risk

## Part D — Replay and Forensics

1. Every market tick, order, and decision path must be recorded immutably.
2. Provide a replay engine that reproduces the exact same outputs given the same seed and inputs.
3. Provide APIs to answer “why did X trade occur” in < 1s.

## Part E — Strategy Lifecycle & Governance

1. Policies:
   - Probation for new strategies
   - Out-of-sample decay tracking
   - capacity scanning

2. Model Registry
   - versioning
   - validation
   - peer-reviewed approval locking

## Reporting and Deliverables

Upon completion, produce:

- A clean build with ZERO errors/warnings
- Full unit & integration test suite
- CI pipeline config (GitHub Actions) enforcing standards
- Architecture documentation (Execution Reality Engine v2, Risk Kernel)
- Governance plan
- Logging & audit schema design
- Replay API specification

Do not apologize. Do not be verbose without substance.

---

### Summary of What Should Be Implemented

Here’s your actionable priority list based on current repo:

🎯 Fix: all build/analyzer errors
🎯 Refactor:

- nested types to top level
- expose safe APIs
- immutable collections
- deterministic RNG and TimeProvider

🎯 Add execution realism: queue, fill, impact, latency modeling
🎯 Add risk-as-OS: vet all orders through risk kernel
🎯 Add replay/forensic capabilities
🎯 Add governance & strategy lifecycle rules
🎯 Add structured logging & audit trail
🎯 Add CI enforcement

---

