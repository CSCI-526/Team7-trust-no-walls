# Trust No Wall: Game Spec

A 2D top-down maze game. Reach the Destination from the Start Point. Every level the maze grows by one in each dimension, and the maze itself lies to you: hidden walls, walls that move, vanish, rotate, floors that collapse, triggers that rewire the layout, teleporters, decoys, patrols and a chaser that follows your trail.

This spec fills every gap in the brief with the team's recommended defaults. It is binding for implementation.

## Platform and tech (same stack as the team's previous prototype)
- Unity 6000.6.0f1 arm64 editor at `/Applications/Unity/Hub/Editor/6000.6.0f1-arm64/Unity.app/Contents/MacOS/Unity`, Built-in render pipeline, 2D orthographic camera.
- WebGL build hosted on GitHub Pages from `docs/` on `main`. Compression Disabled.
- Legacy Input Manager, keyboard only. All objects created from code at runtime (no prefabs, no imported art). Procedural sprites (`Texture2D`), IMGUI UI, synthesized audio.
- The whole game simulation is pure C# in `Assets/Scripts/Core` (namespace `TrustNoWall.Core`): maze generation, level design, the simulation step, and the autopilot. It is covered by EditMode tests, including end-to-end tests where the autopilot completes generated levels. The Unity layer (`Assets/Scripts/Game`, namespace `TrustNoWall.Game`) only renders simulation state, reads input and plays effects.

