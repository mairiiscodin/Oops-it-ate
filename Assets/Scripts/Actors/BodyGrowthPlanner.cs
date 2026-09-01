using System;
using System.Collections.Generic;
using OopsItAte.Grid;
using UnityEngine;

namespace OopsItAte.Actors
{
    internal static class BodyGrowthPlanner
    {
        private static readonly GridPosition[] Directions =
        {
            new GridPosition(-1, 1),
            new GridPosition(0, 1),
            new GridPosition(1, 1),
            new GridPosition(-1, 0),
            new GridPosition(1, 0),
            new GridPosition(-1, -1),
            new GridPosition(0, -1),
            new GridPosition(1, -1)
        };

        private static readonly GridPosition[] PlayerDirectionTieBreak =
        {
            new GridPosition(0, 1),
            new GridPosition(1, 1),
            new GridPosition(1, 0),
            new GridPosition(1, -1),
            new GridPosition(0, -1),
            new GridPosition(-1, -1),
            new GridPosition(-1, 0),
            new GridPosition(-1, 1)
        };

        private sealed class PushActor
        {
            public PushableBox Box;
            public PetBody Body;
            public GridMover Player;
            public readonly List<GridPosition> Cells = new List<GridPosition>();

            public bool IsPlayer => Player != null;
            public bool CanMove => Box != null
                || Player != null
                || (Body != null && Body.CanBePushedByBodyGrowth);

            public int KindOrder => Box != null ? 0 : Body != null ? 1 : 2;

            public GridPosition SortPosition => Cells.Count > 0
                ? Cells[0]
                : default;

            public BodyPushMove CreateMove(GridPosition direction)
            {
                if (Box != null)
                {
                    return BodyPushMove.ForBox(Box, direction);
                }

                if (Body != null)
                {
                    return BodyPushMove.ForBody(Body, direction);
                }

                return BodyPushMove.ForPlayer(Player, direction);
            }
        }

        private sealed class GrowthEdge
        {
            public GridPosition Source;
            public GridPosition Direction;
        }

        private sealed class GrowthCandidate
        {
            public GridPosition Position;
            public readonly List<GrowthEdge> Edges = new List<GrowthEdge>();
            public GridPosition PressureDirection;
            public PushActor Occupant;
        }

        private sealed class PushBranch
        {
            public PushActor Root;
            public readonly List<GrowthCandidate> Roots = new List<GrowthCandidate>();
            public readonly List<GridPosition> Directions = new List<GridPosition>();
            public readonly Dictionary<PushActor, GridPosition> Requests =
                new Dictionary<PushActor, GridPosition>();
            public int NextDirectionIndex;
            public bool Active;
        }

        public static bool TryBuild(
            PetBody growingBody,
            GridMover player,
            out BodyGrowthPlan plan)
        {
            plan = null;
            if (growingBody == null || growingBody.World == null || player == null)
            {
                return false;
            }

            GridWorld world = growingBody.World;
            var growingCells = new HashSet<GridPosition>(growingBody.Cells);
            List<PushActor> actors = BuildActors(growingBody, player, world);
            if (!TryBuildOccupancy(actors, out Dictionary<GridPosition, PushActor> occupancy))
            {
                Debug.LogWarning("Cannot grow: two actors already occupy the same grid cell.");
                return false;
            }

            List<GrowthCandidate> candidates = BuildCandidates(
                growingCells,
                occupancy,
                world);
            if (candidates.Count == 0)
            {
                return false;
            }

            var candidatePositions = new HashSet<GridPosition>();
            for (int i = 0; i < candidates.Count; i++)
            {
                candidatePositions.Add(candidates[i].Position);
            }

            List<PushBranch> branches = BuildBranches(
                candidates,
                occupancy,
                growingCells,
                candidatePositions,
                world);
            var rejectedCandidates = new HashSet<GrowthCandidate>();

            ResolveBranchesAndCorners(
                branches,
                candidates,
                rejectedCandidates,
                growingCells,
                occupancy,
                world);

            Dictionary<PushActor, GridPosition> moves = BuildEffectiveMoves(branches);
            HashSet<GrowthCandidate> acceptedCandidates = BuildAcceptedCandidates(
                branches,
                candidates,
                rejectedCandidates);
            if (acceptedCandidates.Count == 0)
            {
                return false;
            }

            List<GridPosition> growthCells = ToSortedPositions(acceptedCandidates);
            List<PushActor> moveOrder = BuildMoveOrder(moves, occupancy);
            var pushMoves = new List<BodyPushMove>(moveOrder.Count);
            for (int i = 0; i < moveOrder.Count; i++)
            {
                PushActor actor = moveOrder[i];
                pushMoves.Add(actor.CreateMove(moves[actor]));
            }

            plan = new BodyGrowthPlan(growthCells, pushMoves);
            return true;
        }

