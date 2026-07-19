using System;
using System.Collections.Generic;
using System.Collections;
using OopsItAte.Grid;
using OopsItAte.Interaction;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OopsItAte.Actors
{
    public readonly struct PetBodyMove
    {
        public PetBodyMove(PetBody body, GridPosition direction)
        {
            Body = body;
            Direction = direction;
        }

        public PetBody Body { get; }
        public GridPosition Direction { get; }
    }

    public sealed class PetBody : MonoBehaviour
    {
        private const int ActorSortingOrderBase = 1000;
        private static readonly int IsFatAnimatorParameter = Animator.StringToHash("IsFat");
        private static readonly int IsHungryAnimatorParameter = Animator.StringToHash("IsHungry");

        [SerializeField] private Color color = new Color(0.25f, 0.9f, 0.35f);
        [SerializeField] private GridPosition origin;
        [SerializeField] private string bodyName = "Pet";
        [SerializeField] private bool canBePushedByBodyGrowth = true;
        [Tooltip("Visual shown while this body occupies exactly one cell (for example DogVisual or OvenVisual).")]
        [FormerlySerializedAs("normalDogVisual")]
        [SerializeField] private GameObject normalVisual;

        [Header("Big Pet Body Tiles")]
        [Tooltip("Fallback sprite for an outer/side body cell.")]
        [SerializeField] private Sprite bigDogSide;
        [Header("Body Tile - Open On 3 Sides")]
        [Tooltip("Tile with only a North neighbour; East, South and West are open.")]
        [SerializeField] private Sprite bigDogOpen3ConnectedNorth;
        [Tooltip("Tile with only an East neighbour; North, South and West are open.")]
        [SerializeField] private Sprite bigDogOpen3ConnectedEast;
        [Tooltip("Tile with only a South neighbour; North, East and West are open.")]
        [SerializeField] private Sprite bigDogOpen3ConnectedSouth;
        [Tooltip("Tile with only a West neighbour; North, East and South are open.")]
        [SerializeField] private Sprite bigDogOpen3ConnectedWest;

        [Header("Body Tile - Corners And Edges")]
        [SerializeField] private Sprite bigDogNE;
        [SerializeField] private Sprite bigDogES;
        [SerializeField] private Sprite bigDogWN;
        [SerializeField] private Sprite bigDogWS;
        [SerializeField] private Sprite bigDogNES;
        [SerializeField] private Sprite bigDogNEW;
        [SerializeField] private Sprite bigDogNSW;
        [SerializeField] private Sprite bigDogESW;

        [Header("Big Pet Face Overlay")]
        [Tooltip("26-frame animation played on the face overlay while the pet is enlarged.")]
        [SerializeField] private AnimationClip bigDogFaceAnimation;
        [HideInInspector]
        [SerializeField] private Sprite bigDogFace;
        [Tooltip("Optional face used when the largest body rectangle is 1 cell wide and 2 cells tall.")]
        [SerializeField] private Sprite bigDogFace1x2Vertical;
        [Tooltip("Optional face used when the largest body rectangle is 2 cells wide and 1 cell tall.")]
        [SerializeField] private Sprite bigDogFace2x1Horizontal;

        [Header("Big Pet Burp Animation")]
        [SerializeField, Min(0.1f)] private float bigDogFaceFallbackDuration = 1.1f;

        private readonly HashSet<GridPosition> bodyCells = new HashSet<GridPosition>();
        private readonly Dictionary<GridPosition, GameObject> visuals = new Dictionary<GridPosition, GameObject>();
        private readonly List<List<GridPosition>> growthLayers = new List<List<GridPosition>>();
        private GridWorld world;
        private Material bodyMaterial;
        private Coroutine burpCoroutine;
        private GameObject bigPetFaceVisual;
        private Animator petAnimator;
        private bool becameFat;
        private GridMover playerMover;
        private PlayerInventory playerInventory;
        private bool isShowingHungryAnimation;
        [HideInInspector]
        [SerializeField] private PetFaceAnimationData bigDogFaceFrameData;

        public bool CanBePushedByBodyGrowth => canBePushedByBodyGrowth;

        public void SetCanBePushedByBodyGrowth(bool canBePushed)
        {
            canBePushedByBodyGrowth = canBePushed;
        }

        public void Initialize(GridWorld gridWorld, GridPosition startPosition)
        {
            Initialize(gridWorld, startPosition, color, bodyName);
        }

        public void Initialize(GridWorld gridWorld, GridPosition startPosition, Color bodyColor, string displayName)
        {
            world = gridWorld;
            origin = startPosition;
            color = bodyColor;
            bodyName = displayName;
            FindNormalVisual();
            petAnimator = normalVisual != null
                ? normalVisual.GetComponentInChildren<Animator>(true)
                : null;
            becameFat = false;
            isShowingHungryAnimation = false;
            playerMover = null;
            playerInventory = null;
            if (bigDogFaceAnimation == null)
            {
                bigDogFaceAnimation = Resources.Load<AnimationClip>("Pet/BigDogFace");
            }
            if (bigDogFaceFrameData == null)
            {
                bigDogFaceFrameData = Resources.Load<PetFaceAnimationData>("Pet/BigDogFaceFrames");
            }
            bodyCells.Clear();
            bodyCells.Add(origin);
            growthLayers.Clear();
            StopBurpTimer();
            SyncAttachedGridObject();
            Redraw();
        }

        private void Update()
        {
            UpdateHungryAnimation();
        }

        public bool IsAdjacentTo(GridPosition position)
        {
            foreach (GridPosition cell in bodyCells)
            {
                int distance = Mathf.Abs(cell.X - position.X) + Mathf.Abs(cell.Y - position.Y);
                if (distance == 1)
                {
                    return true;
                }
            }

            return false;
        }

        public bool Contains(GridPosition position)
        {
            return bodyCells.Contains(position);
        }

        public bool TouchesBoundary(bool verticalBoundary, int coordinate)
        {
            foreach (GridPosition cell in bodyCells)
            {
                if ((verticalBoundary ? cell.X : cell.Y) == coordinate)
                {
                    return true;
                }
            }

            return false;
        }

        public bool WouldOccupyAfterShift(GridPosition position, GridPosition direction)
        {
            foreach (GridPosition cell in bodyCells)
            {
                if ((cell + direction).Equals(position))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryShift(GridPosition direction)
        {
            ClearBlockers();
            var shiftedCells = new HashSet<GridPosition>();
            foreach (GridPosition cell in bodyCells)
            {
                GridPosition target = cell + direction;
                if (world.IsBlocked(target))
                {
                    AddBlockers();
                    return false;
                }

                shiftedCells.Add(target);
            }

            bodyCells.Clear();
            foreach (GridPosition cell in shiftedCells)
            {
                bodyCells.Add(cell);
            }

            origin += direction;
            for (int layerIndex = 0; layerIndex < growthLayers.Count; layerIndex++)
            {
                for (int cellIndex = 0; cellIndex < growthLayers[layerIndex].Count; cellIndex++)
                {
                    growthLayers[layerIndex][cellIndex] += direction;
                }
            }

            foreach (GameObject visual in visuals.Values)
            {
                Destroy(visual);
            }
            visuals.Clear();
            SyncAttachedGridObject();
            Redraw();
            return true;
        }

        internal void SuspendBlockers() => ClearBlockers();
        internal void RestoreBlockers() => AddBlockers();

        private void AddOccupiedCells(HashSet<GridPosition> occupied)
        {
            foreach (GridPosition cell in bodyCells) occupied.Add(cell);
        }

        private bool TryGetOverlap(
            HashSet<GridPosition> currentCells,
            HashSet<GridPosition> newCells,
            out GridPosition overlap)
        {
            foreach (GridPosition cell in bodyCells)
            {
                if (currentCells.Contains(cell) || newCells.Contains(cell))
                {
                    overlap = cell;
                    return true;
                }
            }

            overlap = default;
            return false;
        }

        private bool CanShiftTo(GridPosition direction, HashSet<GridPosition> occupied)
        {
            foreach (GridPosition cell in bodyCells)
            {
                GridPosition target = cell + direction;
                if (world.IsBlocked(target)
                    || (occupied.Contains(target) && !bodyCells.Contains(target)))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryFindGrowthPlan(
            GridPosition playerPosition,
            Func<GridPosition, bool> canPlayerMoveTo,
            out List<GridPosition> growthCells,
            out GridPosition pushDirection,
            out bool shouldPushPlayer,
            out List<PushableBoxMove> boxMoves,
            out List<PetBodyMove> bodyMoves)
        {
            ClearBlockers();
            PushableBox[] boxes = FindObjectsByType<PushableBox>();
            PetBody[] bodies = FindObjectsByType<PetBody>();
            SuspendBoxBlockers(boxes);
            SuspendOtherBodyBlockers(bodies, this);
            growthCells = new List<GridPosition>();
            boxMoves = new List<PushableBoxMove>();
            bodyMoves = new List<PetBodyMove>();
            var candidates = new HashSet<GridPosition>();
            var preferredPushDirections = new Dictionary<GridPosition, GridPosition>();
            bool playerWillBeCovered = bodyCells.Contains(playerPosition);
            pushDirection = default;
            shouldPushPlayer = false;

            try
            {
                foreach (GridPosition cell in bodyCells)
                {
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            if (offsetX == 0 && offsetY == 0)
                            {
                                continue;
                            }

                            var offset = new GridPosition(offsetX, offsetY);
                            GridPosition candidate = cell + offset;
                            if (bodyCells.Contains(candidate)
                                || candidates.Contains(candidate)
                                || !CanExpandInto(cell, offset, candidate))
                            {
                                continue;
                            }

                            candidates.Add(candidate);
                            growthCells.Add(candidate);
                            preferredPushDirections[candidate] = GetPushDirection(cell, candidate);
                            if (candidate.Equals(playerPosition))
                            {
                                playerWillBeCovered = true;
                            }
                        }
                    }
                }

                var growthCellSet = new HashSet<GridPosition>(growthCells);
                var occupiedPositions = new HashSet<GridPosition> { playerPosition };
                for (int i = 0; i < boxes.Length; i++)
                {
                    if (boxes[i].IsPushable)
                    {
                        occupiedPositions.Add(boxes[i].Position);
                    }
                }
                for (int i = 0; i < bodies.Length; i++)
                {
                    if (bodies[i] != this) bodies[i].AddOccupiedCells(occupiedPositions);
                }

                var reservedTargets = new HashSet<GridPosition>();
                if (playerWillBeCovered)
                {
                    preferredPushDirections.TryGetValue(playerPosition, out GridPosition preferredPlayerDirection);
                    shouldPushPlayer = TryFindOutwardPushDirection(
                        playerPosition,
                        preferredPlayerDirection,
                        bodyCells,
                        growthCellSet,
                        target => canPlayerMoveTo(target)
                            && !occupiedPositions.Contains(target)
                            && !reservedTargets.Contains(target),
                        out pushDirection);

                    if (!shouldPushPlayer)
                    {
                        if (bodyCells.Contains(playerPosition))
                        {
                            growthCells.Clear();
                            return false;
                        }

                        growthCellSet.Remove(playerPosition);
                        growthCells.Remove(playerPosition);
                        pushDirection = default;
                    }
                    else
                    {
                        reservedTargets.Add(playerPosition + pushDirection);
                    }
                }

                for (int i = 0; i < boxes.Length; i++)
                {
                    PushableBox box = boxes[i];
                    if (!box.IsPushable
                        || (!bodyCells.Contains(box.Position) && !growthCellSet.Contains(box.Position)))
                    {
                        continue;
                    }

                    preferredPushDirections.TryGetValue(box.Position, out GridPosition preferredBoxDirection);
                    if (!TryFindPushDirection(
                        box.Position,
                        preferredBoxDirection,
                        bodyCells,
                        growthCellSet,
                        target => box.CanMoveTo(target)
                            && !occupiedPositions.Contains(target)
                            && !reservedTargets.Contains(target),
                        out GridPosition boxPushDirection))
                    {
                        growthCells.Clear();
                        boxMoves.Clear();
                        shouldPushPlayer = false;
                        pushDirection = default;
                        return false;
                    }

                    boxMoves.Add(new PushableBoxMove(box, boxPushDirection));
                    reservedTargets.Add(box.Position + boxPushDirection);
                }

                for (int i = 0; i < bodies.Length; i++)
                {
                    PetBody otherBody = bodies[i];
                    if (otherBody == this
                        || !otherBody.TryGetOverlap(bodyCells, growthCellSet, out GridPosition overlap))
                    {
                        continue;
                    }

                    preferredPushDirections.TryGetValue(overlap, out GridPosition preferredDirection);
                    occupiedPositions.UnionWith(growthCellSet);
                    occupiedPositions.UnionWith(reservedTargets);
                    if (!TryFindBodyPushDirection(
                        otherBody,
                        preferredDirection,
                        occupiedPositions,
                        out GridPosition bodyPushDirection))
                    {
                        RemoveGrowthOccupiedByBody(otherBody, growthCells, growthCellSet);
                        continue;
                    }

                    bodyMoves.Add(new PetBodyMove(otherBody, bodyPushDirection));
                    foreach (GridPosition cell in otherBody.bodyCells)
                    {
                        reservedTargets.Add(cell + bodyPushDirection);
                    }
                }

                return growthCells.Count > 0;
            }
            finally
            {
                RestoreBoxBlockers(boxes);
                RestoreOtherBodyBlockers(bodies, this);
                AddBlockers();
            }
        }

        private static void RemoveGrowthOccupiedByBody(
            PetBody body,
            List<GridPosition> growthCells,
            HashSet<GridPosition> growthCellSet)
        {
            foreach (GridPosition occupiedCell in body.bodyCells)
            {
                growthCellSet.Remove(occupiedCell);
                growthCells.Remove(occupiedCell);
            }
        }

        private static bool TryFindBodyPushDirection(
            PetBody body,
            GridPosition preferredDirection,
            HashSet<GridPosition> occupiedPositions,
            out GridPosition pushDirection)
        {
            if (!body.CanBePushedByBodyGrowth)
            {
                pushDirection = default;
                return false;
            }

            if (!preferredDirection.Equals(default)
                && body.CanShiftTo(preferredDirection, occupiedPositions))
            {
                pushDirection = preferredDirection;
                return true;
            }

            pushDirection = default;
            return false;
        }

        private static bool TryFindOutwardPushDirection(
            GridPosition currentPosition,
            GridPosition outwardDirection,
            HashSet<GridPosition> currentBodyCells,
            HashSet<GridPosition> growthCells,
            Func<GridPosition, bool> canMoveTo,
            out GridPosition pushDirection)
        {
            if (outwardDirection.Equals(default))
            {
                pushDirection = default;
                return false;
            }

            GridPosition target = currentPosition + outwardDirection;
            if (!currentBodyCells.Contains(target)
                && !growthCells.Contains(target)
                && canMoveTo(target))
            {
                pushDirection = outwardDirection;
                return true;
            }

            pushDirection = default;
            return false;
        }

        private static void SuspendOtherBodyBlockers(PetBody[] bodies, PetBody except)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != except) bodies[i].SuspendBlockers();
            }
        }

        private static void RestoreOtherBodyBlockers(PetBody[] bodies, PetBody except)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != except) bodies[i].RestoreBlockers();
            }
        }

        private bool CanExpandInto(GridPosition sourceCell, GridPosition offset, GridPosition candidate)
        {
            if (world.IsBlocked(candidate))
            {
                return false;
            }

            if (offset.X == 0 || offset.Y == 0)
            {
                return true;
            }

            GridPosition horizontalSide = sourceCell + new GridPosition(offset.X, 0);
            GridPosition verticalSide = sourceCell + new GridPosition(0, offset.Y);
            return !bodyCells.Contains(horizontalSide)
                && !bodyCells.Contains(verticalSide)
                && !world.IsBlocked(horizontalSide)
                && !world.IsBlocked(verticalSide);
        }

        public bool TryGrow(IReadOnlyList<GridPosition> growthCells)
        {
            ClearBlockers();

            for (int i = 0; i < growthCells.Count; i++)
            {
                GridPosition cell = growthCells[i];
                if (bodyCells.Contains(cell) || world.IsBlocked(cell))
                {
                    AddBlockers();
                    Debug.Log($"{bodyName} cannot grow here.");
                    return false;
                }
            }

            for (int i = 0; i < growthCells.Count; i++)
            {
                bodyCells.Add(growthCells[i]);
            }

            growthLayers.Add(new List<GridPosition>(growthCells));
            Redraw();
            RestartBurpTimer();
            Debug.Log($"{bodyName} expanded into {growthCells.Count} cell(s).");
            return true;
        }

        public bool BurpAndShrink()
        {
            if (bodyCells.Count <= 1)
            {
                Debug.Log($"{bodyName} burped, but it is already 1x1.");
                return false;
            }

            ClearBlockers();
            bodyCells.Clear();
            bodyCells.Add(origin);
            growthLayers.Clear();
            becameFat = true;
            StopBurpTimer();
            Redraw();
            Debug.Log($"{bodyName} burped and shrank to 1x1.");
            return true;
        }

        private void RestartBurpTimer()
        {
            StopBurpTimer();
            burpCoroutine = StartCoroutine(BurpOverTime());
        }

        private void StopBurpTimer()
        {
            if (burpCoroutine == null)
            {
                return;
            }

            StopCoroutine(burpCoroutine);
            burpCoroutine = null;
        }

        private IEnumerator BurpOverTime()
        {
            while (growthLayers.Count > 0)
            {
                yield return PlayBigDogFaceBurpAnimation();

                if (growthLayers.Count == 0)
                {
                    break;
                }

                ClearBlockers();
                List<GridPosition> lastLayer = growthLayers[growthLayers.Count - 1];
                for (int i = 0; i < lastLayer.Count; i++)
                {
                    bodyCells.Remove(lastLayer[i]);
                }

                growthLayers.RemoveAt(growthLayers.Count - 1);
                if (growthLayers.Count == 0)
                {
                    becameFat = true;
                }

                Redraw();
                Debug.Log($"{bodyName} burped and lost one growth layer.");
            }

            burpCoroutine = null;
        }

        private IEnumerator PlayBigDogFaceBurpAnimation()
        {
            Sprite[] frames = bigDogFaceFrameData != null
                ? bigDogFaceFrameData.Frames
                : null;
            if (bigPetFaceVisual != null && frames != null && frames.Length > 0)
            {
                SpriteRenderer faceRenderer = bigPetFaceVisual.GetComponent<SpriteRenderer>();
                float frameDuration = 1f / Mathf.Max(1f, bigDogFaceFrameData.FramesPerSecond);
                for (int frameIndex = 0;
                     frameIndex < frames.Length && bigPetFaceVisual != null;
                     frameIndex++)
                {
                    faceRenderer.sprite = frames[frameIndex];
                    yield return new WaitForSeconds(frameDuration);
                }

                yield break;
            }

            yield return new WaitForSeconds(Mathf.Max(0.1f, bigDogFaceFallbackDuration));
        }

        private static GridPosition GetPushDirection(GridPosition bodyCell, GridPosition playerPosition)
        {
            int x = Mathf.Clamp(playerPosition.X - bodyCell.X, -1, 1);
            int y = Mathf.Clamp(playerPosition.Y - bodyCell.Y, -1, 1);

            if (x != 0)
            {
                return new GridPosition(x, 0);
            }

            return new GridPosition(0, y);
        }

        private static bool TryFindPushDirection(
            GridPosition currentPosition,
            GridPosition preferredDirection,
            HashSet<GridPosition> currentBodyCells,
            HashSet<GridPosition> growthCells,
            Func<GridPosition, bool> canMoveTo,
            out GridPosition pushDirection)
        {
            GridPosition[] directions =
            {
                preferredDirection,
                new GridPosition(-1, 0),
                new GridPosition(0, -1),
                new GridPosition(1, 0),
                new GridPosition(0, 1)
            };

            for (int i = 0; i < directions.Length; i++)
            {
                GridPosition direction = directions[i];
                if (direction.Equals(default))
                {
                    continue;
                }

                GridPosition target = currentPosition + direction;
                if (!currentBodyCells.Contains(target)
                    && !growthCells.Contains(target)
                    && canMoveTo(target))
                {
                    pushDirection = direction;
                    return true;
                }
            }

            pushDirection = default;
            return false;
        }

        private static void SuspendBoxBlockers(PushableBox[] boxes)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i].SuspendBlocker();
            }
        }

        private static void RestoreBoxBlockers(PushableBox[] boxes)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i].RestoreBlocker();
            }
        }

        private void Redraw()
        {
            bool showNormalVisual = normalVisual != null && bodyCells.Count == 1;
            if (petAnimator != null && !showNormalVisual && isShowingHungryAnimation)
            {
                isShowingHungryAnimation = false;
                petAnimator.SetBool(IsHungryAnimatorParameter, false);
            }

            if (normalVisual != null)
            {
                normalVisual.SetActive(showNormalVisual);
                ConfigureNormalVisualSorting();
            }

            // Set the parameter after re-enabling the visual. Animator resets its
            // parameters when its GameObject is reactivated unless state retention
            // is enabled, which previously sent the pet back to Idle here.
            if (petAnimator != null && showNormalVisual)
            {
                petAnimator.SetBool(IsHungryAnimatorParameter, false);
                petAnimator.SetBool(IsFatAnimatorParameter, becameFat);
            }

            // At 1x1 the authored sprite/animation replaces the generated square completely.
            if (showNormalVisual)
            {
                foreach (GameObject visual in visuals.Values)
                {
                    Destroy(visual);
                }

                visuals.Clear();
                DestroyBigPetFaceVisual();
                AddBlockers();
                return;
            }

            if (bodyCells.Count > 1 && UsesBigPetBodySprites())
            {
                RedrawBigPetSprites();
                AddBlockers();
                return;
            }

            DestroyBigPetFaceVisual();

            var removedCells = new List<GridPosition>();
            foreach (GridPosition cell in visuals.Keys)
            {
                if (!bodyCells.Contains(cell))
                {
                    removedCells.Add(cell);
                }
            }

            for (int i = 0; i < removedCells.Count; i++)
            {
                GridPosition cell = removedCells[i];
                Destroy(visuals[cell]);
                visuals.Remove(cell);
            }

            if (bodyMaterial == null)
            {
                bodyMaterial = new Material(FindUnlitShader());
                bodyMaterial.color = color;
            }

            foreach (GridPosition cell in bodyCells)
            {
                if (visuals.ContainsKey(cell))
                {
                    continue;
                }

                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visual.name = $"{bodyName} Body {cell}";
                visual.transform.SetParent(transform);
                visual.transform.position = world.Settings.GridToWorld(cell) + Vector3.back * 0.5f;
                visual.transform.localScale = Vector3.one * world.Settings.cellSize;
                visual.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
                Destroy(visual.GetComponent<Collider>());
                visuals.Add(cell, visual);
            }

            CreateBigPetFaceVisual();
            AddBlockers();
        }

        private void UpdateHungryAnimation()
        {
            if (petAnimator == null)
            {
                return;
            }

            if (playerMover == null)
            {
                playerMover = FindAnyObjectByType<GridMover>();
            }

            if (playerMover != null && playerInventory == null)
            {
                playerInventory = playerMover.GetComponent<PlayerInventory>();
            }

            bool shouldShowHungry = !becameFat
                && bodyCells.Count == 1
                && playerMover != null
                && playerInventory != null
                && playerInventory.HasFood
                && IsAdjacentTo(playerMover.CurrentPosition);
            if (shouldShowHungry == isShowingHungryAnimation)
            {
                return;
            }

            isShowingHungryAnimation = shouldShowHungry;
            petAnimator.SetBool(IsHungryAnimatorParameter, shouldShowHungry);
        }

        private bool UsesBigPetBodySprites()
        {
            return string.Equals(bodyName, "Pet", StringComparison.OrdinalIgnoreCase)
                && (bigDogSide != null
                    || bigDogOpen3ConnectedNorth != null
                    || bigDogOpen3ConnectedEast != null
                    || bigDogOpen3ConnectedSouth != null
                    || bigDogOpen3ConnectedWest != null
                    || bigDogNE != null
                    || bigDogES != null
                    || bigDogWN != null
                    || bigDogWS != null
                    || bigDogNES != null
                    || bigDogNEW != null
                    || bigDogNSW != null
                    || bigDogESW != null);
        }

        private void RedrawBigPetSprites()
        {
            // A cell's artwork can change when a new neighbour grows beside it,
            // so rebuild the small visual set whenever the body shape changes.
            foreach (GameObject visual in visuals.Values)
            {
                Destroy(visual);
            }
            visuals.Clear();
            DestroyBigPetFaceVisual();

            foreach (GridPosition cell in bodyCells)
            {
                Sprite sprite = GetBigPetSprite(cell);
                if (sprite == null)
                {
                    continue;
                }

                GameObject visual = new GameObject($"{bodyName} Body {cell}");
                visual.transform.SetParent(transform);

                float targetSize = world.Settings.cellSize;
                float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
                float scale = spriteSize > 0f ? targetSize / spriteSize : 1f;
                visual.transform.localScale = Vector3.one * scale;

                Vector3 cellCenter = world.Settings.GridToWorld(cell) + Vector3.back * 0.5f;
                visual.transform.position = cellCenter - (Vector3)(sprite.bounds.center * scale);

                SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = ActorSortingOrderBase
                    + Mathf.RoundToInt(-cellCenter.y * 100f);
                visuals.Add(cell, visual);
            }

            CreateBigPetFaceVisual();
        }

        private void CreateBigPetFaceVisual()
        {
            if (bodyCells.Count <= 1
                || !TryFindLargestBodyRectangle(out GridPosition bottomLeft, out GridPosition topRight))
            {
                return;
            }

            int rectangleWidth = topRight.X - bottomLeft.X + 1;
            int rectangleHeight = topRight.Y - bottomLeft.Y + 1;
            Sprite faceSprite = bigDogFace;
            if (rectangleWidth == 1 && rectangleHeight == 2 && bigDogFace1x2Vertical != null)
            {
                faceSprite = bigDogFace1x2Vertical;
            }
            else if (rectangleWidth == 2 && rectangleHeight == 1 && bigDogFace2x1Horizontal != null)
            {
                faceSprite = bigDogFace2x1Horizontal;
            }

            if (faceSprite == null)
            {
                return;
            }

            Vector3 bottomLeftWorld = world.Settings.GridToWorld(bottomLeft);
            Vector3 topRightWorld = world.Settings.GridToWorld(topRight);
            Vector3 rectangleCenter = (bottomLeftWorld + topRightWorld) * 0.5f
                + Vector3.back * 0.6f;

            bigPetFaceVisual = new GameObject($"{bodyName} Face");
            bigPetFaceVisual.transform.SetParent(transform);

            Sprite scaleReference = bigDogSide != null ? bigDogSide : faceSprite;
            float referenceSize = Mathf.Max(
                scaleReference.bounds.size.x,
                scaleReference.bounds.size.y);
            float scale = referenceSize > 0f
                ? world.Settings.cellSize / referenceSize
                : 1f;
            bigPetFaceVisual.transform.localScale = Vector3.one * scale;
            bigPetFaceVisual.transform.position = rectangleCenter
                - (Vector3)(faceSprite.bounds.center * scale);

            SpriteRenderer renderer = bigPetFaceVisual.AddComponent<SpriteRenderer>();
            renderer.sprite = faceSprite;
            renderer.sortingOrder = GetHighestBodySortingOrder() + 1;

            // The generated face object is the overlay above the large body tiles.
            // Put the animation's first frame on it immediately, then the burp
            // coroutine advances the remaining frames before shrinking one layer.
            if (bigDogFaceFrameData != null
                && bigDogFaceFrameData.Frames != null
                && bigDogFaceFrameData.Frames.Length > 0)
            {
                renderer.sprite = bigDogFaceFrameData.Frames[0];
            }
        }

        private int GetHighestBodySortingOrder()
        {
            int highestOrder = ActorSortingOrderBase;
            foreach (GameObject visual in visuals.Values)
            {
                SpriteRenderer bodyRenderer = visual.GetComponent<SpriteRenderer>();
                if (bodyRenderer != null)
                {
                    highestOrder = Mathf.Max(highestOrder, bodyRenderer.sortingOrder);
                }
            }

            return highestOrder;
        }

        private bool TryFindLargestBodyRectangle(
            out GridPosition bestBottomLeft,
            out GridPosition bestTopRight)
        {
            bestBottomLeft = default;
            bestTopRight = default;
            if (bodyCells.Count == 0)
            {
                return false;
            }

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            foreach (GridPosition cell in bodyCells)
            {
                minX = Mathf.Min(minX, cell.X);
                maxX = Mathf.Max(maxX, cell.X);
                minY = Mathf.Min(minY, cell.Y);
                maxY = Mathf.Max(maxY, cell.Y);
            }

            int bestArea = 0;
            float bestDistanceToOrigin = float.MaxValue;
            for (int bottom = minY; bottom <= maxY; bottom++)
            {
                for (int top = bottom; top <= maxY; top++)
                {
                    for (int left = minX; left <= maxX; left++)
                    {
                        for (int right = left; right <= maxX; right++)
                        {
                            int area = (right - left + 1) * (top - bottom + 1);
                            if (area < bestArea || !IsBodyRectangleFilled(left, right, bottom, top))
                            {
                                continue;
                            }

                            float centerX = (left + right) * 0.5f;
                            float centerY = (bottom + top) * 0.5f;
                            float distanceToOrigin = (centerX - origin.X) * (centerX - origin.X)
                                + (centerY - origin.Y) * (centerY - origin.Y);
                            if (area == bestArea && distanceToOrigin >= bestDistanceToOrigin)
                            {
                                continue;
                            }

                            bestArea = area;
                            bestDistanceToOrigin = distanceToOrigin;
                            bestBottomLeft = new GridPosition(left, bottom);
                            bestTopRight = new GridPosition(right, top);
                        }
                    }
                }
            }

            return bestArea > 0;
        }

        private bool IsBodyRectangleFilled(int left, int right, int bottom, int top)
        {
            for (int y = bottom; y <= top; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    if (!bodyCells.Contains(new GridPosition(x, y)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void DestroyBigPetFaceVisual()
        {
            if (bigPetFaceVisual == null)
            {
                return;
            }

            Destroy(bigPetFaceVisual);
            bigPetFaceVisual = null;
        }

        private Sprite GetBigPetSprite(GridPosition cell)
        {
            int mask = 0;
            if (bodyCells.Contains(cell + new GridPosition(0, 1))) mask |= 1;  // N
            if (bodyCells.Contains(cell + new GridPosition(1, 0))) mask |= 2;  // E
            if (bodyCells.Contains(cell + new GridPosition(0, -1))) mask |= 4; // S
            if (bodyCells.Contains(cell + new GridPosition(-1, 0))) mask |= 8; // W

            Sprite selected = mask switch
            {
                1 => bigDogOpen3ConnectedNorth,
                2 => bigDogOpen3ConnectedEast,
                4 => bigDogOpen3ConnectedSouth,
                8 => bigDogOpen3ConnectedWest,
                3 => bigDogNE,
                6 => bigDogES,
                9 => bigDogWN,
                12 => bigDogWS,
                7 => bigDogNES,
                11 => bigDogNEW,
                13 => bigDogNSW,
                14 => bigDogESW,
                _ => bigDogSide
            };

            return selected != null ? selected : bigDogSide;
        }

        private void SyncAttachedGridObject()
        {
            KitchenStation station = GetComponent<KitchenStation>();
            if (station != null)
            {
                station.SyncBodyPosition(world, origin);
                return;
            }

            if (world != null)
            {
                transform.position = world.Settings.GridToWorld(origin) + Vector3.back * 0.5f;
            }
        }

        private void FindNormalVisual()
        {
            if (normalVisual != null)
            {
                ConfigureNormalVisualSorting();
                return;
            }

            string preferredName = string.Equals(bodyName, "Kitchen", StringComparison.OrdinalIgnoreCase)
                ? "OvenVisual"
                : "DogVisual";
            Transform authoredVisual = transform.Find(preferredName);
            if (authoredVisual != null)
            {
                normalVisual = authoredVisual.gameObject;
            }

            if (normalVisual == null)
            {
                Animator authoredAnimator = GetComponentInChildren<Animator>(true);
                if (authoredAnimator != null && authoredAnimator.gameObject != gameObject)
                {
                    normalVisual = authoredAnimator.gameObject;
                }
            }

            if (normalVisual == null)
            {
                SpriteRenderer authoredRenderer = GetComponentInChildren<SpriteRenderer>(true);
                if (authoredRenderer != null && authoredRenderer.gameObject != gameObject)
                {
                    normalVisual = authoredRenderer.gameObject;
                }
            }

            ConfigureNormalVisualSorting();
        }

        private void ConfigureNormalVisualSorting()
        {
            if (normalVisual == null)
            {
                return;
            }

            float worldY = world != null
                ? world.Settings.GridToWorld(origin).y
                : transform.position.y;
            int sortingOrder = ActorSortingOrderBase + Mathf.RoundToInt(-worldY * 100f);
            SortingGroup sortingGroup = normalVisual.GetComponent<SortingGroup>();
            if (sortingGroup != null)
            {
                sortingGroup.sortingOrder = sortingOrder;
            }

            SpriteRenderer[] renderers = normalVisual.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingOrder = sortingGroup == null ? sortingOrder : 0;
            }
        }

        private void ClearBlockers()
        {
            foreach (GridPosition cell in bodyCells)
            {
                world.RemoveDynamicBlocker(cell);
            }
        }

        private void AddBlockers()
        {
            foreach (GridPosition cell in bodyCells)
            {
                world.AddDynamicBlocker(cell);
            }
        }

        private static Shader FindUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = color;
            Gizmos.DrawCube(transform.position, Vector3.one);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (bigDogFaceAnimation == null)
            {
                bigDogFaceAnimation = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/Resources/Pet/BigDogFace.anim");
            }
            if (bigDogFaceFrameData == null)
            {
                bigDogFaceFrameData = AssetDatabase.LoadAssetAtPath<PetFaceAnimationData>(
                    "Assets/Resources/Pet/BigDogFaceFrames.asset");
            }
        }
#endif
    }
}
