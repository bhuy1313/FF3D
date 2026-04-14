# Repository Guidelines

## Project Structure & Module Organization
- `Assets/Game/Features/CallPhase`: call intake flow, transcript extraction, follow-up, assessment/result UI, and related scenes/scripts.
- `Assets/Game/Features/Incident`: onsite gameplay systems such as player control, interaction, fire/smoke, hazards, inventory, and imported character assets.
- `Assets/Game/Features/MainMenu`, `Assets/Game/Features/LevelSelect`, `Assets/Game/Features/Minimap`: menu flow, level routing, settings/localization, and minimap UI/runtime.
- `Assets/Game/Shared/Scripts`: cross-feature runtime state such as loading flow, locks, and shared payload/state helpers.
- `Assets/Game/Scenes`: currently includes playable scenes such as `Tutorial.unity` and `Map1.unity`.
- `Assets/Game/UI` and feature-local `Prefabs` / `Animation` folders hold runtime UI prefabs, fonts, controllers, and animation assets.
- `Packages/manifest.json`: Unity package dependencies.
- `ProjectSettings/`: Unity editor/project configuration, including version and package settings.
- Treat `Library/`, `Temp/`, `Logs/`, and `UserSettings/` as generated/local-only content; do not commit changes there unless the user explicitly asks.

## Build, Test, and Development Commands
- Use Unity Editor version `6000.4.0f1` (see `ProjectSettings/ProjectVersion.txt`).
- Compile check (headless):
  ```powershell
  "<UnityEditorPath>\Unity.exe" -batchmode -quit -projectPath . -logFile Logs/compile.log
  ```
- Run EditMode tests:
  ```powershell
  "<UnityEditorPath>\Unity.exe" -batchmode -quit -projectPath . -runTests -testPlatform editmode -testResults Logs/editmode-results.xml
  ```
- Run PlayMode tests:
  ```powershell
  "<UnityEditorPath>\Unity.exe" -batchmode -quit -projectPath . -runTests -testPlatform playmode -testResults Logs/playmode-results.xml
  ```
- Build player: currently done through Unity UI (`File > Build Settings`); no repository-owned build script is tracked yet.

## Coding Style & Naming Conventions
- C# uses 4-space indentation and brace style consistent with existing scripts under `Assets/Game`.
- Keep one `MonoBehaviour` per file, and match file/class names exactly.
- Use `PascalCase` for types, methods, properties, and serialized enums; use `camelCase` for private fields.
- Prefer `[SerializeField] private` for Inspector-exposed state instead of public fields.
- Keep comments short and intent-focused. Prefer removing dead/debug code over leaving commented-out blocks.
- For scene/prefab wiring, prefer stable serialized references over string-based hierarchy lookups when feasible.

## Testing Guidelines
- Unity Test Framework (`com.unity.test-framework`) is installed, but repository-owned automated tests are limited.
- Do not create or modify test files unless the user explicitly requests it.
- Add new tests under `Assets/Tests/EditMode` or `Assets/Tests/PlayMode`.
- Use test file names like `FeatureNameTests.cs` and keep tests deterministic.
- For gameplay/UI changes where automation is not practical, document manual verification steps in the final response.

## Commit & Pull Request Guidelines
- Recent history uses short imperative commit subjects such as `Add ...`, `Fix ...`, `Refactor ...`.
- Recommended format: `Verb scope: concise summary` (example: `Refactor minimap: split fullscreen legend binding`).
- PRs should include: what changed, affected scenes/prefabs/scripts, manual test steps, and screenshots/videos for UI or visual updates.
- Call out `ProjectSettings/`, package changes, scene YAML changes, and prefab wiring changes explicitly.