        private static List<PushActor> BuildActors(
            PetBody growingBody,
            GridMover player,
            GridWorld world)
        {
            var actors = new List<PushActor>();
            if (player.World == world)
            {
                var playerActor = new PushActor { Player = player };
                playerActor.Cells.Add(player.CurrentPosition);
                actors.Add(playerActor);
            }

            PushableBox[] boxes = UnityEngine.Object.FindObjectsByType<PushableBox>();
            for (int i = 0; i < boxes.Length; i++)
            {
                PushableBox box = boxes[i];
                if (!box.IsPushable || box.World != world)
                {
                    continue;
                }

                var boxActor = new PushActor { Box = box };
                boxActor.Cells.Add(box.Position);
                actors.Add(boxActor);
            }

            PetBody[] bodies = UnityEngine.Object.FindObjectsByType<PetBody>();
            for (int i = 0; i < bodies.Length; i++)
            {
                PetBody body = bodies[i];
                if (body == growingBody || body.World != world)
                {
                    continue;
                }

                var bodyActor = new PushActor { Body = body };
                foreach (GridPosition cell in body.Cells)
                {
                    bodyActor.Cells.Add(cell);
                }
                bodyActor.Cells.Sort(ComparePositions);
                actors.Add(bodyActor);
            }

            actors.Sort(CompareActors);
            return actors;
        }

        private static bool TryBuildOccupancy(
            List<PushActor> actors,
            out Dictionary<GridPosition, PushActor> occupancy)
        {
            occupancy = new Dictionary<GridPosition, PushActor>();
            for (int actorIndex = 0; actorIndex < actors.Count; actorIndex++)
            {
                PushActor actor = actors[actorIndex];
                for (int cellIndex = 0; cellIndex < actor.Cells.Count; cellIndex++)
                {
                    GridPosition cell = actor.Cells[cellIndex];
                    if (occupancy.ContainsKey(cell))
                    {
                        return false;
                    }

                    occupancy.Add(cell, actor);
                }
            }

            return true;
        }

        private static List<GrowthCandidate> BuildCandidates(
            HashSet<GridPosition> growingCells,
            Dictionary<GridPosition, PushActor> occupancy,
            GridWorld world)
        {
            var byPosition = new Dictionary<GridPosition, GrowthCandidate>();
            var sources = new List<GridPosition>(growingCells);
            sources.Sort(ComparePositions);

            for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                GridPosition source = sources[sourceIndex];
                for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
                {
                    GridPosition direction = Directions[directionIndex];
                    GridPosition target = source + direction;
                    if (growingCells.Contains(target)
                        || world.IsTerrainBlocked(target)
                        || !HasTerrainClearance(source, direction, world))
                    {
                        continue;
                    }

                    if (!byPosition.TryGetValue(target, out GrowthCandidate candidate))
                    {
                        candidate = new GrowthCandidate { Position = target };
                        byPosition.Add(target, candidate);
                    }

                    candidate.Edges.Add(new GrowthEdge
                    {
                        Source = source,
                        Direction = direction
                    });
                }
            }

            var result = new List<GrowthCandidate>(byPosition.Values);
            result.Sort((left, right) => ComparePositions(left.Position, right.Position));
            for (int i = 0; i < result.Count; i++)
            {
                GrowthCandidate candidate = result[i];
                int pressureX = 0;
                int pressureY = 0;
                for (int edgeIndex = 0; edgeIndex < candidate.Edges.Count; edgeIndex++)
                {
                    pressureX += candidate.Edges[edgeIndex].Direction.X;
                    pressureY += candidate.Edges[edgeIndex].Direction.Y;
                }

                candidate.PressureDirection = new GridPosition(
                    Math.Sign(pressureX),
                    Math.Sign(pressureY));
                occupancy.TryGetValue(candidate.Position, out candidate.Occupant);
            }

