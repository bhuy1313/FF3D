# FF3D (Fightfigter 3D Simulator) - AI Agent Guidelines

## Project Overview
**FF3D** is a first-person firefighter simulation game built with Unity (`m_EditorVersion: 6000.3.3f1`). The game focuses on simulating realistic firefighting scenarios where the player must assess the situation, select the right tools, control fire spread, and rescue victims under time pressure. 

Key gameplay systems include an FPS controller, interaction system, inventory, vitals management, and a comprehensive fire system covering intensity, spread, extinguishing mechanics, and damage.

### Key Directories
- `Assets/Game/Features`: Feature-based architecture containing core gameplay C# scripts (interaction, fire systems, player state, inventory).
- `Assets/Game/UI`: Runtime UI logic and UI prefabs/materials.
- `Assets/Game/Scenes`: Core playable scenes (e.g., Map1, Tutorial).
- `ProjectSettings/`: Unity editor and project configuration files.
- `Packages/manifest.json`: Unity package dependencies (URP, Input System, Test Framework, etc.).

*Note: Treat `Library/`, `Temp/`, `Logs/`, and `UserSettings/` as generated/local-only content; do not commit changes to these directories.*

## Building and Testing
Currently, the game is built manually through the Unity UI (`File > Build Settings`). However, headless operations can be run via the command line.

### Commands
Ensure you use the correct Unity Editor path for version `6000.3.3f1`.

*   **Compile Check (headless):**
    ```powershell
    "<UnityEditorPath>\Unity.exe" -batchmode -quit -projectPath . -logFile Logs/compile.log
    ```
*   **Run EditMode Tests:**
    ```powershell
    "<UnityEditorPath>\Unity.exe" -batchmode -quit -projectPath . -runTests -testPlatform editmode -testResults Logs/editmode-results.xml
    ```
*   **Run PlayMode Tests:**
    ```powershell
    "<UnityEditorPath>\Unity.exe" -batchmode -quit -projectPath . -runTests -testPlatform playmode -testResults Logs/playmode-results.xml
    ```

## Development Conventions

### Coding Style
- **Language:** C#
- **Indentation:** 4-space indentation.
- **Braces:** Match the style of existing scripts in `Assets/Game/Features`.
- **File Organization:** Keep one `MonoBehaviour` per file. The file name must exactly match the class name (e.g., `Fire.cs` -> `class Fire`).
- **Naming:** 
    - `PascalCase` for types, methods, and properties.
    - `camelCase` for private fields.
- **Inspector Variables:** Prefer `[SerializeField] private` for fields that need to be exposed in the Inspector instead of making them `public`.
- **Comments:** Keep comments short and focused on intent. Remove dead or debug code before submitting changes.

### Testing Guidelines
- The Unity Test Framework (`com.unity.test-framework`) is installed.
- Add new tests in `Assets/Game/Tests/EditMode` or `Assets/Game/Tests/PlayMode`.
- Use descriptive test file names like `FeatureNameTests.cs`.
- Keep tests deterministic; avoid relying on scene-only or manual setup when possible.
- When implementing gameplay changes, include at least one automated test or document the reasoning if automation is not feasible.

### Git and Commit Guidelines
- Use short, imperative commit subjects (e.g., `Add ...`, `Refactor ...`).
- Recommended format: `Verb scope: concise summary` (e.g., `Refactor fire: clamp spread overlap`).
- Call out changes to `ProjectSettings/` or package dependencies explicitly.
