# FF3D (Fightfigter 3D Simulator) - AI Agent Guidelines

## Project Overview
**FF3D** is a squad-based tactical firefighter simulation game built with Unity (`m_EditorVersion: 6000.4.4f1`). The game focuses on simulating realistic, procedural firefighting scenarios where the player acts as a frontline commander. They must assess incidents at the dispatch desk (Call Phase), lead a team of AI firefighters, select specialized tools, control dynamic fire spread, and execute complex rescue operations under strict time and survival pressures.

Key gameplay systems include:
- **Call Phase & Incident Generation:** Dispatch desk interaction, risk assessment, and procedural incident seeding.
- **Squad-Based AI (Bots):** Intelligent teammates capable of following orders to extinguish fires, breach obstacles, and rescue victims.
- **Advanced Fire Simulation:** Material-based cluster spread, scorch decals, smoke hazards, and ventilation mechanics.
- **Extensive Tool Arsenal:** A robust Tool Wheel featuring thermal cameras, masks, various extinguishers, hoses, ladders, and rescue cushions.
- **Dynamic Mission System:** Highly configurable objectives, fail conditions, and scoring based on efficiency and casualties.

### Key Directories
- `Assets/Game/Features/CallPhase`: Dispatch desk gameplay, transcript analysis, and incident seeding logic.
- `Assets/Game/Features/Incident/Scripts/Bot`: Core AI Squad logic (pathfinding, behavior, commands, inventory).
- `Assets/Game/Features/Incident/Scripts/Equipment` & `ToolWheel`: Specialized firefighting tools and UI selection.
- `Assets/Game/Features/Incident/Scripts/FireSimulation`: Complex procedural fire, scorch marks, and smoke mechanics.
- `Assets/Game/Features/Incident/Scripts/Gameplay`: Mission system, objectives, scoring, and cinematic flow.
- `Assets/Game/UI`: Runtime UI logic and UI prefabs/materials.
- `ProjectSettings/`: Unity editor and project configuration files.
- `Packages/manifest.json`: Unity package dependencies.

*Note: Treat `Library/`, `Temp/`, `Logs/`, and `UserSettings/` as generated/local-only content; do not commit changes to these directories.*

## Building and Testing
Currently, the game is built manually through the Unity UI (`File > Build Settings`). However, headless operations can be run via the command line.

### Commands
Ensure you use the correct Unity Editor path for version `6000.4.4f1`.

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