            return result;
        }

        private static List<PushBranch> BuildBranches(
            List<GrowthCandidate> candidates,
            Dictionary<GridPosition, PushActor> occupancy,
            HashSet<GridPosition> growingCells,
            HashSet<GridPosition> candidatePositions,
            GridWorld world)
        {
            var byActor = new Dictionary<PushActor, PushBranch>();
            for (int i = 0; i < candidates.Count; i++)
            {
                GrowthCandidate candidate = candidates[i];
                if (candidate.Occupant == null)
                {
                    continue;
                }

                if (!byActor.TryGetValue(candidate.Occupant, out PushBranch branch))
                {
                    branch = new PushBranch { Root = candidate.Occupant };
                    byActor.Add(candidate.Occupant, branch);
                }
                branch.Roots.Add(candidate);
            }

            var branches = new List<PushBranch>(byActor.Values);
            branches.Sort((left, right) => CompareActors(left.Root, right.Root));
            for (int i = 0; i < branches.Count; i++)
            {
                PushBranch branch = branches[i];
                GridPosition direction = SumPressure(branch.Roots);
                AddPushDirections(branch.Directions, direction);
                branch.Active = TryActivateNextPushDirection(
                    branch,
                    occupancy,
                    growingCells,
                    candidatePositions,
                    world);
            }

            return branches;
        }

        private static void AddPushDirections(
            List<GridPosition> directions,
            GridPosition pressureDirection)
        {
            if (IsZero(pressureDirection))
            {
                return;
            }

            directions.Add(pressureDirection);
            if (!IsDiagonal(pressureDirection))
            {
                return;
            }

            directions.Add(new GridPosition(pressureDirection.X, 0));
            directions.Add(new GridPosition(0, pressureDirection.Y));
        }

        private static bool TryActivateNextPushDirection(
            PushBranch branch,
            Dictionary<GridPosition, PushActor> occupancy,
            HashSet<GridPosition> growingCells,
            HashSet<GridPosition> candidatePositions,
            GridWorld world)
        {
            branch.Requests.Clear();
            while (branch.NextDirectionIndex < branch.Directions.Count)
            {
                GridPosition direction = branch.Directions[branch.NextDirectionIndex++];
                if (PlanPushBranch(
                    branch,
                    branch.Root,
                    direction,
                    occupancy,
                    growingCells,
                    candidatePositions,
                    world,
                    new HashSet<PushActor>()))
                {
                    return true;
                }

                branch.Requests.Clear();
            }

            return false;
        }

        private static bool PlanPushBranch(
            PushBranch branch,
            PushActor actor,
            GridPosition direction,
            Dictionary<GridPosition, PushActor> occupancy,
            HashSet<GridPosition> growingCells,
            HashSet<GridPosition> candidatePositions,
            GridWorld world,
            HashSet<PushActor> visiting)
        {
            if (!actor.CanMove || visiting.Contains(actor))
            {
                return false;
            }

            if (branch.Requests.TryGetValue(actor, out GridPosition existingDirection))
            {
                return existingDirection.Equals(direction);
            }

            if (actor.IsPlayer)
            {
                return TryPlanPlayerEscape(
                    branch,
                    actor,
                    direction,
                    occupancy,
                    growingCells,
                    candidatePositions,
                    world);
            }

            branch.Requests.Add(actor, direction);
            visiting.Add(actor);
            for (int i = 0; i < actor.Cells.Count; i++)
            {
                GridPosition source = actor.Cells[i];
                GridPosition destination = source + direction;
                if (world.IsTerrainBlocked(destination)
                    || growingCells.Contains(destination)
                    || candidatePositions.Contains(destination)
                    || !HasTerrainClearance(source, direction, world))
                {
                    visiting.Remove(actor);
                    return false;
                }

                if (!occupancy.TryGetValue(destination, out PushActor blockingActor)
                    || blockingActor == actor)
                {
                    continue;
                }

                if (!PlanPushBranch(
                        branch,
                        blockingActor,
                        direction,
                        occupancy,
                        growingCells,
                        candidatePositions,
                        world,
                        visiting))
                {
                    visiting.Remove(actor);
                    return false;
                }
            }

            visiting.Remove(actor);
            return true;
        }

