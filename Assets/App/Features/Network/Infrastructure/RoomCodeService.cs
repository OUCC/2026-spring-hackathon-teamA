using FloorBreaker.Shared.Application.Interfaces;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// ルームコードの生成・正規化・検証を行うユーティリティ。
    /// 紛らわしい文字 (0/O, 1/I/L) を除外した25文字セットを使用する。
    /// </summary>
    public static class RoomCodeService
    {
        private const string CharSet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        private const int CodeLength = 5;

        public static string GenerateRoomCode(IRandomProvider random)
        {
            var chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++)
                chars[i] = CharSet[random.Range(0, CharSet.Length)];
            return new string(chars);
        }

        public static string NormalizeInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return input.Trim().ToUpperInvariant();
        }

        public static bool IsValid(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != CodeLength)
                return false;

            foreach (char c in code)
            {
                if (CharSet.IndexOf(c) < 0)
                    return false;
            }
            return true;
        }
    }
}
