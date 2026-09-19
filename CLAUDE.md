# AmbientServices

## Module descriptions — The 5P Protocol (5P)

Each unit of code (class, module, subsystem, or system) carries up to five named prose layers — the **5P** — in its XML-doc `<remarks>`, using custom elements `<pitch>`, `<pledge>`, `<plan>`, `<pin>`, and `<priority>`. The library viewed as one whole-system unit carries its layers in dedicated project-level files instead: `docs/PITCH.md`, `docs/PLEDGE.md`, `docs/PLAN.md`, `docs/PRIORITY.md`, linked from `README.md`. Generic definition: `docs/MODULE_DESCRIPTIONS.md`; C# placement convention and examples: `docs/MODULE_DESCRIPTIONS.AmbientServices.md`. Refer to these by their exact names — Pitch, Pledge, Plan, Pin, Priority — never informal synonyms.

### The 5P Protocol (5P)
- **Pitch** *(Value Proposition)* — short; the caller's "is this what I need?" decision. Problem/benefit, optional limits.
- **Pledge** *(Contract)* — data flow, valid/invalid call sequences, and behavioral rules the signatures can't express. Attaches to the abstraction; realizations link to it.
- **Plan** *(Implementation)* — per-realization algorithms, dependencies, and performance/durability/reliability/cost trade-offs and how they're achieved.
- **Pin** *(Compatibility)* — what may never change, who already depends on it, and what breaks if it moves. Omit entirely when nothing is frozen.
- **Priority** *(Precedence)* — a short ranked list of which consideration wins when two otherwise-legal options compete. Every entry names what wins *and* what loses, and is tagged `(public)` when callers may rely on it or `(private)` when it binds maintainers only. Only non-obvious rankings; omit entirely when there are none.

5P sharing is per-layer, not a tree: realizations of one Pledge may still have different Pitches. Abstractions carry Pitch + Pledge (and may carry a Priority binding every realization); realizations carry a Pitch, one or more Pledges, and a Plan — a realization links the abstraction's Pledge and may add realization-specific extension Pledges. Pledges, Pins, and Priorities are **linked, never copied**.

**Rules:**
- 5P on test code is completely optional and should only be included if there are some unusual goals or strategy for the test suite such as multithreading or multiprocessing to verify safety or test contention, fuzzing to try to find obscure parsing issues, etc.
- Before modifying a unit, check to see if its 5P are documented and **flag any drift** between them and the code. Treat a `<pin>` as read-only unless the change is explicitly a migration.
- A significant code change updates the affected 5P **in the same change**.
- A change to any of the 5P is **agreed in prose first**, then code and tests follow. Violating a `<pin>` is a **migration** decision, not a code decision: it invalidates data or peers that already exist, so the compatibility plan is agreed before the code changes.
- Decide **fix / enhance / branch** using the constraints at *every* layer: code-vs-description mismatch → fix; within all layers → enhance; outside any layer → new unit, or a deliberate, agreed change to that layer.
- Where several changes are all legal, the `<priority>` breaks the tie — follow it rather than re-deciding the fork. Inverting a ranking is a Priority change (prose first), and inverting a **public** one forces a Pitch review in the same change.