        private static bool TryPlanPlayerEscape(
            PushBranch branch,
            PushActor player,
            GridPosition pressureDirection,
            Dictionary<GridPosition, PushActor> occupancy,
            HashSet<GridPosition> growingCells,
            HashSet<GridPosition> candidatePositions,
            GridWorld world)
        {
            List<GridPosition> escapeDirections = GetPlayerEscapeDirections(
                pressureDirection);
            GridPosition source = player.Cells[0];
            for (int i = 0; i < escapeDirections.Count; i++)
            {
                GridPosition direction = escapeDirections[i];
                if (!CanPlayerEscape(
                    branch,
                    player,
                    source,
                    direction,
                    occupancy,
                    growingCells,
                    candidatePositions,
                    world))
                {
                    continue;
                }

                branch.Requests.Add(player, direction);
                return true;
            }

            return false;
        }

        private static List<GridPosition> GetPlayerEscapeDirections(
            GridPosition pressureDirection)
        {
            var result = new List<GridPosition>(PlayerDirectionTieBreak.Length);
            if (!IsZero(pressureDirection))
            {
                result.Add(pressureDirection);
            }

            var alternatives = new List<GridPosition>(PlayerDirectionTieBreak.Length - 1);
            for (int i = 0; i < PlayerDirectionTieBreak.Length; i++)
            {
                GridPosition candidate = PlayerDirectionTieBreak[i];
                if (!candidate.Equals(pressureDirection))
                {
                    alternatives.Add(candidate);
                }
            }

            alternatives.Sort((left, right) =>
            {
                int rightScore = Dot(right, pressureDirection);
                int leftScore = Dot(left, pressureDirection);
                int scoreComparison = rightScore.CompareTo(leftScore);
                return scoreComparison != 0
                    ? scoreComparison
                    : GetPlayerTieBreakIndex(left).CompareTo(GetPlayerTieBreakIndex(right));
            });
            result.AddRange(alternatives);
            return result;
        }

        private static bool CanPlayerEscape(
            PushBranch branch,
            PushActor player,
            GridPosition source,
            GridPosition direction,
            Dictionary<GridPosition, PushActor> occupancy,
            HashSet<GridPosition> growingCells,
            HashSet<GridPosition> candidatePositions,
            GridWorld world)
        {
            GridPosition destination = source + direction;
            if (world.IsTerrainBlocked(destination)
                || growingCells.Contains(destination)
                || candidatePositions.Contains(destination)
                || !HasTerrainClearance(source, direction, world)
                || (occupancy.TryGetValue(destination, out PushActor destinationActor)
                    && destinationActor != player))
            {
                return false;
            }

            if (!IsDiagonal(direction))
            {
                return true;
            }

            GridPosition[] sideCells = GetSideCells(source, direction);
            for (int i = 0; i < sideCells.Length; i++)
            {
                GridPosition side = sideCells[i];
                if (growingCells.Contains(side))
                {
                    return false;
                }

                if (!occupancy.TryGetValue(side, out PushActor sideActor)
                    || sideActor == player)
                {
                    continue;
                }

                if (!branch.Requests.TryGetValue(sideActor, out GridPosition sideDirection)
                    || ActorOccupiesAfterMove(sideActor, sideDirection, side))
                {
                    return false;
                }
            }

            return true;
        }

