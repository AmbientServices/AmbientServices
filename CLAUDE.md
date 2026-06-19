# AmbientServices

## Module descriptions — The 3P Protocol (3P)

Each unit of code (class, module, subsystem, or system) carries up to three named prose layers — the **3P** — in its XML-doc `<remarks>`, using custom elements `<pitch>`, `<pledge>`, `<plan>`. Generic definition: `docs/MODULE_DESCRIPTIONS.md`; C# placement convention and examples: `docs/MODULE_DESCRIPTIONS.PicMe.md`. Refer to these by their exact names — Pitch, Pledge, Plan — never informal synonyms.

### The 3P Protocol (3P)
- **Pitch** *(Value Proposition)* — short; the caller's "is this what I need?" decision. Problem/benefit, optional limits.
- **Pledge** *(Contract)* — data flow, valid/invalid call sequences, and behavioral rules the signatures can't express. Attaches to the abstraction; realizations link to it.
- **Plan** *(Implementation)* — per-realization algorithms, dependencies, and performance/durability/reliability/cost trade-offs and how they're achieved.

Sharing is per-layer, not a tree: realizations of one Pledge may still have different Pitches. Abstractions carry Pitch + Pledge; realizations carry a Pitch, one or more Pledges, and a Plan — a realization links the abstraction's Pledge and may add realization-specific extension Pledges.

**Rules:**
- Before modifying a unit, check to see if its 3 Ps are documented and **flag any drift** between them and the code.
- A significant code change updates the affected 3 Ps **in the same change**.
- A change to any of the 3 Ps is **agreed in prose first**, then code and tests follow.
- Decide **fix / enhance / branch** using the constraints at *every* layer: code-vs-description mismatch → fix; within all layers → enhance; outside any layer → new unit, or a deliberate, agreed change to that layer.
- This is a warning-free project.  All except for temporarily lingering Obsoletion warnings should be resolved before committing.
- This project uses the latest version of C# allowed by two month old releases of Visual Studio.
- This project uses the latest C# coding styles except for the following:
	* Avoid using var (please replace any instances of it you find)
	* Do not use Primary Constructors except for records
- Use modern array and collection initializers whenever possible
- Use nameof(T) when possible even if referencing method or class names in strings
- Use the newer Assert styles like IsGreaterThan in test code
- Use readonly properties when possible
- Use explicit invariant culture and UTC unless there is an exception explicitly documented in the code
- Use AmbientClock.UtcNow instead of DateTime.UtcNow unless there is a specific, documented reason not to
- Use _ as a prefix and camel casing for private instance variables
- Use _ as a prefix and Pascal casing for private static variables
- Use Pascal casing for constants
- Group members in the following order: primary: constants, statics, readonly instance, regular instance, volatile/interlocked instance; secondary: fields, constructors, properties, methods
- Always use ValueTask instead of Task, unless ValueTask is not supported by the convention, or when the caller is expected to use the response in a way that requires Task (ie. awaiting multiple times)
- Task and ValueTask's Result property, GetAwaiter().GetResult(), and other async-avoidance patterns should NEVER be used.  Propagate async coding styles up the call stack as needed
- Do not use ConfigureAwait()
- DO NOT REMOVE EXISTING COMMENTS UNLESS REMOVING THE CODE THEY APPLY TO
- Write all tests so they can be run more than once at the same time as well as concurrently with all other tests
- if, while, and for statements may be done without braces only if the code remains on one line.  when flowing to more than one line, braces should always be used.
- Function parameter lists should not be put onto multiple lines
- Comments should add non-obvious commentary, but should usually stay on one line unless a narrative explanation is warranted