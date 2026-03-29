using System;
using Fusion;
using Fusion.Sockets;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// ロビー状態をホスト→クライアントに同期する最小限の NetworkBehaviour。
    /// ステージ名・CPU構成・プレイヤー数・マッチ開始シグナルを [Networked] で共有する。
    /// </summary>
    public class LobbyController : NetworkBehaviour
    {
        [Networked] public NetworkBool MatchStarted { get; set; }
        [Networked] public int PlayerCount { get; set; }
        [Networked] public int CpuSlotMask { get; set; }
        [Networked, Capacity(64)] public NetworkString<_64> StageName { get; set; }

        /// <summary>クライアント側でマッチ開始を検知した際に発火する。</summary>
        public event Action OnMatchStartDetected;

        /// <summary>Spawned 時にクライアント側で発火する。</summary>
        public static event Action<LobbyController> OnLobbySpawned;

        private bool _wasMatchStarted;

        public override void Spawned()
        {
            // クライアント側で LobbyController が同期された時点で通知
            if (!Object.HasStateAuthority)
            {
                OnLobbySpawned?.Invoke(this);
            }
        }

        public override void Render()
        {
            if (!Object.HasStateAuthority && MatchStarted && !_wasMatchStarted)
            {
                _wasMatchStarted = true;
                OnMatchStartDetected?.Invoke();
            }
        }

        // --- ホスト側ユーティリティ ---

        public void SetLobbyConfig(int playerCount, bool[] cpuSlots, string stageName)
        {
            if (!Object.HasStateAuthority) return;

            PlayerCount = playerCount;
            CpuSlotMask = EncodeCpuSlots(cpuSlots);
            StageName = stageName ?? string.Empty;
        }

        public void StartMatch()
        {
            if (!Object.HasStateAuthority) return;
            MatchStarted = true;
        }

        // --- CPU スロットのビットマスク変換 ---

        public static int EncodeCpuSlots(bool[] slots)
        {
            int mask = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i]) mask |= (1 << i);
            }
            return mask;
        }

        public static bool[] DecodeCpuSlots(int mask, int playerCount)
        {
            var slots = new bool[4];
            for (int i = 0; i < playerCount; i++)
            {
                slots[i] = (mask & (1 << i)) != 0;
            }
            return slots;
        }
    }
}