        private static void ResolveBranchesAndCorners(
            List<PushBranch> branches,
            List<GrowthCandidate> candidates,
            HashSet<GrowthCandidate> rejectedCandidates,
            HashSet<GridPosition> growingCells,
            Dictionary<GridPosition, PushActor> occupancy,
            GridWorld world)
        {
            var candidatePositions = new HashSet<GridPosition>();
            for (int i = 0; i < candidates.Count; i++)
            {
                candidatePositions.Add(candidates[i].Position);
            }

            bool changed;
            do
            {
                changed = false;
                DeactivateBranchesWithoutRoots(branches, rejectedCandidates, ref changed);

                Dictionary<PushActor, List<PushBranch>> sources;
                Dictionary<PushActor, GridPosition> moves = BuildEffectiveMoves(
                    branches,
                    out sources,
                    out HashSet<PushBranch> directionConflicts);
                if (AdvanceOrDeactivateBranches(
                    directionConflicts,
                    occupancy,
                    growingCells,
                    candidatePositions,
                    world))
                {
                    changed = true;
                    continue;
                }

                HashSet<PushBranch> movementConflicts = FindMovementConflicts(
                    moves,
                    sources,
                    occupancy,
                    growingCells,
                    world);
                if (AdvanceOrDeactivateBranches(
                    movementConflicts,
                    occupancy,
                    growingCells,
                    candidatePositions,
                    world))
                {
                    changed = true;
                    continue;
                }

                HashSet<PushBranch> cycleConflicts = FindCycleConflicts(
                    moves,
                    sources,
                    occupancy);
                if (AdvanceOrDeactivateBranches(
                    cycleConflicts,
                    occupancy,
                    growingCells,
                    candidatePositions,
                    world))
                {
                    changed = true;
                    continue;
                }

                HashSet<GrowthCandidate> accepted = BuildAcceptedCandidates(
                    branches,
                    candidates,
                    rejectedCandidates);
                List<GrowthCandidate> invalidCorners = FindInvalidGrowthCorners(
                    accepted,
                    moves,
                    occupancy,
                    growingCells);
                for (int i = 0; i < invalidCorners.Count; i++)
                {
                    if (rejectedCandidates.Add(invalidCorners[i]))
                    {
                        changed = true;
                    }
                }
            }
            while (changed);
        }

        private static Dictionary<PushActor, GridPosition> BuildEffectiveMoves(
            List<PushBranch> branches)
        {
            return BuildEffectiveMoves(
                branches,
                out _,
                out _);
        }

        private static Dictionary<PushActor, GridPosition> BuildEffectiveMoves(
            List<PushBranch> branches,
            out Dictionary<PushActor, List<PushBranch>> sources,
            out HashSet<PushBranch> conflicts)
        {
            var moves = new Dictionary<PushActor, GridPosition>();
            sources = new Dictionary<PushActor, List<PushBranch>>();
            conflicts = new HashSet<PushBranch>();

            for (int branchIndex = 0; branchIndex < branches.Count; branchIndex++)
            {
                PushBranch branch = branches[branchIndex];
                if (!branch.Active)
                {
                    continue;
                }

                foreach (KeyValuePair<PushActor, GridPosition> request in branch.Requests)
                {
                    if (!sources.TryGetValue(request.Key, out List<PushBranch> actorSources))
                    {
                        actorSources = new List<PushBranch>();
                        sources.Add(request.Key, actorSources);
                    }
                    actorSources.Add(branch);

                    if (moves.TryGetValue(request.Key, out GridPosition existingDirection)
                        && !existingDirection.Equals(request.Value))
                    {
                        for (int i = 0; i < actorSources.Count; i++)
                        {
                            conflicts.Add(actorSources[i]);
                        }
                        continue;
                    }

                    moves[request.Key] = request.Value;
                }
            }

            return moves;
        }

        private static HashSet<PushBranch> FindMovementConflicts(
            Dictionary<PushActor, GridPosition> moves,
            Dictionary<PushActor, List<PushBranch>> sources,
            Dictionary<GridPosition, PushActor> occupancy,
            HashSet<GridPosition> growingCells,
            GridWorld world)
        {
            var conflicts = new HashSet<PushBranch>();
            var finalOccupancy = new Dictionary<GridPosition, PushActor>();
            List<PushActor> movingActors = ToSortedActors(moves.Keys);

            for (int actorIndex = 0; actorIndex < movingActors.Count; actorIndex++)
            {
                PushActor actor = movingActors[actorIndex];
                GridPosition direction = moves[actor];
                for (int cellIndex = 0; cellIndex < actor.Cells.Count; cellIndex++)
                {
                    GridPosition source = actor.Cells[cellIndex];
                    GridPosition destination = source + direction;
                    if (world.IsTerrainBlocked(destination)
                        || growingCells.Contains(destination))
                    {
                        AddActorSources(actor, sources, conflicts);
                        continue;
                    }

                    if (occupancy.TryGetValue(destination, out PushActor occupant)
                        && occupant != actor
                        && !moves.ContainsKey(occupant))
                    {
                        AddActorSources(actor, sources, conflicts);
                    }

                    if (finalOccupancy.TryGetValue(destination, out PushActor reservedBy)
                        && reservedBy != actor)
                    {
                        AddActorSources(actor, sources, conflicts);
                        AddActorSources(reservedBy, sources, conflicts);
                    }
                    else
                    {
                        finalOccupancy[destination] = actor;
                    }

                    if (IsDiagonal(direction))
                    {
                        ValidateMovingCorner(
                            actor,
                            source,
                            direction,
                            moves,
                            sources,
                            occupancy,
                            growingCells,
                            conflicts);
                    }
                }
            }

            return conflicts;
        }

