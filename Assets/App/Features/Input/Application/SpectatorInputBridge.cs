using FloorBreaker.Input.Infrastructure;
using FloorBreaker.Shared.Application.Interfaces;

namespace FloorBreaker.Input.Application
{
    /// <summary>
    /// 観戦カメラ用の入力読み取り結果を保持する。
    /// SpectatorInputReader から読み取り、MatchTickRunner が SpectatorCamera に配送する。
    /// </summary>
    public sealed class SpectatorInputBridge
    {
        private readonly SpectatorInputReader _reader;

        public SpectatorInputBridge(SpectatorInputReader reader)
        {
            _reader = reader;
        }

        /// <summary>指定デバイスの入力状態を読み取る。</summary>
        public SpectatorInputReader.InputState ReadInput(DeviceType deviceType, int gamepadIndex)
        {
            return _reader.Read(deviceType, gamepadIndex);
        }
    }
}
