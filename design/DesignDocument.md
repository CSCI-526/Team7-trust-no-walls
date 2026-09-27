# Trust No Wall: Design Document

Team: Qiming Xiao, Manas Vardhan

## Logline

Trust No Wall is a 2D top-down maze game (genre: maze / maze-chase) whose twist is that the maze itself cannot be trusted: walls hide, slide, vanish, rewire and lie about where the exit is, yet every layout is guaranteed solvable and every lie follows a fixed, learnable rule, so remembering the maze's tricks (not just its shape) is how you win.

## Genre research: shared tropes of maze games

We looked at three games from the maze genre, spanning its history from the arcade era to modern browser trial-and-error platformers:

- **Pac-Man** (Namco, designed by Toru Iwatani, arcade, 1980). A single player navigates a fixed, fully visible maze, collecting pellets while avoiding four patrol-pattern ghosts. The maze layout never changes: what you see is exactly what is there, and mastery comes from memorizing ghost movement, not the maze's geometry.
- **Maze Craze: A Game of Cops 'n Robbers** (Atari, developed by Rick Maurer, Atari 2600). Two players race through a randomly generated top-down maze. Several of its game variants toggle novelty rules on top of the base maze, including one where the maze is partially or fully invisible and only briefly revealed, and one ("Blockcade") where a player can drop a false wall to trick their opponent. These variants are optional modes layered on an otherwise trustworthy maze, not the constant condition of play.
- **Level Devil** (Unept (Adam Corey), browser release 2023, PC/mobile release March 2025). A minimalist platformer built entirely around trial-and-error: floors that are secretly spikes, ceilings that suddenly fall, platforms that disappear the moment you commit to a jump. There is no telegraphing on a first attempt; the whole design is that the level actively deceives you and punishes trust, and progress is made purely by dying, memorizing, and retrying the identical level.

