using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.MatchFlow.Application
{
    public sealed class FireDamageTickService
    {
        private readonly PlayerDamageService _damageService;
        private readonly SafeTileSearchService _safeTileSearch;
        private readonly SlimeRegistry _slimeRegistry;
        private readonly int _dotDamage;
        private readonly float _dotInterval;

        private readonly Dictionary<int, float> _playerFireAccum = new();
        private readonly Dictionary<SlimeId, float> _slimeFireAccum = new();

        public FireDamageTickService(
            PlayerDamageService damageService,
            SafeTileSearchService safeTileSearch,
            SlimeRegistry slimeRegistry,
            IBalanceParameters balance)
        {
            _damageService = damageService;
            _safeTileSearch = safeTileSearch;
            _slimeRegistry = slimeRegistry;
            _dotDamage = balance.FireBombDotDamage;
            _dotInterval = balance.FireBombDotInterval;
        }

        public void Tick(float deltaTime, IReadOnlyList<PlayerModel> players, StageModel stage)
        {
            TickPlayers(deltaTime, players, stage);
            TickSlimes(deltaTime, stage);
        }

        private void TickPlayers(float deltaTime, IReadOnlyList<PlayerModel> players, StageModel stage)
        {
            var occupied = new HashSet<GridPos>();
            foreach (var p in players) occupied.Add(p.CurrentPosition);

            foreach (var player in players)
            {
                int key = player.Id.Index;
                var tileState = stage.GetTileState(player.CurrentPosition);

                if (tileState == TileState.OnFire)
                {
                    _playerFireAccum.TryGetValue(key, out float accum);
                    accum += deltaTime;

                    while (accum >= _dotInterval)
                    {
                        _damageService.ApplyDamage(player, _dotDamage, false,
                            stage, _safeTileSearch, occupied);
                        accum -= _dotInterval;
                    }

                    _playerFireAccum[key] = accum;
                }
                else
                {
                    _playerFireAccum.Remove(key);
                }
            }
        }

        private void TickSlimes(float deltaTime, StageModel stage)
        {
            var slimes = new List<SlimeModel>(_slimeRegistry.GetAll());

            foreach (var slime in slimes)
            {
                if (!slime.IsAlive) continue;

                var tileState = stage.GetTileState(slime.Position);

                if (tileState == TileState.OnFire)
                {
                    _slimeFireAccum.TryGetValue(slime.Id, out float accum);
                    accum += deltaTime;

                    if (accum >= _dotInterval)
                    {
                        // スライムは即死（HP=1）
                        slime.Kill();
                        _slimeRegistry.Remove(slime.Id);
                        _slimeFireAccum.Remove(slime.Id);
                        // 炎 DoT によるスライム死亡はドロップなし（撃破者不明）
                    }
                    else
                    {
                        _slimeFireAccum[slime.Id] = accum;
                    }
                }
                else
                {
                    _slimeFireAccum.Remove(slime.Id);
                }
            }
        }
    }
}
