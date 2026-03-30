using FloorBreaker.Shared.Application.Interfaces;

namespace FloorBreaker.Input.Application
{
    public static class KeyLabelResolver
    {
        public static (string fireKey, string breakKey, string moveKeys) GetBombKeyLabels(DeviceType deviceType)
        {
            return deviceType switch
            {
                DeviceType.KeyboardWasd   => ("[P]", "[L]", "WASD"),
                DeviceType.KeyboardArrows => ("[Num6]", "[Num2]", "矢印"),
                DeviceType.Gamepad        => ("[RB]", "[LB]", "スティック"),
                _                         => ("", "", ""),
            };
        }
    }
}
