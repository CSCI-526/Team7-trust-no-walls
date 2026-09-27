# Trust No Wall Implementation Plan

Note: the spec was amended during development; `planning/spec.md` is authoritative over this plan wherever the two disagree.

Spec: `planning/spec.md` (binding authority; all numbers, texts and colors come from there).

Repo root: `/Users/manasvardhan/Desktop/TrustNoWall` (git, branch `main`). Unity project: `Game/` (freshly created, never committed). A sibling project with proven tooling exists at `/Users/manasvardhan/Desktop/RuleShift` (same Unity version): copy and adapt from it where noted rather than reinventing.

## Global Constraints

- Unity binary: `/Applications/Unity/Hub/Editor/6000.6.0f1-arm64/Unity.app/Contents/MacOS/Unity`. Only one Unity batch process may touch `Game/` at a time. Never open the GUI editor. Never touch the RuleShift repo except to read/copy from it.
- Run EditMode tests with `tools/run-tests.sh`; build WebGL with `tools/build-webgl.sh` (both from Task 1). Both exit non-zero on failure and print a one-line summary.
- Runtime code: `Game/Assets/Scripts/Core/` (namespace `TrustNoWall.Core`, pure C#: no MonoBehaviour, no UnityEngine APIs except `Vector2`, `Vector2Int`, `Mathf`, `Color` value types) and `Game/Assets/Scripts/Game/` (namespace `TrustNoWall.Game`). Assembly `TrustNoWall.Runtime` (`Game/Assets/Scripts/TrustNoWall.Runtime.asmdef`). Editor: `Game/Assets/Editor/` assembly `TrustNoWall.Editor`. Tests: `Game/Assets/Tests/EditMode/` assembly `TrustNoWall.Tests.EditMode`.
- Everything visible is created from code at runtime; no prefabs, no imported art, no packages beyond `com.unity.test-framework`. IMGUI for UI. Legacy Input Manager.
- Determinism: all randomness in Core uses `System.Random` passed in or seeded from the level seed. Same (level, seed) gives the same layout.
- Commit `.meta` files for every asset added. Never commit `Game/Library`, `Game/Temp`, `Game/Logs`, `Game/UserSettings`, `Game/.vscode`, `test-results/`, `build-logs/`, `node_modules`.
- Test output clean: 0 failures, no compiler warnings from our assemblies.
- Commit messages: plain subject + body. No AI attribution, no Co-Authored-By trailers. Never use em dashes anywhere (code, comments, docs, commits).
- Do not push, do not create GitHub repos. Do not dispatch subagents.

---

### Task 1: Scaffold from the RuleShift tooling

- Copy and adapt from `/Users/manasvardhan/Desktop/RuleShift`: `.gitignore` (add `Game/.vscode/`, `tools/web/node_modules/`), `tools/run-tests.sh`, `tools/build-webgl.sh`, `Game/Assets/Editor/BuildScript.cs` (namespace `TrustNoWall.EditorTools`, productName `Trust No Wall`, companyName `TrustNoWall Team`, same WebGL settings: compression Disabled, decompressionFallback false, 960x540, runInBackground, Default template, output `<repoRoot>/docs`, writes `docs/.nojekyll`), the three asmdefs (renamed to TrustNoWall.*), and `Game/Packages/manifest.json`'s `com.unity.test-framework` entry (same version as RuleShift).
- `Game/Assets/Scripts/Game/GameBootstrap.cs`: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] Boot()` creating root `TrustNoWall` with a child orthographic camera (background `#0E1016`, position (0,0,-10), size 5) and logging `Trust No Wall booted`; guard against double boot.
- `Game/Assets/Tests/EditMode/SanityTests.cs`: reflection test that `GameBootstrap.Boot` exists with `RuntimeInitializeOnLoadMethodAttribute`.
- `git init` is already done by the controller if `.git` exists; otherwise run `git init -b main`.
- Verify: `tools/run-tests.sh` prints 1/1 passed; `tools/build-webgl.sh` succeeds; `docs/index.html` title contains `Trust No Wall`; `docs/.nojekyll` exists.
- Commit everything (Game project incl. ProjectSettings and metas, tools, docs, planning). Subject: `Scaffold Trust No Wall Unity project and build tooling`.

---

### Task 2: Grid model and maze generation (Core, TDD)

