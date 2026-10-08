using System.Security.Cryptography;
using System.Text;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;
internal sealed class WindowsSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("LinserHungary.DHL.1");
    public byte[] Protect(byte[] value) => ProtectedData.Protect(value, Entropy, DataProtectionScope.CurrentUser);
    public byte[] Unprotect(byte[] value) => ProtectedData.Unprotect(value, Entropy, DataProtectionScope.CurrentUser);
}
