# AmbientServices

## Module descriptions — The 3P Protocol (3P)

Each unit of code (class, module, subsystem, or system) carries up to three named prose layers — the **3P** — in its XML-doc `<remarks>`, using custom elements `<pitch>`, `<pledge>`, `<plan>`. The library viewed as one whole-system unit carries its layers in dedicated project-level files instead: `docs/PITCH.md`, `docs/PLEDGE.md`, `docs/PLAN.md`, linked from `README.md`. Generic definition: `docs/MODULE_DESCRIPTIONS.md`; C# placement convention and examples: `docs/MODULE_DESCRIPTIONS.AmbientServices.md`. Refer to these by their exact names — Pitch, Pledge, Plan — never informal synonyms.

### The 3P Protocol (3P)
- **Pitch** *(Value Proposition)* — short; the caller's "is this what I need?" decision. Problem/benefit, optional limits.
- **Pledge** *(Contract)* — data flow, valid/invalid call sequences, and behavioral rules the signatures can't express. Attaches to the abstraction; realizations link to it.
- **Plan** *(Implementation)* — per-realization algorithms, dependencies, and performance/durability/reliability/cost trade-offs and how they're achieved.

3P Sharing is per-layer, not a tree: realizations of one Pledge may still have different Pitches. Abstractions carry Pitch + Pledge; realizations carry a Pitch, one or more Pledges, and a Plan — a realization links the abstraction's Pledge and may add realization-specific extension Pledges.

**Rules:**
- Before modifying a unit, check to see if its 3P are documented and **flag any drift** between them and the code.
- A significant code change updates the affected 3P **in the same change**.
- A change to any of the 3P is **agreed in prose first**, then code and tests follow.
- Decide **fix / enhance / branch** using the constraints at *every* layer: code-vs-description mismatch → fix; within all layers → enhance; outside any layer → new unit, or a deliberate, agreed change to that layer.

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
