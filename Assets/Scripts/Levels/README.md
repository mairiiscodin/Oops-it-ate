# Level Setup

Current slice: scene-based level setup with grid movement, kitchen, carried food, and pet growth.

## Create a scene level

1. Create a scene, for example `Level_001`.
2. Create an empty GameObject named `Level`.
3. Add `LevelSceneSettings`.
4. Add `SceneLevelBuilder`.
5. Set grid width, height, and cell size on `LevelSceneSettings`.

## Make a non-rectangular tile map

On `LevelSceneSettings`, fill in `Tile Map` using:

- `.` for a walkable floor tile.
- `#` for a wall tile.
- A space or `_` for empty space outside the map.

The first text line is the top row. The grid width and height are read from the text map automatically at Play time and in the Scene gizmo preview. For example, this creates an L-shaped room:

```text
###____
#..____
#..####
#.....#
#######
```

Leave `Tile Map` empty to keep the original full rectangular grid. `PlayerStart`, `Kitchen`, `Pet`, boxes, and doors are still placed as scene objects on top of the visible tile cells.

## Place objects

Create these objects in the scene and move them onto grid positions:

- `PlayerStart`: empty GameObject with `PlayerStart`.
- `Kitchen`: Quad or empty GameObject with `KitchenStation`.
- `Pet`: empty GameObject with `PetBody`.
- `PushableBox`: optional 1x1 box with `PushableBox`. The player cannot walk through it, and pet growth pushes it to an available adjacent cell.
- `WallBlocker`: one object per blocked cell with `GridWall`.
- `DoorExit`: one exit cell with `DoorExit`.

At Play time, `SceneLevelBuilder` snaps those objects to the grid and creates the runtime player/input.

## Door exits

1. Create a new scene for the next room, for example `Level_002`.
2. Add both scenes to `File > Build Profiles > Scene List`.
3. In `Level_001`, create `GameObject > Oops It Ate > Door Exit`.
4. Select the door and set `Target Scene Name` to `Level_002`.
5. When the player steps onto the door, Unity loads `Level_002`.
6. The door occupies one grid cell. The target scene must be included in `Build Profiles > Scene List`.
7. A door can be placed one cell outside the loaded grid as part of its boundary. Walking toward it changes scene; expanding that boundary moves the door outward with the edge.
8. Feeding a boundary door grows it only along the wall edge, never inward or outward. Every grown door cell changes scene, and the newest growth layer burps away after 3 seconds.

Move with WASD or arrow keys.

Interact with `J`:

- Movement keys also turn the player, even when the cell ahead is blocked.
- Face the kitchen and press `J` to carry food.
- While carrying food, face the pet, kitchen, or a pushable box and press `J` to feed and grow it.
- Kitchen and fed boxes use the same grid-cell growth and burp/shrink rules as the pet. Kitchen blocks movement, so food is collected by facing it.
- Every grown Kitchen cell can provide food, not only its original center cell.
- Face the unloaded space just outside the grid and feed it to load one entire new row or column. If a temporary growth layer contains an authored WallBlocker, shrinking pushes that wall one cell inward instead of deleting it with the layer.
- Expanded grid edges burp after 3 seconds. Before removing the newest row or column, the edge pushes the player, boxes, pets, kitchens, and grown boxes one cell inward. If an object cannot be pushed, shrinking waits at that layer; attached doors move back with the edge.
- Shaped tile maps can also expand at a concave edge. Feeding loads the complete line directly in front of the player: facing left or right loads that column, while facing up or down loads that row. Burping removes only the cells added by that layer and restores the authored shape.
- Each successful feeding fills every empty cell next to the pet. Diagonal cells are filled only when both orthogonal cells forming the corner are also empty.
- If an expanding cell reaches the player, the player is pushed to an available adjacent cell.
- Growth pushes both normal boxes and the complete multi-cell body of boxes that have already been fed; failed moves are rolled back before growth continues.
- If the player cannot be pushed away, growth still fills every valid edge cell except the cell occupied by the player.
- When the player cannot be pushed, growth leaves the player's cell and its corner-adjacent edge cells empty.
- Every 3 seconds, a pet larger than `1x1` burps and loses its latest growth layer (for example, `5x5` becomes `3x3`).
- If the pet cannot grow, it burps and shrinks straight back to `1x1`.
