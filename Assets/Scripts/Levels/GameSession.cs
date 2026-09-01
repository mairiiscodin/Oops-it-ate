using System;
using System.Collections.Generic;
using OopsItAte.Grid;
using UnityEngine;

namespace OopsItAte.Levels
{
    /// <summary>Keeps adventure progress while room scenes are being replaced.</summary>
    public static class GameSession
    {
        private static readonly HashSet<string> unlockedDoors =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, GridPosition> objectPositions =
            new Dictionary<string, GridPosition>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> flags =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static bool hasPendingArrival;
        private static string pendingSourceRoomId;
        private static string pendingSourceSceneName;
        private static string pendingTargetDoorId;

        public static bool HasFood { get; private set; }
        public static string CurrentRoomId { get; private set; } = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetBeforeGameStarts()
        {
            StartNewGame();
        }

        public static void StartNewGame()
        {
            unlockedDoors.Clear();
            objectPositions.Clear();
            flags.Clear();
            HasFood = false;
            CurrentRoomId = string.Empty;
            hasPendingArrival = false;
            pendingSourceRoomId = string.Empty;
            pendingSourceSceneName = string.Empty;
            pendingTargetDoorId = string.Empty;
        }

        public static void EnterRoom(string roomId)
        {
            CurrentRoomId = Normalize(roomId);
        }

        public static void ResetRoom(string roomId)
        {
            string prefix = Normalize(roomId) + "::";
            unlockedDoors.RemoveWhere(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            flags.RemoveWhere(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            var positionKeys = new List<string>();
            foreach (string key in objectPositions.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    positionKeys.Add(key);
                }
            }

            for (int i = 0; i < positionKeys.Count; i++)
            {
                objectPositions.Remove(positionKeys[i]);
            }

            HasFood = false;
            hasPendingArrival = false;
            pendingSourceRoomId = string.Empty;
            pendingSourceSceneName = string.Empty;
            pendingTargetDoorId = string.Empty;
        }

        public static void BeginRoomTransition(
            string sourceRoomId,
            string sourceSceneName,
            string targetDoorId,
            bool hasFood)
        {
            pendingSourceRoomId = Normalize(sourceRoomId);
            pendingSourceSceneName = Normalize(sourceSceneName);
            pendingTargetDoorId = Normalize(targetDoorId);
            hasPendingArrival = true;
            HasFood = hasFood;
        }

        public static bool TryConsumeArrival(
            out string sourceRoomId,
            out string sourceSceneName,
            out string targetDoorId)
        {
            if (!hasPendingArrival)
            {
                sourceRoomId = string.Empty;
                sourceSceneName = string.Empty;
                targetDoorId = string.Empty;
                return false;
            }

            sourceRoomId = pendingSourceRoomId;
            sourceSceneName = pendingSourceSceneName;
            targetDoorId = pendingTargetDoorId;
            hasPendingArrival = false;
            pendingSourceRoomId = string.Empty;
            pendingSourceSceneName = string.Empty;
            pendingTargetDoorId = string.Empty;
            return true;
        }

        public static void SetHasFood(bool value)
        {
            HasFood = value;
        }

        public static void UnlockDoor(string roomId, string doorId)
        {
            unlockedDoors.Add(GetScopedKey(roomId, doorId));
        }

        public static bool IsDoorUnlocked(string roomId, string doorId)
        {
            return unlockedDoors.Contains(GetScopedKey(roomId, doorId));
        }

        public static void SaveObjectPosition(
            string roomId,
            string objectId,
            GridPosition position)
        {
            objectPositions[GetScopedKey(roomId, objectId)] = position;
        }

        public static bool TryGetObjectPosition(
            string roomId,
            string objectId,
            out GridPosition position)
        {
            return objectPositions.TryGetValue(GetScopedKey(roomId, objectId), out position);
        }

        public static void SavePetFedState(string roomId, string petId, bool hasBeenFed)
        {
            string key = GetScopedKey(roomId, $"PetFed:{petId}");
            if (hasBeenFed) flags.Add(key);
            else flags.Remove(key);
        }

        public static bool HasPetBeenFed(string roomId, string petId)
        {
            return flags.Contains(GetScopedKey(roomId, $"PetFed:{petId}"));
        }

        public static void MarkRoomCompletionPlayed(string roomId)
        {
            flags.Add(GetScopedKey(roomId, "RoomCompletionPlayed"));
        }

        public static bool HasRoomCompletionPlayed(string roomId)
        {
            return flags.Contains(GetScopedKey(roomId, "RoomCompletionPlayed"));
        }

        public static void SetFlag(string flagId, bool value = true)
        {
            string normalized = Normalize(flagId);
            if (value) flags.Add(normalized);
            else flags.Remove(normalized);
        }

        public static bool HasFlag(string flagId) => flags.Contains(Normalize(flagId));

        private static string GetScopedKey(string roomId, string objectId)
        {
            return $"{Normalize(roomId)}::{Normalize(objectId)}";
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
