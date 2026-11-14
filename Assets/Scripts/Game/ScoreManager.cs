using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SpaceMissionDesign.Data;

namespace SpaceMissionDesign.Game
{
    /// <summary>
    /// Manages scoring across multiple players in a collaborative session.
    /// </summary>
    public sealed class ScoreManager : MonoBehaviour
    {
        private MissionManager manager => MissionManager.Instance;

        [Serializable]
        public class PlayerScore
        {
            public int playerIndex;
            public string playerName;
            public double personalScore;
            public List<string> contributions = new List<string>();
        }

        [SerializeField] private List<PlayerScore> playerScores = new List<PlayerScore>();
        [SerializeField] private double teamScore;

        public List<PlayerScore> PlayerScores => playerScores;
        public double TeamScore => teamScore;

        public void InitializeScores(int playerCount, List<string> playerNames)
        {
            playerScores = new List<PlayerScore>();
            for (int i = 0; i < playerCount; i++)
            {
                playerScores.Add(new PlayerScore
                {
                    playerIndex = i,
                    playerName = i < playerNames.Count ? playerNames[i] : $"Player {i + 1}",
                    personalScore = 0
                });
            }
            teamScore = 0;
        }

        public void RecordContribution(int playerIndex, string contribution, double points)
        {
            if (playerIndex < 0 || playerIndex >= playerScores.Count) return;
            playerScores[playerIndex].personalScore += points;
            playerScores[playerIndex].contributions.Add(contribution);
            teamScore += points;
        }

        public void FinalizeFromSimulation(SimulationResult result)
        {
            // Distribute the simulation score across players based on current turn order.
            // In a collaborative game, all players share the team score.
            if (playerScores.Count == 0) return;

            double perPlayer = result.score / playerScores.Count;
            foreach (var ps in playerScores)
            {
                ps.personalScore += perPlayer;
            }
            teamScore = result.score;
        }

        public string GetRankingText()
        {
            var sorted = playerScores.OrderByDescending(s => s.personalScore).ToList();
            var text = "Final Scores:\n";
            for (int i = 0; i < sorted.Count; i++)
                text += $"{i + 1}. {sorted[i].playerName}: {sorted[i].personalScore:F1}\n";
            text += $"\nTeam Score: {teamScore:F1}";
            return text;
        }
    }

    /// <summary>
    /// Manages turn-based multiplayer for up to 4 players collaborating on one mission design.
    /// </summary>
    public sealed class MultiplayerManager : MonoBehaviour
    {
        private MissionManager manager => MissionManager.Instance;

        public event Action<int> OnTurnChanged;

        public void SetupSession(int playerCount, List<string> names)
        {
            if (manager == null) return;
            manager.SetPlayerCount(playerCount);
            for (int i = 0; i < names.Count && i < playerCount; i++)
                manager.SetPlayerName(i, names[i]);
            OnTurnChanged?.Invoke(manager.CurrentPlayerIndex);
        }

        public void EndTurn()
        {
            if (manager == null) return;
            manager.AdvancePlayer();
            OnTurnChanged?.Invoke(manager.CurrentPlayerIndex);
        }

        public int CurrentPlayer => manager != null ? manager.CurrentPlayerIndex : 0;
        public int PlayerCount => manager != null ? manager.PlayerCount : 1;
        public string CurrentPlayerName => manager != null && manager.PlayerNames.Count > manager.CurrentPlayerIndex
            ? manager.PlayerNames[manager.CurrentPlayerIndex]
            : "Player 1";
    }
}
