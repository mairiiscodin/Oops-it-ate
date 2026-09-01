using System;
using System.Collections.Generic;
using OopsItAte.Grid;
using UnityEngine;

namespace OopsItAte.Actors
{
    internal sealed class BodyGrowthPlan
    {
        private readonly List<GridPosition> growthCells;
        private readonly List<BodyPushMove> pushMoves;

        public BodyGrowthPlan(
            List<GridPosition> growthCells,
            List<BodyPushMove> pushMoves)
        {
            this.growthCells = growthCells;
            this.pushMoves = pushMoves;
        }

        public IReadOnlyList<GridPosition> GrowthCells => growthCells;

        public bool TryCommit(PetBody growingBody)
        {
            int appliedMoves = 0;
            try
            {
                for (int i = 0; i < pushMoves.Count; i++)
                {
                    pushMoves[i].Apply();
                    appliedMoves++;
                }

                if (growingBody.TryGrow(growthCells))
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            for (int i = appliedMoves - 1; i >= 0; i--)
            {
                pushMoves[i].Revert();
            }

            return false;
        }
    }

    internal sealed class BodyPushMove
    {
        private readonly PushableBox box;
        private readonly PetBody body;
        private readonly GridMover player;
        private readonly GridPosition startPosition;

        private BodyPushMove(
            PushableBox box,
            PetBody body,
            GridMover player,
            GridPosition startPosition,
            GridPosition direction)
        {
            this.box = box;
            this.body = body;
            this.player = player;
            this.startPosition = startPosition;
            Direction = direction;
        }

        public GridPosition Direction { get; }

        public static BodyPushMove ForBox(PushableBox box, GridPosition direction)
        {
            return new BodyPushMove(box, null, null, box.Position, direction);
        }

        public static BodyPushMove ForBody(PetBody body, GridPosition direction)
        {
            return new BodyPushMove(null, body, null, body.Origin, direction);
        }

        public static BodyPushMove ForPlayer(GridMover player, GridPosition direction)
        {
            return new BodyPushMove(null, null, player, player.CurrentPosition, direction);
        }

        public void Apply()
        {
            if (box != null)
            {
                box.SetPositionUnchecked(startPosition + Direction);
                return;
            }

            if (body != null)
            {
                body.ShiftUnchecked(Direction);
                return;
            }

            player.MoveTo(startPosition + Direction);
        }

        public void Revert()
        {
            if (box != null)
            {
                box.SetPositionUnchecked(startPosition);
                return;
            }

            if (body != null)
            {
                body.ShiftUnchecked(new GridPosition(-Direction.X, -Direction.Y));
                return;
            }

            player.MoveTo(startPosition);
        }
    }
}
