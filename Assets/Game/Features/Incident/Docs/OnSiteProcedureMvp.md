# On-Site Procedure MVP

## Goal
Add a procedure recommendation layer to `On Site Phase` that teaches safe firefighting decision-making without implying that SOP is always absolute.

The system should communicate three things:
- Standard procedure is the baseline and usually the safest choice.
- Some situations justify controlled deviation when life safety is at stake.
- Deviating from procedure is not free; it should trade safety/compliance for time or rescue outcome.

## Design Position
Do not ship this as a pure self-check checklist.

That version is easy to author, but it produces weak gameplay because the player can tick boxes without the game reacting to behavior. The recommended MVP is a hybrid:
- The player sees a recommended checklist.
- Some items are manual acknowledgement only.
- Some items are auto-validated through mission signals or state.
- End-of-mission debrief compares what the player claimed, what the scene observed, and what the outcome was.

## Core Loop
1. `Call Phase` persists the incident payload as it already does today.
2. `On Site` resolves one `IncidentProcedureDefinition` from payload fields such as `scenarioId`, `hazardType`, `logicalFireLocation`, `severityBand`, and trapped-victim estimate.
3. The player receives a `Procedure Recommendation` panel before or during scene start.
4. The panel presents:
- command priorities
- required / recommended / conditional checklist items
- critical exception notes
5. During play:
- some steps complete automatically from `MissionSignalSource`
- some remain manual
- some can be invalidated by contradictory signals
6. At mission end, the game scores outcome and prints a debrief:
- what procedure items were respected
- what procedure items were skipped
- what deviations were justified
- what deviations created unnecessary risk

## Why This Fits Current Code
The current codebase already has the right primitives:
- `IncidentWorldSetupPayload` carries scenario-derived state into the scene.
- `MissionSignalSource` already emits scene events with lightweight authoring.
- `MissionScoreConfig` already supports signal-based score deltas.
- `MissionObjectiveDefinition` and `MissionFailConditionDefinition` already evaluate mission state and can remain the source of truth for success/failure.

This means the procedure layer can start as an advisory-and-scoring system, not a replacement for the mission system.

## Data Model
`IncidentProcedureDefinition` is the root asset.

Suggested responsibilities:
- scenario matching
- command priorities
- procedure checklist authoring
- exception authoring
- debrief copy
- axis scoring weights

Checklist item types:
- `Required`: baseline safety-critical expectation
- `Recommended`: good practice but not always decisive
- `Conditional`: only relevant when a condition or signal is true

Validation modes:
- `ManualOnly`: player ticks it manually
- `AutoSignal`: completes from a `MissionSignalSource`
- `AutoState`: reserved for later runtime state evaluation
- `Hybrid`: player can tick it, but signals can confirm or contradict it

Scoring axes:
- `LifeSafety`
- `FireControl`
- `CrewSafety`
- `ProcedureCompliance`
- `TimeEfficiency`

## UI Recommendation
Keep the UI compact and operational, not academic.

Panel layout:
- title and summary
- 3 command priorities
- checklist grouped by `Required`, `Recommended`, `Conditional`
- exception warning cards

Checklist row content:
- title
- one-line rationale
- priority badge
- state icon: pending / checked / auto-verified / contradicted

Do not force the player to open this panel constantly. It should be available as a side panel or toggleable tablet-style overlay.

## MVP Scoring Rules
Use existing mission scoring for mission success, then add procedure interpretation on top.

Recommended MVP logic:
- `Required` item completed: positive compliance score
- `Required` item contradicted: compliance penalty
- critical exception used when life safety improved: positive `LifeSafety`, negative or neutral `ProcedureCompliance`
- reckless deviation without rescue benefit: negative `CrewSafety` and `ProcedureCompliance`

Important: do not let checklist score override hard mission failure. The mission system still owns fail states.

## Example Scenario
Kitchen fire with reported trapped child.

Recommended procedure:
- size up entry route
- isolate utilities if safely accessible
- coordinate extinguishment and search
- maintain escape path

Critical exception:
- if a victim is visible and time-to-death is immediate, rapid entry before full isolation may be justified

Scoring interpretation:
- player enters early, rescues victim, then suffers secondary flare-up because utilities stayed live
- result should read as tactically mixed, not simply correct or incorrect

## Implementation Order
1. Author 1-2 `IncidentProcedureDefinition` assets for current playable scenarios.
2. Add a lightweight resolver that picks a matching procedure from `IncidentWorldSetupPayload`.
3. Build a simple UI panel that renders the resolved asset.
4. Wire a few existing `MissionSignalSource` objects to auto-complete or invalidate checklist items.
5. Append procedure debrief lines to end-of-mission results.

## Non-Goals For MVP
- full realtime SOP enforcement
- complex rule engine
- AI commander dialogue
- exhaustive branch tracking

Those can come later if the hybrid checklist proves useful.