## Grid model
- Level `L` (starting at 1) has an `N x N` maze with `N = L + 3`.
- Cells `(x, y)`, `0 <= x, y < N`, y up. Cell size 1 world unit; maze centered at the origin.
- Walls live on edges between adjacent cells. The outer boundary is always a solid visible wall.
- Base maze: a perfect maze from a seeded recursive backtracker (iterative DFS). From level 4 on, braid it: remove `floor(0.06 * N * N)` extra random interior walls to create loops.
- Start = cell (0, 0). Destination = the cell with the greatest BFS distance from Start in the base (pre-braid) maze; ties broken by lowest index.
- Levels are generated from `seed = runSeed * 1000 + L` (runSeed random per title-screen start). Dying restarts the SAME layout (the brief's "memory" pillar): all dynamic state resets, the layout does not change.

## Player movement
- Grid-step movement. While a direction is held (WASD or Arrows) and the player is idle, they attempt a step to the neighbor cell. A step takes 0.13 s (smooth interpolation). If a direction is still held when the step ends, the next step begins immediately. The most recently pressed held key wins.
- A step across an edge is blocked (a "bump": no movement, small shake, bump sound, 0.12 s lockout) when the edge is a visible solid wall, a closed phase wall, a moving wall currently on that edge, a closed trigger wall, a one-way edge in the wrong direction, or a rotating gate that does not allow that side.
- A step into an invisible wall is fatal (it is the brief's rule).
- During a step the player is "crossing" the edge. If that edge becomes blocked while crossing (a phase wall turns solid, a moving wall arrives, a trigger wall closes) the player dies (crushed).
- The player occupies `from` for the first half of a step and `to` for the second half, for collision with patrols and the chaser.

## Obstacles (all 13 from the brief, some merged)
Each obstacle has an intro level. Numbers are binding.

| Mechanic | Intro | Behavior | Fatal? |
|---|---|---|---|
| Invisible Walls | 2 | A subset of the maze's interior walls is not drawn. Walking into one kills. | Yes |
| Memory Tiles | 2 | Blue tile. Entering it reveals every invisible wall within Chebyshev distance 3 of the tile for 3.0 s (drawn as glowing red dashed walls fading out in the last 0.5 s). Re-entering refreshes it. | No |
| Moving Walls | 3 | A wall that slides like a sliding door between two collinear neighboring edges along its own line (same orientation, adjacent along that line; both open passages in the base maze). It dwells 3.0 s, then slides over 0.4 s; its blocking edge switches at the slide's midpoint. It flashes toward white for 0.6 s before sliding, with a chevron showing the slide direction; a faint ghost outline and a track line mark its empty slot and the line it slides along. Phase offset random per wall. | Crush |
| Disappearing Walls (also covers "Temporary Safe Paths") | 3 | A wall on an edge that is solid for 3.0 s and gone for 2.0 s, cycling. It flickers for the last 0.6 s before it returns, with a tick sound. Half are placed on base-maze walls (they open temporary shortcuts), half on passages of the Start-to-Destination route (the player must time them). Random phase offset. | Crush |
| Collapsing Tiles | 4 | Cracked-looking tile. Entering it starts a 0.8 s collapse; if the player is still on it at collapse, they fall. After collapse it is a pit: stepping into a pit kills. 50% are temporary (restore after 4.0 s), 50% permanent for the attempt. | Yes |
| Trigger Walls | 4 | A pressure plate linked (same color) to 1 to 2 edges. Each time the player enters the plate, every linked edge toggles open/closed (0.3 s grow/shrink animation). Linked edges are either base walls that open (reveal hidden corridors) or loop passages that close. | Crush |
| Teleport Tiles | 5 | A purple pad and its target marker. Arriving on the pad teleports the player to the target 0.15 s later. Targets are random reachable cells, some closer to the Destination, some farther. Regenerated each level. | No |
| Rotating Barriers | 5 | A gate cell with a bar that rotates 90 degrees every 2.5 s (0.3 s animation). Horizontal bar: the cell can only be entered or left through its east/west edges. Vertical: north/south. | No |
| Patrolling Obstacles | 6 | A red spiked orb that ping-pongs along a dead-end branch that hangs off the Start-to-Destination route, crossing the junction cell. One cell per 0.45 s. | Yes |
| One-Way Paths | 6 | An open edge with an arrow. It can only be crossed in the arrow's direction. | No |
| Decoy Destination (also covers "Fake Paths") | 7 | Looks like the Destination but sits at the end of the longest dead-end branch. Reaching it: the decoy shatters, the player is teleported back to Start and the real Destination pulses for 2.5 s. Subtle clue: the real Destination's inner star slowly rotates; a decoy's does not. | No |
| Chasing Hazard | 8 | A shadow that appears at Start 5.0 s after the attempt begins and follows the player's loop-erased trail (when the player revisits a trail cell the trail is cut back to that cell) at one cell per 0.32 s. | Yes |

Deaths show a cause: "Walked into an invisible wall", "Crushed by a moving wall", "Crushed by a disappearing wall", "Crushed by a trigger wall", "Fell through a collapsing tile", "Fell into a pit", "Caught by a patrol", "Caught by the shadow".

## Level progression
- Level 1: plain maze.
- Levels 2 and 3 contain ONLY the mechanics introduced at that level (table above) at full count, so a new mechanic's first two levels are never mixed with anything older.
- Levels 4 to 8: the mechanics introduced at that level (table above) at full count, plus one random earlier-introduced mechanic at half count (rounded up, minimum 1).
- Level 9 and up: 4 random unlocked mechanics, 5 from level 12. Invisible Walls always come with Memory Tiles, and vice versa.
- Counts (A = N*N, W = number of interior walls in the braided maze): Invisible Walls `round(0.25 W)`; Memory Tiles `max(1, round(N / 3))`; Moving Walls `max(1, round(A / 20))`; Disappearing Walls `max(2, round(A / 20))`; Collapsing Tiles `max(2, round(A / 10))`; Trigger plates `1 + floor(A / 60)`; Teleporter pairs `1 + floor(A / 80)`; Rotating Barriers `max(1, round(A / 30))`; Patrols `1 + floor(A / 60)`; One-Way edges `max(1, round(A / 25))`; Decoys `1`, `2` from level 11; Chaser `1`.
- Placement rules: nothing on Start or Destination or the 4 neighbors of Start; at most one cell element per cell and one edge element per edge; a placement that cannot satisfy its rules after 40 tries is skipped.

## Solvability (never generate impossible levels)
After each element is placed, a validator builds a conservative directed graph and rejects the element if the check fails:
- Visible and invisible walls block. Trigger-linked edges block in both states (worst case). Permanent collapsing tiles are removed cells. Periodic blockers (moving walls, disappearing walls, rotating gates, patrols, temporary collapsing tiles) are passable (waiting always works). One-way edges are directed. A teleporter pad's only exit is its target. Decoys send to Start.
- Check: let R be the cells reachable from Start and D the cells that can reach the Destination. Require Destination in R and R subset of D (no traps anywhere reachable).
- Additionally, the EditMode end-to-end suite runs the autopilot on levels 1 to 12 over several seeds and requires completion within a time budget.
- R restarts the current attempt at any time (safety net).

## Flow
- Title: "TRUST NO WALL", tagline "The maze is lying to you.", controls, best level reached, "Press SPACE to start".
- Intro card at the start of a level that introduces a mechanic: "NEW: <name>" plus a one-line explanation per new mechanic, shown for up to 4 s or until Space/Enter. Input frozen while shown.
- Playing: HUD shows "LEVEL L", "N x N", attempt deaths, total run time, and a small legend of the mechanics present in this level (colored swatch + name).
- Death: movement stops, player flashes red and shrinks over 0.6 s with shake and a death sound, the cause is shown, then the attempt restarts (same layout) with the player at Start.
- Completion: movement disabled, burst effect at the Destination, "LEVEL L COMPLETE" banner for 1.5 s, then level L+1 is generated and begins (with its intro card if any).
- P or Esc pause. R restart attempt. M mute. Best level saved in PlayerPrefs `TrustNoWall.BestLevel` (with `PlayerPrefs.Save()`).
- Demo mode: URL containing `demo=1` or F2 toggles an autopilot that plays through the same input path; it auto-starts from the title and dismisses intro cards after 1.5 s. Used to record the gameplay video.

## Visual style
- Background `#0E1016`, floor tiles `#1A1D27` with a subtle checker `#1E2230`, visible walls `#D8DCE8` (0.12 thick, rounded ends).
- Player: bright cyan `#4DE1FF` rounded blob with two eyes looking in the move direction, squash on step.
- Start: green ring pad `#3DDC84` labeled "S". Destination: gold `#FFC857` star pad with slowly rotating inner star and a glow pulse. Decoy: same gold, non-rotating star.
- Memory tile `#4D8BFF`; collapsing tile: floor with crack lines, shakes while collapsing, pit is `#050608` with a darker rim; trigger plate and its linked walls share one of `#FF8C42`, `#B084F5`, `#3DDC84`; teleporter pad `#C04DFF` swirl and target ring; rotating gate: hub plus bar `#FFB199`; patrol: red `#FF4D5E` spiked orb; chaser: dark purple translucent shadow `#6A3DB8` with eyes; one-way: arrow chevrons `#7FDBFF` on the edge; moving walls `#FF8C42`; disappearing walls `#7FDBFF` (flicker); revealed invisible walls red dashed.
- Animations: wall slides, walls fading in/out, tile crack and fall, teleport sparkle, trigger wall grow/shrink, completion burst, death shrink.
- Camera fits the whole maze with a HUD band on top (orthographic size chosen so the maze plus 0.6 margin fits both width and height at 16:9).

## Audio (synthesized)
Step (soft click), bump (thud), death (falling tone), complete (rising arpeggio), teleport (sweep), trigger (clunk), memory reveal (shimmer), warning tick (for disappearing walls and moving walls about to change, only when within 4 cells of the player), chaser spawn (low drone blip). M mutes.

## Deliverables beyond the game
- `docs/` WebGL build + `.nojekyll`.
- `design/DesignDocument.md` (logline, genre research, twist justification, prototype description, controls, mechanics matrix, contributions placeholders, links), `design/diagrams/` (SVG + PNG), `design/discord-post.md`, `README.md`.
- `video/trust-no-wall-gameplay.mp4` under 60 s.