### General Guidelines
- Use common terse English of the style found in the project rather than verbose esoteric terminology, especially when more precise or project-specific terms are available; in code, comments, and responses
- API changes must be backwards compatible unless explicitly approved, so changing the schema of existing inputs can only add parameters with defaults, changing the schema of outputs can't remove properties unless they've previously been marked as deprecated
- When looping with retries or backup-and-restart logic, always bound the loops with a specific retry count (with exponential backoff where appropriate) or timeout
- When looping over data structures that might be recursive, always bound the loops with a specific depth limit to avoid stack overflows and infinite loops
- When designing algorithms that adjust their behavior based on inputs, and there is an option to do so, prefer algorithms that respond smoothly to inputs rather than abruptly changing behavior at certain thresholds
- If there are other cases where we loop without a specific bound, add a bound if possible, or please let me know so I can decide how to handle those situations too
- Never commit code with an established interface when you know that there are regressions in the implementation of that interface
- For modules that are likely to access lots of data, never assume it can all fit in memory.  Always use streaming and/or paging when possible, and if not possible, document the reasons why
- NEVER remove existing comments unless removing the code they apply to
- Write all tests so they can be run more than once at the same time as well as concurrently with all other tests
- New dependencies should *always* use the latest stable version of the dependency.  Outdated dependencies are a security incident waiting to happen
- Do not use dependencies that are not actively maintained or have few downloads unless there is no alternative available
- When mocks and emulators are available, the cheapest and most performant version should be used in tests instead of real dependencies, unless the test is specifically testing the dependency itself or a behavior or possible regression that is only observable with the real dependency
- Unless specifically excluded, new features should work together with old features.  For example, if Multiple Desktops were an existing feature, and we add a new feature that allows users to change their desktop background, unless specifically excluded, that would imply that the user should be able to select a *separate* desktop background for each desktop when they are using multiple desktops.  "You can't do those two at the same time" should only be true if it's theoretically impossible, not just because someone didn't want to finish a feature

### C# Coding Standards
- This is a warning-free project.  All warnings except for temporarily lingering Obsoletion warnings should be fixed before claiming completion
- Use the latest version of C# allowed by two month old releases of Visual Studio.
- Use the latest C# coding styles except for the following:
	* Avoid using var (please replace any instances of it you find)
	* Do not use Primary Constructors except for records
- NEVER use the null-forgiving operator unless you're explicitly testing nullability exceptions or you can prove that the expression cannot ever be null.  The explanation must always be in a comment for every use of the null-forgiving operator
- NEVER use [DoNotParallelize] on tests without a full explanation as to why that is absolutely necessary
- API changes must be backwards compatible unless explicitly approved, so changing the schema of existing inputs can only add nullable parameters, changing the schema of outputs can't remove properties unless they've previously been marked as deprecated
- Use modern array and collection initializers whenever possible
- Use nameof(T) when possible even if referencing method or class names in strings
- Use the newer Assert styles like IsGreaterThan in test code
- Use readonly properties when possible
- Use explicit invariant culture and UTC unless there is an exception explicitly documented in the code
- Use AmbientClock.UtcNow instead of DateTime.UtcNow unless there is a specific, documented reason not to
- Use AmbientClock.Pause() to artificially manipulate the clock for tests that would normally need to use Sleep or Delay
- Use _ as a prefix and camel casing for private instance variables
- Use _ as a prefix and Pascal casing for private static variables
- Use Pascal casing for constants
- Group members in the following order: primary: constants, statics, readonly instance, regular instance, volatile/interlocked instance; secondary: fields, constructors, properties, methods
- Always use ValueTask instead of Task, unless ValueTask is not supported by the convention, or when the caller is expected to use the response in a way that requires Task (ie. awaiting multiple times)
- Task and ValueTask's Result property, GetAwaiter().GetResult(), and other async-avoidance patterns should NEVER be used.  Propagate async coding styles up the call stack as needed
- NEVER use ConfigureAwait()
- Do not use the lock keyword or other async-unfriendly lock types, as it will inevitably have to be replaced with future asyncification.  use lock-free algorithms whenever possible, or use async-friendly waits when not possible, always explain why waits are needed instead of lock-free alogorithms
- DO NOT REMOVE EXISTING COMMENTS UNLESS REMOVING THE CODE THEY APPLY TO
- Write all tests so they can be run more than once at the same time as well as concurrently with all other tests
- if, while, and for statements may be done without braces only if the code remains on one line.  when flowing to more than one line, braces should always be used
- Function parameter lists should not be put onto multiple lines
- Comments interspersed in code should add non-obvious commentary, but should usually stay on one line unless a narrative explanation is warranted
- All code should adhere to basic secure coding standards such as OWASP, and should strike a balance between security, performance, and user experience that is appropriate to the context, including the sensitivity of the data involved; the risk of data leaks, destruction, or alteration; and the responsibilities and likely knowledge and technical abilities of users
- 
