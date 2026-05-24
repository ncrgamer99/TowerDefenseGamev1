# MetaHub UI

Runtime UI Toolkit implementation of the generated Meta-Hub screen.

## Runtime flow

- `ChaosUnlockManager` keeps the existing F1 hotkey path.
- F1 is ignored while the start menu is open or the game is over.
- In gameplay, the existing `ChaosUnlockRuntimeUI.OpenUnlocks()` call redirects to `MetaHubController`.
- The start-menu unlock view is left unchanged.
- `MetaHubController` loads `Resources/MetaHubScreen.uxml` and `Resources/MetaHubScreen.uss` at runtime.
- `MetaHubController.useCanvasFallback` is enabled by default so the Meta-Hub is still visible if the runtime `UIDocument` panel does not render in the current scene.

## Data flow

- `MetaHubData` contains all dynamic fields shown by the UI.
- `MetaHubMockData.Create()` contains the screenshot-like sample values.
- `MetaHubMockData.CreateFromGame(GameManager)` maps currently available game/progression managers into `MetaHubData`.
- Lists such as goals, buffs, risks, keystones, and run stats are rebuilt from data collections.

## Integration notes

- No scene or prefab reference is required for the first runtime version.
- The button callbacks are prepared as events on `MetaHubController`.
- Replace or extend `MetaHubMockData.CreateFromGame` when more real progression sources become available.