Files in `Game/Assets/Scripts/Core/`, tests in `Game/Assets/Tests/EditMode/<Name>Tests.cs`.
1. `Grid.cs`: `public enum Dir { Up, Right, Down, Left }`; static `DirUtil` with `Vector2Int Delta(Dir)`, `Dir Opposite(Dir)`, `IReadOnlyList<Dir> All`. `public readonly struct Edge : IEquatable<Edge>`: canonical form stores the lower-left cell and an orientation (`Vertical` = edge between (x,y) and (x+1,y); `Horizontal` = between (x,y) and (x,y+1)). `static Edge Between(Vector2Int a, Dir d)`, `Vector2Int A`, `Vector2Int B`, `bool IsVertical`, `Vector2 WorldCenter(int n)` (cell size 1, maze centered at origin: cell (x,y) center = (x - (n-1)/2f, y - (n-1)/2f)). Also static `Vector2 CellCenter(Vector2Int c, int n)`.
2. `Maze.cs`: `Maze(int n)` starts with ALL walls present. `int N`, `bool InBounds(Vector2Int)`, `bool HasWall(Vector2Int c, Dir d)` (true for boundary), `bool HasWall(Edge)`, `void SetWall(Edge, bool)`, `IEnumerable<Edge> InteriorEdges()` (all interior edges), `IEnumerable<Edge> InteriorWalls()` (interior edges that have walls), `IEnumerable<Vector2Int> OpenNeighbors(Vector2Int)`, `Maze Clone()`.
3. `MazeGenerator.cs`: `static Maze GeneratePerfect(int n, System.Random rng)` (iterative DFS from (0,0)); `static int Braid(Maze m, int count, System.Random rng)` (removes up to `count` random interior walls, returns number removed); `static int[,] Distances(Maze m, Vector2Int from)` (BFS over open edges, -1 unreachable); `static Vector2Int Farthest(Maze m, Vector2Int from)` (max distance, ties lowest x then lowest y); `static List<Vector2Int> ShortestPath(Maze m, Vector2Int a, Vector2Int b)`; `static bool IsBridge(Maze m, Edge e)` (open edge whose removal disconnects the maze).
- Tests: perfect maze has exactly n*n-1 open interior edges and is connected; determinism by seed; braid count formula effect; distances and farthest correct on a hand-built 3x3; shortest path valid (consecutive cells adjacent through open edges); IsBridge true for every open edge of a perfect maze and false for an edge on a loop.
- Verify with `tools/run-tests.sh`. Commit: `Add grid model and maze generation`.

---

### Task 3: Level elements, progression and solvable level generation (Core, TDD)

