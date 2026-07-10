# Level Setup

Current slice: scene-based level setup with grid movement, kitchen, carried food, and pet growth.

## Create a scene level

1. Create a scene, for example `Level_001`.
2. Create an empty GameObject named `Level`.
3. Add `LevelSceneSettings`.
4. Add `SceneLevelBuilder`.
5. Set grid width, height, and cell size on `LevelSceneSettings`.

## Place objects

Create these objects in the scene and move them onto grid positions:

- `PlayerStart`: empty GameObject with `PlayerStart`.
- `Kitchen`: Quad or empty GameObject with `KitchenStation`.
- `Pet`: empty GameObject with `PetBody`.
- `WallBlocker`: one object per blocked cell with `GridWall`.
- `DoorExit`: one exit cell with `DoorExit`.

At Play time, `SceneLevelBuilder` snaps those objects to the grid and creates the runtime player/input.

## Door exits

1. Create a new scene for the next room, for example `Level_002`.
2. Add both scenes to `File > Build Profiles > Scene List`.
3. In `Level_001`, create `GameObject > Oops It Ate > Door Exit`.
4. Select the door and set `Target Scene Name` to `Level_002`.
5. When the player steps onto the door, Unity loads `Level_002`.

Move with WASD or arrow keys.

Interact with `J`:

- Stand on the kitchen and press `J` to carry food.
- Stand next to the pet and press `J` to feed it.
- Each successful feeding fills every empty cell next to the pet. Diagonal cells are filled only when both orthogonal cells forming the corner are also empty.
- If an expanding cell reaches the player, the player is pushed to an available adjacent cell.
- Every 3 seconds, a pet larger than `1x1` burps and loses its latest growth layer (for example, `5x5` becomes `3x3`).
- If the pet cannot grow, it burps and shrinks straight back to `1x1`.
