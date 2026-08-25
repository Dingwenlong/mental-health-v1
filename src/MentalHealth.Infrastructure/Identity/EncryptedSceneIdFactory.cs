using System.Security.Cryptography;
using System.Text;

namespace MentalHealth.Infrastructure.Identity;

public sealed class EncryptedSceneIdFactory
{
    private readonly byte[] _key;

    public EncryptedSceneIdFactory(string ekey)
    {
        try
        {
            _key = Convert.FromBase64String(ekey);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Captcha ekey must be a Base64-encoded 256-bit key.", nameof(ekey), exception);
        }

        if (_key.Length != 32)
        {
            throw new ArgumentException("Captcha ekey must be a Base64-encoded 256-bit key.", nameof(ekey));
        }
    }

    public string Create(string sceneId) => Create(sceneId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public string Create(string sceneId, long unixSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneId);

        var plaintext = Encoding.UTF8.GetBytes($"{sceneId}&{unixSeconds}&300");
        var iv = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
        var payload = new byte[iv.Length + ciphertext.Length];
        Buffer.BlockCopy(iv, 0, payload, 0, iv.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, iv.Length, ciphertext.Length);
        return Convert.ToBase64String(payload);
    }
}