        private static void ValidateMovingCorner(
            PushActor actor,
            GridPosition source,
            GridPosition direction,
            Dictionary<PushActor, GridPosition> moves,
            Dictionary<PushActor, List<PushBranch>> sources,
            Dictionary<GridPosition, PushActor> occupancy,
            HashSet<GridPosition> growingCells,
            HashSet<PushBranch> conflicts)
        {
            GridPosition[] sideCells = GetSideCells(source, direction);
            for (int i = 0; i < sideCells.Length; i++)
            {
                GridPosition side = sideCells[i];
                if (growingCells.Contains(side))
                {
                    AddActorSources(actor, sources, conflicts);
                    continue;
                }

                if (!occupancy.TryGetValue(side, out PushActor sideActor)
                    || sideActor == actor)
                {
                    continue;
                }

                if (!moves.TryGetValue(sideActor, out GridPosition sideDirection)
                    || ActorOccupiesAfterMove(sideActor, sideDirection, side))
                {
                    AddActorSources(actor, sources, conflicts);
                    AddActorSources(sideActor, sources, conflicts);
                }
            }
        }

        private static HashSet<PushBranch> FindCycleConflicts(
            Dictionary<PushActor, GridPosition> moves,
            Dictionary<PushActor, List<PushBranch>> sources,
            Dictionary<GridPosition, PushActor> occupancy)
        {
            var conflicts = new HashSet<PushBranch>();
            var states = new Dictionary<PushActor, int>();
            var stack = new List<PushActor>();
            List<PushActor> actors = ToSortedActors(moves.Keys);
            for (int i = 0; i < actors.Count; i++)
            {
                VisitDependencies(
                    actors[i],
                    moves,
                    sources,
                    occupancy,
                    states,
                    stack,
                    conflicts);
            }

            return conflicts;
        }

        private static void VisitDependencies(
            PushActor actor,
            Dictionary<PushActor, GridPosition> moves,
            Dictionary<PushActor, List<PushBranch>> sources,
            Dictionary<GridPosition, PushActor> occupancy,
            Dictionary<PushActor, int> states,
            List<PushActor> stack,
            HashSet<PushBranch> conflicts)
        {
            if (states.TryGetValue(actor, out int state))
            {
                if (state == 1)
                {
                    int cycleStart = stack.IndexOf(actor);
                    for (int i = cycleStart; i < stack.Count; i++)
                    {
                        AddActorSources(stack[i], sources, conflicts);
                    }
                }
                return;
            }

            states[actor] = 1;
            stack.Add(actor);
            GridPosition direction = moves[actor];
            for (int i = 0; i < actor.Cells.Count; i++)
            {
                GridPosition destination = actor.Cells[i] + direction;
                if (occupancy.TryGetValue(destination, out PushActor dependency)
                    && dependency != actor
                    && moves.ContainsKey(dependency))
                {
                    VisitDependencies(
                        dependency,
                        moves,
                        sources,
                        occupancy,
                        states,
                        stack,
                        conflicts);
                }
            }

            stack.RemoveAt(stack.Count - 1);
            states[actor] = 2;
        }

        private static HashSet<GrowthCandidate> BuildAcceptedCandidates(
            List<PushBranch> branches,
            List<GrowthCandidate> candidates,
            HashSet<GrowthCandidate> rejectedCandidates)
        {
            var branchByRoot = new Dictionary<PushActor, PushBranch>();
            for (int i = 0; i < branches.Count; i++)
            {
                branchByRoot[branches[i].Root] = branches[i];
            }

            var accepted = new HashSet<GrowthCandidate>();
            for (int i = 0; i < candidates.Count; i++)
            {
                GrowthCandidate candidate = candidates[i];
                if (rejectedCandidates.Contains(candidate))
                {
                    continue;
                }

                if (candidate.Occupant == null)
                {
                    accepted.Add(candidate);
                    continue;
                }

                if (branchByRoot.TryGetValue(candidate.Occupant, out PushBranch branch)
                    && branch.Active)
                {
                    accepted.Add(candidate);
                }
            }

            return accepted;
        }

