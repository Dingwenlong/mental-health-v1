using System.Security.Cryptography;
using System.Text;
using MentalHealth.Infrastructure.Identity;

namespace MentalHealth.UnitTests.Security;

public sealed class EncryptedSceneIdFactoryTests
{
    private const string Ekey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    [Fact]
    public void Create_encrypts_scene_id_with_a_fresh_iv_and_five_minute_expiry()
    {
        const long unixSeconds = 1_725_000_000;
        var factory = new EncryptedSceneIdFactory(Ekey);

        var first = factory.Create("admin-scene", unixSeconds);
        var second = factory.Create("admin-scene", unixSeconds);

        Assert.NotEqual(first, second);
        Assert.Equal("admin-scene&1725000000&300", Decrypt(first));
        Assert.Equal("admin-scene&1725000000&300", Decrypt(second));
    }

    [Fact]
    public void Create_rejects_an_ekey_that_does_not_decode_to_256_bits()
    {
        Assert.Throws<ArgumentException>(() => new EncryptedSceneIdFactory("AAECAwQ="));
    }

    private static string Decrypt(string encryptedSceneId)
    {
        var payload = Convert.FromBase64String(encryptedSceneId);
        Assert.True(payload.Length > 16);

        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(Ekey);
        aes.IV = payload[..16];
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(payload, 16, payload.Length - 16));
    }
}
