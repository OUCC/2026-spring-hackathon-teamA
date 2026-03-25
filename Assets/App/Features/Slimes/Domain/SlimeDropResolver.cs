using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;
using FloorBreaker.Upgrades.Domain;

namespace FloorBreaker.Slimes.Domain
{
    public sealed class SlimeDropResolver
    {
        private readonly UpgradeCatalog _catalog;
        private readonly UpgradeApplyService _applyService;

        public SlimeDropResolver(UpgradeCatalog catalog, UpgradeApplyService applyService)
        {
            _catalog = catalog;
            _applyService = applyService;
        }

        /// <summary>
        /// スライム死亡時のドロップ処理。
        /// killer が null の場合（ステージ縮小死亡）はドロップなし。
        /// </summary>
        public void Resolve(SlimeModel slime, PlayerModel killer, IRandomProvider random)
        {
            if (killer == null) return;

            switch (slime.Type)
            {
                case SlimeType.Normal:
                    killer.Stats.AddCoins(1);
                    break;
                case SlimeType.Gold:
                    killer.Stats.AddCoins(5);
                    break;
                case SlimeType.Red:
                    ApplyRandomUpgrade(killer, random);
                    break;
            }
        }

        private void ApplyRandomUpgrade(PlayerModel player, IRandomProvider random)
        {
            var stackables = _catalog.GetUnlimitedStackables();
            if (stackables.Count == 0) return;

            int idx = random.Range(0, stackables.Count);
            _applyService.Apply(stackables[idx].Id, player);
        }
    }
}