**Shared tropes across the genre:**
1. The maze (or level) is treated as a stable, observable puzzle: what you can see is assumed to be the truth of the space, so skilled play means reading the visible layout correctly.
2. Any hidden or falsified information (Maze Craze's invisible-maze variant) is an optional novelty mode bolted onto an otherwise honest maze, not a persistent core mechanic that the whole game is built around.
3. Deception, when it is the whole point (Level Devil), is usually divorced from spatial maze-solving and from any fairness guarantee: there is no check that a level stays completable, and a "trap" can just be an unavoidable first-try loss with no visible rule to learn from.
4. Failing sends the player back to try the exact same, unchanged challenge again: the level or maze itself does not regenerate, only the player's knowledge of it improves.

## Twist and justification

**Twist:** The genre's tropes assume a static, trustworthy maze to be solved by observation. Trust No Wall breaks that assumption directly: the maze is deceptive (invisible walls, a decoy goal, one-way corridors that look open both ways) and reactive (walls that slide, vanish, rewire on a trigger, and a shadow that reacts to your own trail). But two guarantees keep it a fair maze game rather than a troll game: every generated layout is checked by a solvability validator before it is presented, so a completable route always exists; and every deception follows a fixed, telegraphed rule (a flash before a wall slides, a color code linking a plate to its walls, a spinning star marking the real goal, a timed cycle for anything that opens and closes). Dying restarts the same layout, seed and all, so a lie you die to once becomes a rule you can plan around, not a fresh unfair surprise.

**Why this is innovative relative to the researched tropes:** It is a **subversion** of trope 1 (Pac-Man's assumption of an honest, fully visible maze) and trope 2 (Maze Craze's treatment of hidden information as an occasional side-mode): here, deception is the default, constant condition of the whole game rather than a special variant. It is a **combination** of Maze Craze's hidden-maze idea with Level Devil's committed trial-and-error deception, applied to spatial maze navigation instead of platforming reflexes. And it directly answers Level Devil's weak point (trope 3) by extending the genre with a solvability guarantee and telegraphed, learnable rules for every hazard, so the deception is always fair. Trope 4 (restart to the same challenge) is kept and leaned into on purpose: because the layout does not change on death, the escalating deception stays legible and memory of the maze's specific lies becomes the actual skill the player is building, which is the throughline that ties every mechanic below back to the twist.

## Short prototype description

The prototype is a WebGL build of Trust No Wall playable in a browser. Starting from the title screen, the player descends into a procedurally generated maze that grows by one cell in each dimension every level, and that introduces one or two new kinds of lying or reactive wall at fixed levels: first invisible walls (mitigated by a memory-reveal tile), then walls that slide and disappear, then collapsing floor tiles and rewiring trigger plates, then teleporters and rotating gates, then patrol hazards and one-way corridors, then a decoy destination, and finally a chaser that hunts the player's own trail. Every level is validated to remain solvable before it is shown, deaths restart the same layout with the player back at Start, and the run tracks best level reached across attempts.

## Controls

| Input | Action |
|---|---|
| WASD / Arrow keys | Move (grid steps; hold a direction to keep stepping) |
| Space / Enter | Start the game from the title screen, or skip the current intro card |
| R | Restart the current attempt (same layout) |
| P / Esc | Pause |
| M | Mute audio |
| F2 | Toggle the demo autopilot |

## Mechanics matrix

| Mechanics | Description | Interaction with Twist | Affected Genre Elements | Type of Genre Innovation | Supports |
|---|---|---|---|---|---|
| Invisible Walls | A subset of the maze's interior walls is not drawn; walking into one is instantly fatal. | Embodies "the maze is lying" literally: the map the player can see omits real walls, so sight alone cannot be trusted. | Wall/collision reliability; the maze-as-honest-map trope. | Subversion | Deception; Memory-as-skill |
| Memory Tiles | Entering a blue tile reveals every invisible wall within Chebyshev distance 3 for 3.0 s (glowing red dashed, fading in the last 0.5 s); re-entering refreshes it. | The counterbalance that keeps the central lie fair: it turns pure guesswork into an information mechanic the player can re-trigger on demand. | Visibility/information systems; exploration pacing. | Combination | Learnable rules; Solvability fairness |
| Moving Walls | A wall slides between two parallel open edges one cell apart: dwells 3.0 s, flashes 0.6 s, then slides over 0.4 s, with the blocking edge switching at the slide's midpoint; phase is random per wall. | The maze physically rearranges itself mid-run, and a mistimed crossing crushes the player, making a corridor's danger a function of time as well as space. | Static-corridor assumption; timing/pattern reading. | Extension | Reactivity; Memory-as-skill |
| Disappearing Walls | A wall is solid for 3.0 s then gone for 2.0 s, cycling, flickering for the last 0.6 s before it returns; half open shortcuts through the base maze, half sit on the intended route and must be timed. | Subverts the idea that a solid wall is permanent and that a passable gap stays open, forcing the player to track two different lies on the same corridor. | Wall permanence; shortcut trust. | Subversion | Reactivity; Deception |
| Collapsing Tiles | A cracked floor tile starts an 0.8 s collapse once stepped on; if the player is still on it at collapse they fall, and the resulting pit kills on entry too. Half restore after 4.0 s, half stay collapsed for the attempt. | Extends the lie from "what blocks you" to "what holds you up," turning the floor itself into unreliable terrain. | Floor/ground trust; a hazard borrowed from platformers. | Combination | Reactivity; Solvability guarantee |
| Trigger Walls | A color-linked pressure plate toggles 1 to 2 edges open or closed (0.3 s grow/shrink) every time it is stepped on, and can crush the player mid-toggle. | The player's own movement reshapes the maze, so exploring can seal off the very path just walked. | Level topology; cause-and-effect puzzle design. | Combination | Reactivity; Learnable rules |
| Teleport Tiles | A purple pad teleports the player to a marked target 0.15 s later; targets are random reachable cells regenerated each level, some closer to the goal, some farther. | Undercuts the assumption that a pad or shortcut helps: a teleporter can set the player back just as easily as forward. | The warp-tunnel trope; progress trust. | Subversion | Deception |
| Rotating Barriers | A gate rotates 90 degrees every 2.5 s (0.3 s animation), alternating which pair of opposite edges of its cell can be crossed. | A junction that looks like a normal open cell is secretly only half-open at any moment, on a fixed and readable clock. | Junction/intersection trust. | Extension | Reactivity; Learnable rules |
| Patrolling Obstacles | A red spiked orb ping-pongs one cell per 0.45 s along a dead-end branch off the main route, crossing the route's junction cell. | Escalates the classic maze-chase enemy trope into a side-corridor timing hazard that punishes careless side trips. | Enemy avoidance/patrol trope (as with Pac-Man's ghosts). | Emphasis | Reactivity; Memory-as-skill |
| One-Way Paths | An open edge marked with an arrow can only be crossed in that arrow's direction. | A corridor that looks fully open is secretly half-closed, while the validator still guarantees a directed route to the goal exists. | Bidirectional-corridor assumption. | Subversion | Deception; Solvability guarantee |
| Decoy Destination | A fake goal sits at the end of the longest dead-end branch; reaching it shatters the decoy, teleports the player to Start, and pulses the real Destination for 2.5 s. The real star slowly rotates; the decoy's does not. | Attacks trust in the visible goal itself, the strongest form of the maze's deception, while leaving one fair, observable tell. | Goal clarity; the "you can see the exit" trope. | Subversion | Deception; Learnable rules |
| Chasing Hazard | A shadow spawns at Start 5.0 s into the attempt and follows the player's own loop-erased trail at one cell per 0.32 s, cutting its trail short whenever the player revisits a cell. | The maze's reactivity culminates in a hazard that reacts to the player's own choices rather than a fixed script, escalating late-game tension. | Chase-enemy trope (as with Pac-Man's ghosts); pathfinding pressure. | Combination | Reactivity |
| Maze Growth | Each level's maze is N x N with N = L + 3, one cell larger in each dimension than the level before, unlocking new mechanics on a fixed schedule. | Scales the maze's capacity to hide and rearrange lies as the player's skill grows, keeping the deception meaningful rather than trivial. | Difficulty curve; procedural generation as a genre standard. | Extension | Escalating tension; pacing of learnability |
| Same-Layout Restart | Dying resets all dynamic state (timers, triggers, collapsed tiles) but regenerates the exact same layout for the level's seed; only the player's knowledge carries forward. | This is what makes every lie fair: a rule you die to once becomes a rule you can plan around next attempt, turning deception into a memory test rather than blind luck. | The restart-to-same-challenge convention, applied here to a procedurally generated maze rather than a handcrafted one. | Combination | Memory-as-skill; Learnable rules |