Files in `Game/Assets/Scripts/Core/`:
1. `Mechanic.cs`: `enum Mechanic { InvisibleWalls, MemoryTiles, MovingWalls, DisappearingWalls, CollapsingTiles, TriggerWalls, Teleporters, RotatingBarriers, Patrols, OneWayPaths, Decoys, Chaser }` and `MechanicInfo` (display name, one-line explanation for intro cards, intro level, swatch color from the spec's Visual style). Explanations (use verbatim): InvisibleWalls "Some walls are invisible. Touching one is fatal."; MemoryTiles "Blue tiles briefly reveal nearby invisible walls."; MovingWalls "Orange walls slide back and forth. Don't get crushed."; DisappearingWalls "Cyan walls vanish and return. Cross while they're gone."; CollapsingTiles "Cracked tiles collapse soon after you step on them."; TriggerWalls "Pressure plates open and close the walls of their color."; Teleporters "Purple pads teleport you. Not always forward."; RotatingBarriers "Gates rotate. Enter only from the open sides."; Patrols "Red spikes patrol side corridors. Time your crossing."; OneWayPaths "Arrows can only be crossed one way."; Decoys "Fake goals send you back to the start. The real star spins."; Chaser "A shadow follows your trail. Keep moving."
2. `Elements.cs`: immutable element classes with pure timing functions (t = seconds since attempt start):
   - `MovingWall(Edge a, Edge b, float phase)`: constants Dwell 3.0, Slide 0.4, Warn 0.6. `Edge BlockingEdgeAt(float t)`, `float SlideProgressAt(float t)` (0 at a, 1 at b; animates during slides), `bool IsWarningAt(float t)`. Cycle: dwell at a, slide a->b, dwell at b, slide b->a; the blocking edge switches at slide midpoint. Intro card text: "Orange walls slide between two spots. Wait for the gap."
   - `PhaseWall(Edge edge, float phase, bool onBaseWall)`: Solid 3.0, Open 2.0, Warn 0.6. `bool IsSolidAt(float t)`, `bool IsWarningAt(float t)` (last 0.6 s of the open window), `float OpacityAt(float t)`.
   - `RotatingGate(Vector2Int cell, float phase, bool startHorizontal)`: Period 2.5, Rotate 0.3. `bool IsHorizontalAt(float t)` (switches at the end of the rotation), `float AngleAt(float t)` (degrees, animates), `bool Allows(Dir side, float t)` (horizontal allows Left/Right).
   - `Patrol(IReadOnlyList<Vector2Int> path, float phase)`: StepTime 0.45, ping-pong. `Vector2Int CellAt(float t)`, `Vector2 PositionAt(float t, int n)` (interpolated world position), `float HalfStepCellAt` semantics: occupies the cell it is leaving for the first half of a step and the next cell for the second half: `Vector2Int OccupiedCellAt(float t)`.
   - Data-only: `InvisibleWall(Edge)`, `MemoryTile(Vector2Int)`, `CollapseTile(Vector2Int cell, bool permanent)` (constants CollapseDelay 0.8, RestoreDelay 4.0), `TriggerPlate(Vector2Int plate, IReadOnlyList<Edge> opens, IReadOnlyList<Edge> closes, int colorIndex)`, `Teleporter(Vector2Int pad, Vector2Int target)`, `OneWay(Edge edge, Dir allowed)` (allowed = direction of travel that may cross), `Decoy(Vector2Int cell)`.
3. `LevelLayout.cs`: `Level`, `N`, `Seed`, `Maze` (walls; invisible walls ARE walls in this maze), `Start`, `Destination`, `Mechanics` (present), `NewMechanics` (introduced this level), and read-only lists of every element type above, plus `bool HasChaser`. Helper lookups: `IsInvisible(Edge)`, `CellElementAt(Vector2Int)` kind query (or dictionaries).
4. `LevelPlan.cs`: `static IReadOnlyList<Mechanic> MechanicsFor(int level, System.Random rng)` and `static int CountFor(Mechanic m, int n, int interiorWalls, bool half)` implementing the spec's progression and counts exactly (half = the earlier mechanic added at half count, rounded up, min 1). InvisibleWalls and MemoryTiles always come together.
5. `SolvabilityValidator.cs`: `static bool IsSolvable(LevelLayout layout)` implementing the spec's conservative directed graph and the R subset of D check.
6. `LevelGenerator.cs`: `static LevelLayout Generate(int level, int seed)`: N = level + 3; perfect maze; Start (0,0); Destination = Farthest in the perfect maze; braid from level 4; then place mechanics in this order: TriggerWalls, OneWayPaths, MovingWalls, DisappearingWalls, InvisibleWalls, MemoryTiles, CollapsingTiles, Teleporters, RotatingBarriers, Patrols, Decoys, Chaser. Each element is validated with `IsSolvable` right after placement and dropped if it fails; up to 40 tries per element. Placement rules from the spec (exclusions around Start/Destination; one cell element per cell; one edge element per edge). Specific rules: moving wall pairs must be two parallel open edges one cell apart; half of disappearing walls on base walls, half on open edges of the current Start-to-Destination shortest path; trigger closes only non-bridge open edges, opens only interior walls; patrols on dead-end branches of length 2 to 4 that attach to a cell of the Start-to-Destination path (path = [junction, branch cells...]); decoys at the end of the longest dead-end branch not containing the Destination (length >= 3); one-way edges only on non-bridge open edges or on route edges pointing toward the Destination; teleporter targets random cells not equal to the pad, Start or Destination; invisible walls chosen from interior walls that do not carry another element.
- Tests: counts per level follow the plan; Level 1 has no mechanics; level 2 introduces InvisibleWalls+MemoryTiles; each intro level introduces exactly its mechanics; determinism; every generated level for levels 1..15 and seeds 1..20 satisfies `IsSolvable` and has Start != Destination; validator rejects hand-built trap cases (a one-way into a dead end; a teleporter into a sealed pocket; a permanent collapse tile on a bridge); element timing functions (moving wall edge switch at midpoint, phase solid/open windows and warning, gate orientation switching, patrol ping-pong and occupied cell).
- Verify with `tools/run-tests.sh`. Commit: `Add level elements, progression and solvable level generation`.

---

### Task 4: Level simulation part 1: movement, walls and edge mechanics (Core, TDD)

`Game/Assets/Scripts/Core/LevelSim.cs` (split helpers into `SimEvents.cs` etc. if it grows past ~400 lines):
- `public enum SimStatus { Playing, Dead, Complete }`; `public enum SimEventKind { Step, Bump, Died, Completed, Teleported, TriggerToggled, MemoryRevealed, TileCracked, TileCollapsed, TileRestored, DecoyFound, Warning, ChaserSpawned }`; `public struct SimEvent { Kind; Vector2Int Cell; string Text; }`.
- `LevelSim(LevelLayout layout)`, `Reset()` (back to Start, t = 0, all dynamic state reset), `IReadOnlyList<SimEvent> Step(float dt, Dir? held)` (clears and returns the frame's event list; no-ops unless Playing).
- State for the view: `Layout`, `Time`, `Status`, `DeathCause`, `PlayerCell` (logical cell), `bool IsMoving`, `Vector2Int MoveFrom`, `MoveTo`, `float MoveProgress` (0..1), `Dir Facing`, `Vector2 PlayerWorldPos`, `Vector2Int PlayerOccupiedCell` (from for first half, to for second half), `float BumpTimer`, `bool IsEdgeBlocked(Edge e, Vector2Int from, Dir d)` (current passability, used by autopilot too), trigger toggle states `bool IsTriggerEdgeOpen(Edge)`, `float MemoryRevealRemaining(Edge invisibleWall)` (0 when hidden).
- This task implements: grid-step movement (0.13 s steps, chaining while held, 0.12 s bump lockout); blocking by visible walls, one-way wrong direction, moving wall on the edge, solid phase wall, closed trigger edge, rotating gate sides (for the cell being left and the cell being entered); invisible wall = death "Walked into an invisible wall"; crush while crossing: if the edge being crossed becomes blocked by a moving wall ("Crushed by a moving wall"), phase wall ("Crushed by a disappearing wall") or trigger wall ("Crushed by a trigger wall") before MoveProgress reaches 1; trigger plates toggle their edges on entering the plate cell (event TriggerToggled); memory tiles reveal invisible walls within Chebyshev distance 3 for 3.0 s (event MemoryRevealed); reaching Destination -> Complete (event Completed); Warning events (once per warning window) for moving/phase walls whose edge is within 4 cells (Chebyshev) of the player.
- Leave clean extension points for Task 5 (cell hazards, teleporters, patrols, decoys, chaser): e.g. private `OnEnteredCell(cell)` and `CheckHazards()` hooks.
- Tests (drive the sim with fixed dt such as 1/60): movement timing and chaining, bump on visible wall, death on invisible wall, one-way both directions, gate allows/blocks, phase wall blocks when solid and passes when open, crush by phase wall and by moving wall, trigger toggle opens/closes and crush, memory reveal timing and radius, completion, Reset restores everything. Build tiny hand-made layouts in tests via a test helper (add an internal/public constructor or builder on LevelLayout for tests).
- Verify, commit: `Add level simulation movement and edge mechanics`.

---

### Task 5: Level simulation part 2: cell hazards, teleporters, patrols, decoys, chaser (Core, TDD)

Extend `LevelSim`:
- Collapsing tiles: entering starts a 0.8 s timer (TileCracked); at 0.8 s it collapses (TileCollapsed); if the player occupies it then: death "Fell through a collapsing tile"; while collapsed, entering it (the step's `to` cell once MoveProgress passes 0.5) is death "Fell into a pit"; temporary tiles restore after 4.0 s (TileRestored), permanent stay collapsed until Reset. Expose `TileState(Vector2Int) -> (Intact | Cracking(progress) | Collapsed | Restoring?)` for the view.
- Teleporters: arriving on a pad (step completes) waits 0.15 s (input frozen) then moves the player to the target (Teleported, with Cell = target).
- Patrols: death "Caught by a patrol" when `PlayerOccupiedCell == patrol.OccupiedCellAt(Time)` or they swap cells within a frame.
- Decoys: arriving on a decoy: DecoyFound, the decoy is removed for the rest of the attempt, the player is placed at Start, and `RealDestinationRevealRemaining` = 2.5 s.
- Chaser (when `Layout.HasChaser`): spawns at Start at t = 5.0 (ChaserSpawned) and follows the loop-erased trail of cells the player entered (trail starts with Start; entering a cell already in the trail truncates the trail after it), moving one cell per 0.32 s toward the next trail cell after its own position (if its cell was truncated away, it walks back along its own recorded path to the trail). Expose `bool ChaserActive`, `Vector2 ChaserWorldPos`, `Vector2Int ChaserCell`. Death "Caught by the shadow" on occupied-cell collision.
- Tests for each, plus Reset clearing all of it.
- Verify, commit: `Add cell hazards, teleporters, patrols, decoys and chaser to the simulation`.

---

### Task 6: Autopilot and end-to-end level tests (Core)

- `Game/Assets/Scripts/Core/Autopilot.cs`: `Dir? Decide(LevelSim sim)` returning the direction to hold this frame (null = wait). It knows the full layout (including invisible walls). Plan with a time-expanded BFS/A* over states (cell, time step) with time step = the move duration 0.13 s and actions {wait one step, move in 4 directions}, horizon up to 400 steps, using the element timing functions to predict edge passability and hazards at future times (moving walls, phase walls, gates, patrols, chaser treated as dangerous within 1 cell of its predicted trail position, collapse tiles: never wait on an intact collapse tile or pass a collapsed one, avoid invisible walls, avoid decoys, teleporters modeled as jumps). Replan when idle. Add a safety margin so it never starts a crossing that a wall could close mid-step. It may be imperfect.
- Tests: `EndToEndTests.cs`: for levels 1..12 and seeds 1..5, simulate at dt 1/60 with the autopilot, restarting via `Reset()` on death, and assert completion within 240 simulated seconds per level and at most 5 deaths. Keep total test runtime reasonable (< 60 s); report the timings.
- If a failure reveals a generator/sim bug, fix it in the right place with a regression test; if it reveals an autopilot weakness, improve the autopilot.
- Verify, commit: `Add autopilot and end-to-end level completion tests`.

---

### Task 7: World rendering and animation (Game)

Files in `Game/Assets/Scripts/Game/`:
- `SpriteFactory.cs`: procedural cached sprites (white, tintable): Square, RoundedRect (for walls), Circle, Ring, Star (5-point), Blob (player body), Eye, Spike orb, Arrow chevron, Swirl, Crack overlay, Plate. Copying the approach from `/Users/manasvardhan/Desktop/RuleShift/Game/Assets/Scripts/Game/SpriteFactory.cs` is encouraged.
- `CameraFit.cs`: fits the camera to an N x N maze plus 0.6 margin at 16:9 leaving a HUD band of about 12% of the screen height on top (offset the camera down accordingly).
- `MazeView.cs`: `Build(LevelSim sim)` creates all visuals for the layout (clears previous level): floor checker, visible walls (skip invisible walls), Start and Destination pads, decoys, memory tiles, collapse tiles, trigger plates and their linked walls, teleporter pads and target rings, rotating gates, one-way arrows, moving walls, phase walls, patrols, chaser. `Sync(LevelSim sim)` every frame updates everything from sim state: moving wall slide position and warning flash, phase wall opacity and flicker, trigger wall grow/shrink (0.3 s tween on toggle), gate angle, collapse tile shake/fall/pit and restore, revealed invisible walls (red dashed, fading in the last 0.5 s), decoy removal, destination rotation and reveal pulse, patrol positions, chaser position and fade-in.
- `PlayerView.cs`: blob at `sim.PlayerWorldPos`, eyes toward `Facing`, squash on step, bump nudge, death shrink + red flash (driven by the flow in Task 8 via a method), teleport sparkle.
- `Effects.cs`: simple pooled particle-like bursts using sprites (completion burst at Destination, teleport sparkle, collapse dust, decoy shatter) and camera shake.
- Colors exactly as the spec's Visual style. Sorting orders: floor 0, tiles 1, pads 2, walls 5, enemies 8, player 10, effects 12.
- Bootstrap: for now create a LevelSim for level 1 seed 1 and a debug driver that reads keyboard input so the build is playable to walk the maze (replaced in Task 8).
- Verify: tests pass; `tools/build-webgl.sh` succeeds. Commit: `Add maze world rendering and animations`.

---

### Task 8: Game flow, HUD, audio and demo mode (Game)

- `GameFlow.cs`: states Title, IntroCard, Playing, Dying, LevelComplete, Paused per the spec's Flow section (texts verbatim): run seed on start; builds each level via `LevelGenerator.Generate(level, runSeed * 1000 + level)`; intro card when `NewMechanics` is non-empty; Dying 0.9 s (death cause shown, then `sim.Reset()`, attempt deaths +1); LevelComplete 1.5 s then next level; R restarts the attempt; P/Esc pause; best level saved with `PlayerPrefs.Save()`; total run time.
- `KeyboardInput.cs` (held direction with most-recently-pressed priority) and an `IDirectionSource` interface; `AutopilotSource` wraps `Core.Autopilot`.
- `Hud.cs` (IMGUI scaled to 960x540 reference): title screen, intro card, HUD band (LEVEL L, N x N, deaths, time), legend of present mechanics (swatch + name), death cause banner, LEVEL COMPLETE banner, pause overlay, DEMO tag when demo is on. No per-frame allocations of styles/textures.
- `Sfx.cs`: synthesized clips per the spec's Audio section, triggered from SimEvents and flow events; warning ticks only within 4 cells; M mutes.
- `DemoMode.cs`: URL `demo=1` or F2; autopilot input; auto-start from Title after 1.5 s; dismiss intro cards after 1.5 s.
- Remove the Task 7 debug driver.
- Verify: tests pass; build succeeds. Commit: `Add game flow, HUD, audio and demo mode`.

---

### Task 9: Browser verification and bug fixing

- Build, serve `docs/` with `python3 -m http.server 8766 --directory docs` in the background.
- `tools/web/` Node package with Playwright (`npm i -D playwright`, `npx playwright install chromium` if needed). `tools/web/verify.mjs`: loads the page, waits for `Trust No Wall booted`, collects console errors, screenshots title, starts a run, plays with keys for a few seconds, then loads `/?demo=1` and lets the autopilot run for ~120 s taking screenshots every 6 s into `test-results/web/`. Fails on console errors or exceptions.
- Inspect every screenshot (Read tool) against the spec's Visual style and Flow: maze fully visible and centered, HUD not overlapping the maze, Start/Destination clear, every mechanic visible and readable, intro cards, death and complete banners. Confirm the demo reaches at least level 5.
- Fix what is wrong (with tests for Core fixes), rebuild, re-verify. Report each issue and fix and list the final screenshots.
- Commit fixes and rebuilt docs. Subject e.g. `Fix issues found in browser verification`.

---

### Task 10: Documentation, diagrams and Discord post

- `design/DesignDocument.md`: the descriptive document for the course (sections: Logline (Genre + Twist); Genre tropes research on at least three maze games with a shared-tropes list; Twist and justification (the genre's tropes assume a static, trustworthy maze to be solved by observation; Trust No Wall makes the maze itself deceptive and reactive, while guaranteeing solvability and keeping each deception rule learnable, and resetting to the same layout on death so memory becomes a skill); Short prototype description; Controls; Mechanics matrix table with columns Mechanics, Description, Interaction with Twist, Affected Genre Elements, Type of Genre Innovation, Supports, one row per mechanic plus Maze Growth and Same-Layout Restart rows; Level progression table; GitHub repo placeholder `https://github.com/ManasVardhan/trust-no-wall` and Pages link `https://manasvardhan.github.io/trust-no-wall/`; Individual contributions with `[Add your main contributions here]` placeholders for Qiming Xiao and Manas (do not invent contributions); Diagrams section). Use WebSearch to ground the research on real maze games (for example Pac-Man, Maze Craze, and a deceptive-design game like Level Devil) and keep claims accurate.
- `design/diagrams/`: `game-loop.svg/.png` (Title -> Level intro -> Playing -> Dying -> restart same layout / LevelComplete -> next level with N+1) and `level-generation.svg/.png` (perfect maze -> braid -> choose mechanics by level -> place each element -> validator accept/reject -> layout). Hand-written dark-theme SVG; render PNG at 2x with Playwright from `tools/web`.
- `design/discord-post.md` (under 2000 chars): title, links (video, doc, playable; Pages URL above, others as placeholders), logline, description, controls, team (Qiming Xiao, Manas Vardhan).
- `README.md`: pitch, play link, controls, mechanics list, level progression, how to run tests and build, project layout, team.
- No em dashes. Commit: `Add design document, diagrams, README and Discord post`.

---

### Task 11: Gameplay video

- `tools/web/record-video.mjs`: serve `docs/`, open `/?demo=1` at 1280x720 with Playwright `recordVideo`, record about 58 s starting at the title screen. The autopilot should show several levels, intro cards, at least one death-and-restart if it happens naturally, and level-complete banners. Take the best of up to 3 takes (most levels reached).
- ffmpeg to `video/trust-no-wall-gameplay.mp4` (H.264, yuv420p, 30 fps, CRF 23, <= 59 s). Optional 2 s title card via drawtext ("TRUST NO WALL: the maze is lying to you") using `/System/Library/Fonts/Supplemental/Arial Bold.ttf`, total <= 59 s.
- Extract 6 frames with ffmpeg and view them to confirm the content. Report duration via ffprobe.
- Commit the mp4 and script. Subject: `Add gameplay video`.
