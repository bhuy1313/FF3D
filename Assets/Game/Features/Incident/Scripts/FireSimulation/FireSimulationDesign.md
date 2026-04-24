# Fire Simulation Architecture

## Goal
- Move fire gameplay away from per-fire `MonoBehaviour` simulation.
- Keep one central simulation source of truth.
- Let VFX, damage, and suppression read from simulation state instead of owning it.

## Runtime Layers
1. `FireSurfaceNodeAuthoring`
- Scene authoring point on floor, wall, ceiling, or object surface.
- Stores fuel, ignition threshold multiplier, spread resistance, hazard type, and neighbor hints.

2. `FireSurfaceGraph`
- Collects authoring nodes from the scene.
- Builds a runtime graph once on startup.

3. `FireSimulationManager`
- Owns the runtime graph.
- Ticks heat, wetness, fuel, ignition, and spread on a fixed interval.
- Rebuilds cluster snapshots on a slower interval for presentation.

4. `FireClusterSnapshot`
- Read-only summary of a burning region.
- Intended to drive VFX, audio, damage zones, and AI/nav reactions.

5. `FireClusterView`
- Pure presentation object.
- Can be pooled and LOD-ed without changing simulation.

## Suggested Integration Path
1. Use `IncidentPayloadAnchor` only to ignite graph nodes, not to spawn many fire prefabs.
2. Let extinguisher and hose call `FireSimulationManager.ApplySuppressionSphere(...)`.
3. Let mission objectives query burning node count or active cluster count from the manager.
4. Add a separate exposure system that samples nearby cluster snapshots instead of many trigger colliders.

## Why This Scales Better
- Simulation runs in one manager at low frequency.
- Spread is graph-based, not physics-query-based.
- VFX count follows cluster count, not burning node count.
- Damage and nav can be sampled from clustered state instead of per-fire callbacks.
