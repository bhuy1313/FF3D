# Repository Guidelines

## Project Structure & Module Organization
- `Assets/TrueJourney/Script`: core gameplay C# (interaction, fire systems, player state, inventory).
- `Assets/UI/Scripts` and `Assets/UI/Prefabs`: runtime UI logic and UI prefabs/materials.
- `Assets/Scenes`: playable scenes (`DemoWorld.unity`, `SampleScene.unity`).
- `Packages/manifest.json`: Unity package dependencies (URP, Input System, Test Framework, etc.).
- `ProjectSettings/`: Unity editor/project configuration, including version and package settings.
- Treat `Library/`, `Temp/`, `Logs/`, and `UserSettings/` as generated/local-only content; do not commit changes there.

## Build, Test, and Development Commands
- Use Unity Editor version `6000.3.3f1` (see `ProjectSettings/ProjectVersion.txt`).
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
- Build player: currently done through Unity UI (`File > Build Settings`); no custom build script is tracked yet.

## Coding Style & Naming Conventions
- C# uses 4-space indentation and brace style consistent with existing scripts in `Assets/TrueJourney/Script`.
- Keep one `MonoBehaviour` per file, and match file/class names exactly (example: `Fire.cs` -> `Fire`).
- Use `PascalCase` for types/methods/properties and `camelCase` for private fields.
- Prefer `[SerializeField] private` for Inspector-exposed state instead of public fields.
- Keep comments short and intent-focused; remove dead/debug code before opening a PR.

## Testing Guidelines
- Unity Test Framework (`com.unity.test-framework`) is installed, but repository-owned tests are not yet present under `Assets/`.
- Add new tests in `Assets/Tests/EditMode` or `Assets/Tests/PlayMode`.
- Use test file names like `FeatureNameTests.cs` and keep tests deterministic (avoid scene-only/manual setup when possible).
- For gameplay changes, include at least one automated test or document why automation is not feasible.

## Commit & Pull Request Guidelines
- Recent history uses short imperative commit subjects (for example, `Add ...`, `Refactor ...`).
- Recommended format: `Verb scope: concise summary` (example: `Refactor fire: clamp spread overlap`).
- PRs should include: what changed, affected scenes/prefabs/scripts, manual test steps, and screenshots/videos for UI or visual updates.
- Call out `ProjectSettings/` or package dependency changes explicitly and link related issues/tasks.