        private static List<GrowthCandidate> FindInvalidGrowthCorners(
            HashSet<GrowthCandidate> accepted,
            Dictionary<PushActor, GridPosition> moves,
            Dictionary<GridPosition, PushActor> occupancy,
            HashSet<GridPosition> growingCells)
        {
            var acceptedPositions = new HashSet<GridPosition>();
            foreach (GrowthCandidate candidate in accepted)
            {
                acceptedPositions.Add(candidate.Position);
            }

            var invalid = new List<GrowthCandidate>();
            foreach (GrowthCandidate candidate in accepted)
            {
                bool hasValidEdge = false;
                for (int edgeIndex = 0; edgeIndex < candidate.Edges.Count; edgeIndex++)
                {
                    GrowthEdge edge = candidate.Edges[edgeIndex];
                    if (!IsDiagonal(edge.Direction)
                        || HasDynamicGrowthClearance(
                            edge,
                            moves,
                            occupancy,
                            growingCells,
                            acceptedPositions))
                    {
                        hasValidEdge = true;
                        break;
                    }
                }

                if (!hasValidEdge)
                {
                    invalid.Add(candidate);
                }
            }

            return invalid;
        }

        private static bool HasDynamicGrowthClearance(
            GrowthEdge edge,
            Dictionary<PushActor, GridPosition> moves,
            Dictionary<GridPosition, PushActor> occupancy,
            HashSet<GridPosition> growingCells,
            HashSet<GridPosition> acceptedPositions)
        {
            GridPosition[] sideCells = GetSideCells(edge.Source, edge.Direction);
            for (int i = 0; i < sideCells.Length; i++)
            {
                GridPosition side = sideCells[i];
                if (growingCells.Contains(side) || acceptedPositions.Contains(side))
                {
                    continue;
                }

                if (!occupancy.TryGetValue(side, out PushActor sideActor))
                {
                    continue;
                }

                if (!moves.TryGetValue(sideActor, out GridPosition direction)
                    || ActorOccupiesAfterMove(sideActor, direction, side))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<PushActor> BuildMoveOrder(
            Dictionary<PushActor, GridPosition> moves,
            Dictionary<GridPosition, PushActor> occupancy)
        {
            var result = new List<PushActor>();
            var visited = new HashSet<PushActor>();
            var visiting = new HashSet<PushActor>();
            List<PushActor> actors = ToSortedActors(moves.Keys);
            for (int i = 0; i < actors.Count; i++)
            {
                AddMoveWithDependencies(
                    actors[i],
                    moves,
                    occupancy,
                    visiting,
                    visited,
                    result);
            }
            return result;
        }

        private static void AddMoveWithDependencies(
            PushActor actor,
            Dictionary<PushActor, GridPosition> moves,
            Dictionary<GridPosition, PushActor> occupancy,
            HashSet<PushActor> visiting,
            HashSet<PushActor> visited,
            List<PushActor> result)
        {
            if (visited.Contains(actor) || visiting.Contains(actor))
            {
                return;
            }

            visiting.Add(actor);
            GridPosition direction = moves[actor];
            for (int i = 0; i < actor.Cells.Count; i++)
            {
                GridPosition destination = actor.Cells[i] + direction;
                if (occupancy.TryGetValue(destination, out PushActor dependency)
                    && dependency != actor
                    && moves.ContainsKey(dependency))
                {
                    AddMoveWithDependencies(
                        dependency,
                        moves,
                        occupancy,
                        visiting,
                        visited,
                        result);
                }
            }

            visiting.Remove(actor);
            visited.Add(actor);
            result.Add(actor);
        }

        private static void DeactivateBranchesWithoutRoots(
            List<PushBranch> branches,
            HashSet<GrowthCandidate> rejectedCandidates,
            ref bool changed)
        {
            for (int i = 0; i < branches.Count; i++)
            {
                PushBranch branch = branches[i];
                if (!branch.Active)
                {
                    continue;
                }

                bool hasRoot = false;
                for (int rootIndex = 0; rootIndex < branch.Roots.Count; rootIndex++)
                {
                    if (!rejectedCandidates.Contains(branch.Roots[rootIndex]))
                    {
                        hasRoot = true;
                        break;
                    }
                }

                if (!hasRoot)
                {
                    branch.Active = false;
                    changed = true;
                }
            }
        }

        private static bool AdvanceOrDeactivateBranches(
            HashSet<PushBranch> branches,
            Dictionary<GridPosition, PushActor> occupancy,
            HashSet<GridPosition> growingCells,
            HashSet<GridPosition> candidatePositions,
            GridWorld world)
        {
            bool changed = false;
            foreach (PushBranch branch in branches)
            {
                if (!branch.Active)
                {
                    continue;
                }

                branch.Active = false;
                branch.Requests.Clear();
                branch.Active = TryActivateNextPushDirection(
                    branch,
                    occupancy,
                    growingCells,
                    candidatePositions,
                    world);
                changed = true;
            }
            return changed;
        }

        private static void AddActorSources(
            PushActor actor,
            Dictionary<PushActor, List<PushBranch>> sources,
            HashSet<PushBranch> conflicts)
        {
            if (!sources.TryGetValue(actor, out List<PushBranch> actorSources))
            {
                return;
            }

            for (int i = 0; i < actorSources.Count; i++)
            {
                conflicts.Add(actorSources[i]);
            }
        }

        private static GridPosition SumPressure(List<GrowthCandidate> candidates)
        {
            int x = 0;
            int y = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                x += candidates[i].PressureDirection.X;
                y += candidates[i].PressureDirection.Y;
            }
            return new GridPosition(Math.Sign(x), Math.Sign(y));
        }

        private static bool HasTerrainClearance(
            GridPosition source,
            GridPosition direction,
            GridWorld world)
        {
            if (!IsDiagonal(direction))
            {
                return true;
            }

            GridPosition[] sides = GetSideCells(source, direction);
            return !world.IsTerrainBlocked(sides[0])
                && !world.IsTerrainBlocked(sides[1]);
        }

        private static GridPosition[] GetSideCells(
            GridPosition source,
            GridPosition direction)
        {
            return new[]
            {
                source + new GridPosition(direction.X, 0),
                source + new GridPosition(0, direction.Y)
            };
        }

        private static bool ActorOccupiesAfterMove(
            PushActor actor,
            GridPosition direction,
            GridPosition position)
        {
            for (int i = 0; i < actor.Cells.Count; i++)
            {
                if ((actor.Cells[i] + direction).Equals(position))
                {
                    return true;
                }
            }
            return false;
        }

        private static List<GridPosition> ToSortedPositions(
            HashSet<GrowthCandidate> candidates)
        {
            var result = new List<GridPosition>(candidates.Count);
            foreach (GrowthCandidate candidate in candidates)
            {
                result.Add(candidate.Position);
            }
            result.Sort(ComparePositions);
            return result;
        }

        private static List<PushActor> ToSortedActors(IEnumerable<PushActor> actors)
        {
            var result = new List<PushActor>(actors);
            result.Sort(CompareActors);
            return result;
        }

        private static int CompareActors(PushActor left, PushActor right)
        {
            int positionComparison = ComparePositions(left.SortPosition, right.SortPosition);
            if (positionComparison != 0)
            {
                return positionComparison;
            }
            return left.KindOrder.CompareTo(right.KindOrder);
        }

        private static int ComparePositions(GridPosition left, GridPosition right)
        {
            int yComparison = left.Y.CompareTo(right.Y);
            return yComparison != 0 ? yComparison : left.X.CompareTo(right.X);
        }

        private static int Dot(GridPosition left, GridPosition right)
        {
            return left.X * right.X + left.Y * right.Y;
        }

        private static int GetPlayerTieBreakIndex(GridPosition direction)
        {
            for (int i = 0; i < PlayerDirectionTieBreak.Length; i++)
            {
                if (PlayerDirectionTieBreak[i].Equals(direction))
                {
                    return i;
                }
            }

            return int.MaxValue;
        }

        private static bool IsDiagonal(GridPosition direction)
        {
            return direction.X != 0 && direction.Y != 0;
        }

        private static bool IsZero(GridPosition direction)
        {
            return direction.X == 0 && direction.Y == 0;
        }
    }
}
