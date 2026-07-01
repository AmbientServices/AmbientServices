# Module Descriptions — The 3P Protocol (3P)

A reusable methodology for describing units of code at a higher linguistic altitude than the code itself. It is the shared language a developer and an AI assistant use to agree on what each unit is for, how it behaves, and how it's built — and the means by which they decide what to change and where. It is also how an AI assistant re-acquires context about a unit quickly across sessions.

This document is project- and language-agnostic. A companion file supplies the language-specific placement convention (where the layers physically live in source) and concrete examples; that companion references this file. (By convention, the project-specific companion is `docs/MODULE_DESCRIPTIONS.<project-name>.md`.)

We use **unit** to mean the thing being described, at any level of abstraction: a class, a module, a subsystem, or a whole system. Each unit carries up to three named prose layers — the **3P**:

- **Pitch** *(Value Proposition)* — why you'd use it.
- **Pledge** *(Contract)* — what it promises and how you interact with it.
- **Plan** *(Implementation)* — how it's built.

With the formal interface, the code, the tests, and the call sites, the three layers are independent expressions of the same intent; that redundancy is what lets collaborators coordinate, choose tools, and find bugs.

## Why these exist

- **Triage.** Read the **Pitch** to decide, problem-to-problem, whether a unit is relevant — usually without reading code.
- **Triangulation.** A defect shows up as a *disagreement* between two independent expressions of intent. Code that agrees with a wrong test hides the bug; prose layers at different altitudes add witnesses, so contradictions surface.
- **Agree first.** When a Pitch, Pledge, or Plan changes, collaborators agree on the prose change first, then write code and tests to match. Cheap to disagree in prose; expensive in code.
- **Fix / enhance / branch.** The layers' constraints tell us whether a change is a bug fix, an in-scope enhancement, or grounds for a new unit (below).

## The three layers

The layers form a **constraint cascade** — each removes degrees of freedom the previous left open. Sharing is **per-layer and independent**, not a strict tree: two units can share a Pledge yet have different Pitches, or share a Pitch with unrelated Pledges.  The Plan is generally unique because it's much longer and more descriptive than the other two.

Each layer attaches independently to an **abstraction** (e.g., an interface) or to a **concrete realization** (e.g., a class):

- **Pitch** — attaches to both; an abstraction has a general one, each realization has its own, which may differ from its siblings'.
- **Pledge** — attaches to the abstraction; realizations share it and link to it.
- **Plan** — attaches to each concrete realization; always differs between siblings.

### Pitch — *short; for the caller's decision*

