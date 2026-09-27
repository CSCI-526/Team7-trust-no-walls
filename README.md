# Trust No Wall

A 2D top-down maze game where the maze itself is lying to you. Reach the Destination from the Start Point, but hidden walls, walls that move and vanish, floors that collapse, triggers that rewire the layout, teleporters, a fake goal and a shadow that hunts your trail all stand in the way. Every layout is guaranteed solvable and every deception follows a fixed, learnable rule, so dying and restarting the identical layout turns memory into the skill that gets you through.

**Play online:** https://csci-526.github.io/Team7-trust-no-walls/

## Controls

| Input | Action |
|---|---|
| WASD / Arrow keys | Move (grid steps; hold a direction to keep stepping) |
| Space / Enter | Start from the title screen, or skip the current intro card |
| R | Restart the current attempt (same layout) |
| P / Esc | Pause |
| M | Mute audio |
| F2 | Toggle the demo autopilot |

## Mechanics

- **Invisible Walls** (level 2): a subset of interior walls is not drawn; walking into one is fatal.
- **Memory Tiles** (level 2): reveal nearby invisible walls for a few seconds.
- **Moving Walls** (level 3): slide between two edges on a timer; can crush.
- **Disappearing Walls** (level 3): cycle solid and open; can crush.
- **Collapsing Tiles** (level 4): crack and fall after being stepped on; the resulting pit is fatal.
- **Trigger Walls** (level 4): pressure plates that open or close linked walls; can crush.
- **Teleport Tiles** (level 5): teleport the player to a target cell, not always closer to the goal.
- **Rotating Barriers** (level 5): gates that rotate which sides can be crossed.
- **Patrolling Obstacles** (level 6): hazards that ping-pong along side branches; fatal on contact.
- **One-Way Paths** (level 6): edges that can only be crossed in one direction.
- **Decoy Destination** (level 7): a fake goal that sends the player back to Start.
- **Chasing Hazard** (level 8): a shadow that spawns partway through the attempt and follows the player's trail; fatal on contact.

Full mechanic-by-mechanic detail, including how each one interacts with the game's twist, is in [`design/DesignDocument.md`](design/DesignDocument.md).

## Level progression

Level `L` has an `N x N` maze with `N = L + 3`, growing by one cell in each dimension every level. Levels 2 through 8 each introduce one or two new mechanics (see the table above) at full count, plus, from level 3 on, one earlier mechanic at half count. From level 9 on, each level draws 4 random unlocked mechanics (5 from level 12), with Invisible Walls and Memory Tiles always unlocked together. See [`design/DesignDocument.md`](design/DesignDocument.md) for the full level-by-level table and the mechanic count formulas.

## Running tests and building

Requires the Unity 6000.6.0f1 (arm64) editor installed at `/Applications/Unity/Hub/Editor/6000.6.0f1-arm64/Unity.app`.

```
tools/run-tests.sh      # runs the EditMode test suite, prints a pass/fail summary
tools/build-webgl.sh    # builds the WebGL player into docs/, for GitHub Pages
```

Both scripts exit non-zero on failure. Test results land in `test-results/`, logs in `build-logs/` (both git-ignored).

To re-render the hand-written SVG diagrams in `design/diagrams/` to PNG (used by the design document):

```
cd tools/web
npm i
npx playwright install chromium
cd ..
node tools/render-svg.mjs design/diagrams/game-loop.svg design/diagrams/level-generation.svg
```

## Project layout

```
Game/                       Unity project (all runtime and editor code, tests)
  Assets/Scripts/Core/      Pure C# simulation: maze generation, level design, sim step, autopilot
  Assets/Scripts/Game/      Unity layer: rendering, input, effects
  Assets/Editor/            Build tooling
  Assets/Tests/EditMode/    EditMode test suite
docs/                       WebGL build output, served via GitHub Pages
design/                     Design document, diagrams, Discord post
planning/                   Game spec and implementation plan
tools/                      Test runner, build script, SVG-to-PNG renderer
tools/web/                  Node/Playwright tooling for rendering diagrams
video/                      Gameplay video
```

## Team

Qiming Xiao, Manas Vardhan

## Repository

https://github.com/ManasVardhan/trust-no-wall
