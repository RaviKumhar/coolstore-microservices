using System;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace VND.Fw.Utils.Helpers
{
  /// <summary>
  /// https://tools.ietf.org/html/rfc6238
  /// </summary>
  public static class TotpHelper
  {
    private static readonly DateTime _unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static TimeSpan _timestep = TimeSpan.FromMinutes(3);
    private static readonly Encoding _encoding = new UTF8Encoding(false, true);

    private static int ComputeTotp(HashAlgorithm hashAlgorithm, ulong timestepNumber, string modifier)
    {
      // # of 0's = length of pin
      const int Mod = 1000000;

      // See https://tools.ietf.org/html/rfc4226
      // We can add an optional modifier
      byte[] timestepAsBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((long)timestepNumber));
      byte[] hash = hashAlgorithm.ComputeHash(ApplyModifier(timestepAsBytes, modifier));

      // Generate DT string
      int offset = hash[hash.Length - 1] & 0xf;
      Debug.Assert(offset + 4 < hash.Length);
      int binaryCode = (hash[offset] & 0x7f) << 24
                             | (hash[offset + 1] & 0xff) << 16
                             | (hash[offset + 2] & 0xff) << 8
                             | (hash[offset + 3] & 0xff);

      return binaryCode % Mod;
    }

    private static byte[] ApplyModifier(byte[] input, string modifier)
    {
      if (string.IsNullOrEmpty(modifier))
      {
        return input;
      }

      byte[] modifierBytes = _encoding.GetBytes(modifier);
      byte[] combined = new byte[checked(input.Length + modifierBytes.Length)];
      Buffer.BlockCopy(input, 0, combined, 0, input.Length);
      Buffer.BlockCopy(modifierBytes, 0, combined, input.Length, modifierBytes.Length);
      return combined;
    }

    // More info: https://tools.ietf.org/html/rfc6238#section-4
    private static ulong GetCurrentTimeStepNumber()
    {
      TimeSpan delta = DateTime.UtcNow - _unixEpoch;
      return (ulong)(delta.Ticks / _timestep.Ticks);
    }

    public static int GenerateCode(byte[] securityToken, string modifier = null)
    {
      if (securityToken == null)
      {
        throw new ArgumentNullException(nameof(securityToken));
      }

      // Allow a variance of no greater than 90 seconds in either direction
      ulong currentTimeStep = GetCurrentTimeStepNumber();
      using (HMACSHA1 hashAlgorithm = new HMACSHA1(securityToken))
      {
        return ComputeTotp(hashAlgorithm, currentTimeStep, modifier);
      }
    }

    public static bool ValidateCode(byte[] securityToken, int code, string modifier = null)
    {
      if (securityToken == null)
      {
        throw new ArgumentNullException(nameof(securityToken));
      }

      // Allow a variance of no greater than 90 seconds in either direction
      ulong currentTimeStep = GetCurrentTimeStepNumber();
      using (HMACSHA1 hashAlgorithm = new HMACSHA1(securityToken))
      {
        for (int i = -2; i <= 2; i++)
        {
          int computedTotp = ComputeTotp(hashAlgorithm, (ulong)((long)currentTimeStep + i), modifier);
          if (computedTotp == code)
          {
            return true;
          }
        }
      }

      // No match
      return false;
    }

    public static int GenerateCode(string securityToken, string modifier = null) => GenerateCode(Encoding.Unicode.GetBytes(securityToken), modifier);

    public static bool ValidateCode(string securityToken, int code, string modifier = null) => ValidateCode(Encoding.Unicode.GetBytes(securityToken), code, modifier);
  }
}