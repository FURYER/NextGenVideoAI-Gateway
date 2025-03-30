using System.Security.Cryptography;
using System.Text;

namespace ApiGateway.Services
{
    public interface ITwoFactorAuthService
    {
        string GenerateSecret();
        string GenerateCode(string secret);
        bool ValidateCode(string secret, string code);
    }

    public class TwoFactorAuthService : ITwoFactorAuthService
    {
        private const int CodeLength = 6;
        private const int TimeStep = 30; // 30 секунд для каждого кода

        public string GenerateSecret()
        {
            var randomBytes = new byte[20];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes);
        }

        public string GenerateCode(string secret)
        {
            var secretBytes = Convert.FromBase64String(secret);
            var timeStep = (long)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond / TimeStep);
            var timeBytes = BitConverter.GetBytes(timeStep);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(timeBytes);

            using (var hmac = new HMACSHA1(secretBytes))
            {
                var hash = hmac.ComputeHash(timeBytes);
                var offset = hash[hash.Length - 1] & 0xf;
                var code = ((hash[offset] & 0x7f) << 24) |
                          ((hash[offset + 1] & 0xff) << 16) |
                          ((hash[offset + 2] & 0xff) << 8) |
                          (hash[offset + 3] & 0xff);
                return (code % (int)Math.Pow(10, CodeLength)).ToString().PadLeft(CodeLength, '0');
            }
        }

        public bool ValidateCode(string secret, string code)
        {
            var currentCode = GenerateCode(secret);
            var previousCode = GenerateCode(secret);
            var nextCode = GenerateCode(secret);

            return code == currentCode || code == previousCode || code == nextCode;
        }
    }
}