The problem the unit solves, or the benefit it gives the caller, written so a reader can quickly decide **"is this what I need?"** Naturally **much shorter** than the other two layers. It *may* state limits — what it doesn't try to do — but only insofar as that aids the decision; at a high enough altitude it may state none. Its constraints may flow down into the Pledge and definitely flow down to the Plan, but their **purpose here is the caller's decision, not the specification of behavior** (that is the Pledge's job). It deliberately under-determines the unit: many units can share one.

Two realizations that share one Pledge may still have **different** Pitches. Consider an interface for a key-value store with two implementations: one in-memory (fast and local, but lost on restart) and one backed by cloud storage (durable and shared). They obey the identical Pledge, yet one's Pitch sells speed without persistence and the other sells shared durability. A realization's Pitch is the caller-facing distillation of its Plan's trade-offs — it states, briefly, which trade-off profile you get, without getting into the implementation details, so you can choose among siblings.

### Pledge — *the behavioral protocol*

High-level interaction rules that cannot be explicitly expressed in the method signatures: how data flows through the interface, which call sequences are expected and which are never valid, ordering/protocol/state constraints, behavioral guarantees. It adds constraints to the paired Pitch in the same way the formal interface does, but without naming methods, parameters, or types. Usually **larger** than the Pitch. The formal interface (the signatures) is its concrete, machine-checked tail.

A unit may have multiple pledges, but will usually only have one, with possible implementation-specific extensions to that.

A unit may link to a pledge defined by another unit, as it would when it implements an abstraction. In that case, the unit promises to fulfill the terms of the linked pledge, and the content of that pledge applies to this unit as well. When a unit both links a pledge and adds its own, its full set of promises is the linked pledge together with those extensions.

### Plan — *the concrete realization and its trade-offs*

The per-realization layer, and the one that always differs between siblings. High-level algorithms; the building blocks, tools, and backend services it is built on, named explicitly (so a functionally-equivalent rewrite knows what to reach for); the trade-offs struck across performance, durability, reliability, and cost; what that profile implies for which use cases; and how this last level of constraints achieves it. **Not** a line-by-line narration — kept at the altitude of strategy and consequences so it resists drift. Its performance implications feed back into triage.

## What belongs in each layer

A few recurring kinds of content have natural homes; put them there rather than inventing new descriptions:

- **Invariants.** Conditions that always hold. Caller-facing invariants ("reads always return the latest write") belong in the **Pledge**; internal-consistency invariants ("a node is never simultaneously locked for both splitting and merging") belong in the **Plan**. State them explicitly — they are the most bug-dense, checkable lines, and the easiest place for code and prose to disagree.
- **Failure modes and non-goals.** What the unit refuses to do belongs in the **Pitch**'s limits; how it behaves under error or contention belongs in the **Pledge**; how it degrades under certain types of load belongs in the **Plan**.
- **Canonical usage.** Depending on the language conventions, optionally point to a representative test, sometimes *by name* rather than inlining example code. Keep it prose — don't formally link from the code into its test project, since tests depend on the code, not the other way around. Treat the pointer as an optional, judgment-call aid: it is an ongoing maintenance chore, so skip it where the Pledge already makes usage clear. Where the investment is justified.  At the project-level, consider build-time sample extraction (e.g., https://github.com/jamesivie/dotnet-markdown-sample-code) can pull build-verified samples from real code or tests outside the implementation into the rendered project-level documentation.

## Regeneration as a completeness test

The layers double as a generative specification, which lets us *test* their completeness without storing anything extra. The target is a *functionally equivalent* unit — not an identical copy:

- A complete **Pitch + Pledge** should be enough to regenerate an implementation that passes the unit's tests, even if its design differs.
- Adding the **Plan** should narrow that to an implementation that uses the *same building blocks and dependencies* and has *similar performance characteristics* — still not line-for-line, but functionally and structurally equivalent.

Functional equivalence is not *compatibility*. A regenerated unit can pass the tests yet be unable to read data the original wrote, or interoperate with it, because compatibility usually hinges on details below the Plan's altitude — exact serialization formats, on-disk or path layouts, key encodings, wire protocols. The 3P deliberately leave those unspecified, so regeneration validates behavior, not format compatibility. Where compatibility actually matters, those formats must be pinned down explicitly (in the Plan or a dedicated format spec), at the cost of the Plan moving toward full determination.

Use this as an on-demand check, not a stored description: hand the layers (plus the dependency signatures) to a fresh generator, have it reimplement the unit, and run the tests. Gaps reveal what a layer is missing; and if a regenerated version passes the tests but is wrong, the tests are underspecified. Because the goal is functional equivalence with the same dependencies, the Plan must name the building blocks, tools, and backend services explicitly (see above).

## Where they live in the code

Each project defines a placement convention appropriate to its language and documentation system, recorded in that project's companion module-descriptions file. Whatever the mechanism, an **abstraction** carries a Pitch and a Pledge; a **concrete realization** carries a Pitch, one or more Pledges, and a Plan — a realization's Pledge is usually a reference to the abstraction's Pledge, plus any realization-specific extensions.

## Glossary

A project that adopts this model keeps a single cross-cutting **glossary** of its invented vocabulary — the names of its core abstractions, data structures, and techniques. The glossary is orthogonal to the three per-unit layers: those describe one unit each, while the glossary defines the terms every unit's prose relies on. Its value is disproportionate and its upkeep is nearly free — defining a term once lets every Pitch, Pledge, and Plan reference it tersely instead of re-explaining it, and it gives every collaborator, human or AI, one authoritative meaning per term. Add an entry when a term is coined; entries change far more slowly than code.

Standard location: `docs/GLOSSARY.md` at the project root.

## Using them: fix, enhance, or branch

The constraints at **every layer** decide the move:

- **Fix** — the code disagrees with the Pitch, Pledge, or Plan, or the layers disagree with each other. A bug; reconcile them.
- **Enhance** — the change fits within the Pitch, the Pledge, *and* the Plan's trade-offs. Extend in place.
- **Branch** — the change falls outside the Pitch's scope, against the Pledge, or is wrong for this Plan's trade-offs. Build a new unit — or, by considered agreement, change the relevant layer first.

Because the boundaries are written down, crossing one is **visible**: it forces the "widen this, or build new?" conversation at the moment it should happen.

## Maintaining them

- A significant code change updates the affected Pitch, Pledge, or Plan **in the same change**.
- A change to a Pitch, Pledge, or Plan is **agreed in prose first**, then code and tests follow.
- Keep the Plan at strategy/building-blocks/trade-off altitude — never narration.
- Resolve ambiguities (in the descriptions or the code) thoughtfully as encountered, and write the resolution back.
- **The assistant's standing duty:** before modifying a unit, read its three layers (Pitch, Pledge, Plan) and flag any drift between them and the code.

## AI-oriented notes (optional)

For complex units, a sidecar `*.ai.md` beside the code can speed an AI assistant's re-acquisition — content tuned for an LLM rather than a human reader: the minimal mental model, precise invariants, gotchas, source pointers to the subtle parts, cross-links to related units, known failure modes, and which tests prove what. A shared section serves any LLM; an assistant-specific section (e.g., `## Claude`) holds notes for one assistant. May use LLM-specific language as long as they don't break the ability to parse one LLMs data from another.  These stay out of the published documentation to keep it clean. Keeping them current whenever the unit is modified is part of the same maintenance duty. Start only where re-acquisition is genuinely slow.

## This document is itself layered

Per the same principle: the project's agent-instructions file (e.g., `CLAUDE.md`) is the shortest layer — the operative rules; this file is the full definition; the project companion adds language- and project-specific detail.