## Level progression

| Level(s) | Grid | New mechanics introduced | Notes |
|---|---|---|---|
| 1 | 4x4 | None | Plain maze, movement tutorial. |
| 2 | 5x5 | Invisible Walls, Memory Tiles | Always introduced together. |
| 3 | 6x6 | Moving Walls, Disappearing Walls | Plus one earlier mechanic at half count. Braiding (loop passages) begins at level 4, so level 3 is still a perfect maze. |
| 4 | 7x7 | Collapsing Tiles, Trigger Walls | Plus one earlier mechanic at half count. Braiding begins: `floor(0.06 * N * N)` extra walls removed. |
| 5 | 8x8 | Teleport Tiles, Rotating Barriers | Plus one earlier mechanic at half count. |
| 6 | 9x9 | Patrolling Obstacles, One-Way Paths | Plus one earlier mechanic at half count. |
| 7 | 10x10 | Decoy Destination | Plus one earlier mechanic at half count. |
| 8 | 11x11 | Chasing Hazard | Plus one earlier mechanic at half count. All 12 mechanics unlocked from here on. |
| 9-10 | 12x12, 13x13 | None (remix) | 4 random unlocked mechanics per level. |
| 11 | 14x14 | None (remix) | 4 random unlocked mechanics; Decoys increase to 2. |
| 12+ | 15x15 and up | None (remix) | 5 random unlocked mechanics per level; grid grows by 1x1 every level indefinitely. |

Mechanic counts scale with grid area `A = N*N` and interior wall count `W` per the spec (for example Invisible Walls = `round(0.25 W)`, Chaser and Decoys are fixed at 1, or 2 for Decoys from level 11). Invisible Walls and Memory Tiles are always unlocked together.

## Links

- Repository: https://github.com/ManasVardhan/trust-no-wall
- Play online: https://manasvardhan.github.io/trust-no-wall/

## Individual contributions

- Qiming Xiao: [Add your main contributions here]
- Manas Vardhan: [Add your main contributions here]

## Diagrams

See `design/diagrams/`:

- `game-loop.svg` / `game-loop.png`: the per-attempt and per-level flow, from Title through the intro card, Playing, Dying (restart to the same layout) or Level Complete (advance to level N+1).
- `level-generation.svg` / `level-generation.png`: the level generation pipeline, from the seeded perfect maze through braiding, mechanic selection by level, per-element placement, and the solvability validator's accept/reject loop, to the final layout.
