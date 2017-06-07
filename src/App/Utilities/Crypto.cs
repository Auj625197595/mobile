﻿using Bit.App.Models;
using PCLCrypto;
using System;
using System.Linq;

namespace Bit.App.Utilities
{
    public static class Crypto
    {
        public static CipherString AesCbcEncrypt(byte[] plainBytes, SymmetricCryptoKey key)
        {
            if(key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if(plainBytes == null)
            {
                throw new ArgumentNullException(nameof(plainBytes));
            }

            var provider = WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithm.AesCbcPkcs7);
            var cryptoKey = provider.CreateSymmetricKey(key.EncKey);
            var iv = RandomBytes(provider.BlockLength);
            var encryptedBytes = WinRTCrypto.CryptographicEngine.Encrypt(cryptoKey, plainBytes, iv);
            var mac = key.MacKey != null ? ComputeMacBase64(encryptedBytes, iv, key.MacKey) : null;

            return new CipherString(key.EncryptionType, Convert.ToBase64String(iv),
                Convert.ToBase64String(encryptedBytes), mac);
        }

        public static byte[] AesCbcDecrypt(CipherString encyptedValue, SymmetricCryptoKey key)
        {
            if(key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if(encyptedValue == null)
            {
                throw new ArgumentNullException(nameof(encyptedValue));
            }

            if(key.MacKey != null && !string.IsNullOrWhiteSpace(encyptedValue.Mac))
            {
                var computedMacBytes = ComputeMac(encyptedValue.CipherTextBytes,
                    encyptedValue.InitializationVectorBytes, key.MacKey);
                if(!MacsEqual(key.MacKey, computedMacBytes, encyptedValue.MacBytes))
                {
                    throw new InvalidOperationException("MAC failed.");
                }
            }

            var provider = WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithm.AesCbcPkcs7);
            var cryptoKey = provider.CreateSymmetricKey(key.EncKey);
            var decryptedBytes = WinRTCrypto.CryptographicEngine.Decrypt(cryptoKey, encyptedValue.CipherTextBytes,
                encyptedValue.InitializationVectorBytes);
            return decryptedBytes;
        }

        public static byte[] RandomBytes(int length)
        {
            return WinRTCrypto.CryptographicBuffer.GenerateRandom(length);
        }

        private static string ComputeMacBase64(byte[] ctBytes, byte[] ivBytes, byte[] macKey)
        {
            var mac = ComputeMac(ctBytes, ivBytes, macKey);
            return Convert.ToBase64String(mac);
        }

        private static byte[] ComputeMac(byte[] ctBytes, byte[] ivBytes, byte[] macKey)
        {
            if(macKey == null)
            {
                throw new ArgumentNullException(nameof(macKey));
            }

            if(ctBytes == null)
            {
                throw new ArgumentNullException(nameof(ctBytes));
            }

            if(ivBytes == null)
            {
                throw new ArgumentNullException(nameof(ivBytes));
            }

            var algorithm = WinRTCrypto.MacAlgorithmProvider.OpenAlgorithm(MacAlgorithm.HmacSha256);
            var hasher = algorithm.CreateHash(macKey);
            hasher.Append(ivBytes.Concat(ctBytes).ToArray());
            var mac = hasher.GetValueAndReset();
            return mac;
        }

        // Safely compare two MACs in a way that protects against timing attacks (Double HMAC Verification).
        // ref: https://www.nccgroup.trust/us/about-us/newsroom-and-events/blog/2011/february/double-hmac-verification/
        private static bool MacsEqual(byte[] macKey, byte[] mac1, byte[] mac2)
        {
            var algorithm = WinRTCrypto.MacAlgorithmProvider.OpenAlgorithm(MacAlgorithm.HmacSha256);
            var hasher = algorithm.CreateHash(macKey);

            hasher.Append(mac1);
            mac1 = hasher.GetValueAndReset();

            hasher.Append(mac2);
            mac2 = hasher.GetValueAndReset();

            if(mac1.Length != mac2.Length)
            {
                return false;
            }

            for(int i = 0; i < mac2.Length; i++)
            {
                if(mac1[i] != mac2[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}