using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// 通用加解密工具类。
/// 提供 AES 对称加解密、哈希（MD5/SHA1/SHA256/SHA512）、HMAC、Base64 编解码、XOR 轻量混淆等功能。
/// AES 采用 CBC 模式 + 随机 IV + PBKDF2 派生密钥，兼顾安全性与易用性。
/// </summary>
public static class CryptoUtility
{
    private const string Tag = "[CryptoUtility]";

    private const int AesKeySize = 256;          // AES-256
    private const int AesBlockSize = 16;         // AES 块大小（字节）
    private const int SaltSize = 16;             // PBKDF2 盐长度（字节）
    private const int Pbkdf2Iterations = 10000;  // PBKDF2 迭代次数

    #region AES 字符串加解密

    /// <summary>
    /// 使用口令对字符串进行 AES 加密，返回 Base64 字符串。
    /// 内部生成随机盐与随机 IV，并依次拼接到密文头部（盐 + IV + 密文）。
    /// </summary>
    /// <param name="plainText">明文</param>
    /// <param name="passphrase">口令，任意长度</param>
    /// <returns>Base64 密文，失败返回 null</returns>
    public static string AesEncrypt(string plainText, string passphrase)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            Debug.LogWarning($"{Tag} AesEncrypt: 明文为空");
            return null;
        }
        if (string.IsNullOrEmpty(passphrase))
        {
            Debug.LogError($"{Tag} AesEncrypt: 口令为空");
            return null;
        }

        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] salt = GenerateRandomBytes(SaltSize);
            byte[] iv = GenerateRandomBytes(AesBlockSize);
            byte[] key = DeriveKey(passphrase, salt);

            byte[] cipherBytes;
            using (var aes = Aes.Create())
            {
                aes.KeySize = AesKeySize;
                aes.BlockSize = AesBlockSize * 8;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;
                using (var encryptor = aes.CreateEncryptor())
                {
                    cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                }
            }

            byte[] result = new byte[SaltSize + AesBlockSize + cipherBytes.Length];
            Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
            Buffer.BlockCopy(iv, 0, result, SaltSize, AesBlockSize);
            Buffer.BlockCopy(cipherBytes, 0, result, SaltSize + AesBlockSize, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} AesEncrypt 失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 使用口令对 Base64 密文字符串进行 AES 解密，返回明文。
    /// </summary>
    /// <param name="cipherBase64">Base64 密文</param>
    /// <param name="passphrase">口令，必须与加密时一致</param>
    /// <returns>明文，失败返回 null</returns>
    public static string AesDecrypt(string cipherBase64, string passphrase)
    {
        if (string.IsNullOrEmpty(cipherBase64))
        {
            Debug.LogWarning($"{Tag} AesDecrypt: 密文为空");
            return null;
        }
        if (string.IsNullOrEmpty(passphrase))
        {
            Debug.LogError($"{Tag} AesDecrypt: 口令为空");
            return null;
        }

        try
        {
            byte[] data = Convert.FromBase64String(cipherBase64);
            if (data.Length < SaltSize + AesBlockSize)
            {
                Debug.LogError($"{Tag} AesDecrypt: 密文长度不合法");
                return null;
            }

            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[AesBlockSize];
            byte[] cipherBytes = new byte[data.Length - SaltSize - AesBlockSize];
            Buffer.BlockCopy(data, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(data, SaltSize, iv, 0, AesBlockSize);
            Buffer.BlockCopy(data, SaltSize + AesBlockSize, cipherBytes, 0, cipherBytes.Length);

            byte[] key = DeriveKey(passphrase, salt);
            byte[] plainBytes;
            using (var aes = Aes.Create())
            {
                aes.KeySize = AesKeySize;
                aes.BlockSize = AesBlockSize * 8;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;
                using (var decryptor = aes.CreateDecryptor())
                {
                    plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                }
            }

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} AesDecrypt 失败: {e.Message}");
            return null;
        }
    }

    #endregion

    #region AES 字节加解密

    /// <summary>
    /// 使用原始密钥（32 字节）对字节数组进行 AES 加密。
    /// 内部生成随机 IV 并拼接到密文头部（IV + 密文）。
    /// </summary>
    /// <param name="plainBytes">明文字节</param>
    /// <param name="key">密钥，长度需为 16/24/32 字节</param>
    /// <returns>IV + 密文，失败返回 null</returns>
    public static byte[] AesEncrypt(byte[] plainBytes, byte[] key)
    {
        if (plainBytes == null || plainBytes.Length == 0)
        {
            Debug.LogWarning($"{Tag} AesEncrypt(bytes): 明文为空");
            return null;
        }
        if (!IsValidAesKeyLength(key))
        {
            Debug.LogError($"{Tag} AesEncrypt(bytes): 密钥长度不合法（需 16/24/32 字节）");
            return null;
        }

        try
        {
            byte[] iv = GenerateRandomBytes(AesBlockSize);
            byte[] cipherBytes;
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;
                using (var encryptor = aes.CreateEncryptor())
                {
                    cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                }
            }

            byte[] result = new byte[AesBlockSize + cipherBytes.Length];
            Buffer.BlockCopy(iv, 0, result, 0, AesBlockSize);
            Buffer.BlockCopy(cipherBytes, 0, result, AesBlockSize, cipherBytes.Length);
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} AesEncrypt(bytes) 失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 使用原始密钥对 IV+密文 进行 AES 解密，返回明文字节。
    /// </summary>
    /// <param name="cipherBytes">IV + 密文</param>
    /// <param name="key">密钥，需与加密时一致</param>
    /// <returns>明文字节，失败返回 null</returns>
    public static byte[] AesDecrypt(byte[] cipherBytes, byte[] key)
    {
        if (cipherBytes == null || cipherBytes.Length < AesBlockSize)
        {
            Debug.LogWarning($"{Tag} AesDecrypt(bytes): 密文长度不合法");
            return null;
        }
        if (!IsValidAesKeyLength(key))
        {
            Debug.LogError($"{Tag} AesDecrypt(bytes): 密钥长度不合法（需 16/24/32 字节）");
            return null;
        }

        try
        {
            byte[] iv = new byte[AesBlockSize];
            byte[] body = new byte[cipherBytes.Length - AesBlockSize];
            Buffer.BlockCopy(cipherBytes, 0, iv, 0, AesBlockSize);
            Buffer.BlockCopy(cipherBytes, AesBlockSize, body, 0, body.Length);

            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;
                using (var decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(body, 0, body.Length);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} AesDecrypt(bytes) 失败: {e.Message}");
            return null;
        }
    }

    #endregion

    #region 密钥生成

    /// <summary>
    /// 生成随机 AES 密钥（32 字节）并以 Base64 返回。
    /// </summary>
    /// <returns>Base64 密钥</returns>
    public static string GenerateAesKeyBase64()
    {
        byte[] key = GenerateRandomBytes(AesKeySize / 8);
        return Convert.ToBase64String(key);
    }

    /// <summary>
    /// 生成指定长度的随机字节数组。
    /// </summary>
    /// <param name="length">字节长度</param>
    /// <returns>随机字节数组</returns>
    public static byte[] GenerateRandomBytes(int length)
    {
        byte[] bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return bytes;
    }

    #endregion

    #region 哈希

    /// <summary>
    /// 计算字符串的 MD5 哈希（小写十六进制）。
    /// </summary>
    public static string ComputeMD5(string text)
    {
        if (text == null)
        {
            return null;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        return ComputeHashHex(MD5.Create(), bytes);
    }

    /// <summary>
    /// 计算字符串的 SHA256 哈希（小写十六进制）。
    /// </summary>
    public static string ComputeSHA256(string text)
    {
        if (text == null)
        {
            return null;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        return ComputeHashHex(SHA256.Create(), bytes);
    }

    /// <summary>
    /// 计算字符串的 SHA512 哈希（小写十六进制）。
    /// </summary>
    public static string ComputeSHA512(string text)
    {
        if (text == null)
        {
            return null;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        return ComputeHashHex(SHA512.Create(), bytes);
    }

    /// <summary>
    /// 计算文件的 MD5 哈希（小写十六进制）。
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>哈希字符串，失败返回 null</returns>
    public static string ComputeFileMD5(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                return ToHex(md5.ComputeHash(stream));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} ComputeFileMD5 失败 [{filePath}]: {e.Message}");
            return null;
        }
    }

    #endregion

    #region HMAC

    /// <summary>
    /// 计算 HMAC-SHA256 消息认证码（小写十六进制）。
    /// 用于验证消息完整性与真实性。
    /// </summary>
    /// <param name="text">消息</param>
    /// <param name="key">密钥</param>
    /// <returns>HMAC 十六进制字符串，失败返回 null</returns>
    public static string ComputeHMACSHA256(string text, string key)
    {
        if (text == null || string.IsNullOrEmpty(key))
        {
            Debug.LogError($"{Tag} ComputeHMACSHA256: 参数为空");
            return null;
        }

        try
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] msgBytes = Encoding.UTF8.GetBytes(text);
            using (var hmac = new HMACSHA256(keyBytes))
            {
                return ToHex(hmac.ComputeHash(msgBytes));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} ComputeHMACSHA256 失败: {e.Message}");
            return null;
        }
    }

    #endregion

    #region Base64 编解码

    /// <summary>
    /// 将字符串编码为 Base64。
    /// </summary>
    public static string Base64Encode(string text, Encoding encoding = null)
    {
        if (text == null)
        {
            return null;
        }
        encoding = encoding ?? Encoding.UTF8;
        return Convert.ToBase64String(encoding.GetBytes(text));
    }

    /// <summary>
    /// 将 Base64 字符串解码为原文。
    /// </summary>
    public static string Base64Decode(string base64, Encoding encoding = null)
    {
        if (string.IsNullOrEmpty(base64))
        {
            return null;
        }
        encoding = encoding ?? Encoding.UTF8;
        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            return encoding.GetString(bytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} Base64Decode 失败: {e.Message}");
            return null;
        }
    }

    #endregion

    #region XOR 轻量混淆

    /// <summary>
    /// 使用 XOR 对字符串进行轻量混淆，返回 Base64 字符串。
    /// 注意：XOR 不具备密码学安全性，仅用于简单遮蔽，不要用于敏感数据保护。
    /// </summary>
    /// <param name="plain">明文</param>
    /// <param name="key">混淆口令</param>
    /// <returns>Base64 结果，失败返回 null</returns>
    public static string XorEncrypt(string plain, string key)
    {
        if (plain == null || string.IsNullOrEmpty(key))
        {
            Debug.LogError($"{Tag} XorEncrypt: 参数为空");
            return null;
        }

        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plain);
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            for (int i = 0; i < plainBytes.Length; i++)
            {
                plainBytes[i] = (byte)(plainBytes[i] ^ keyBytes[i % keyBytes.Length]);
            }
            return Convert.ToBase64String(plainBytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} XorEncrypt 失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 使用 XOR 对 Base64 字符串进行还原。
    /// 与 <see cref="XorEncrypt"/> 配对使用，口令必须一致。
    /// </summary>
    public static string XorDecrypt(string cipherBase64, string key)
    {
        if (string.IsNullOrEmpty(cipherBase64) || string.IsNullOrEmpty(key))
        {
            Debug.LogError($"{Tag} XorDecrypt: 参数为空");
            return null;
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(cipherBase64);
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(bytes[i] ^ keyBytes[i % keyBytes.Length]);
            }
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} XorDecrypt 失败: {e.Message}");
            return null;
        }
    }

    #endregion

    #region 私有工具

    private static byte[] DeriveKey(string passphrase, byte[] salt)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(passphrase);
        using (var derive = new Rfc2898DeriveBytes(passwordBytes, salt, Pbkdf2Iterations))
        {
            return derive.GetBytes(AesKeySize / 8);
        }
    }

    private static bool IsValidAesKeyLength(byte[] key)
    {
        if (key == null)
        {
            return false;
        }
        int bits = key.Length * 8;
        return bits == 128 || bits == 192 || bits == 256;
    }

    private static string ComputeHashHex(HashAlgorithm algorithm, byte[] bytes)
    {
        using (algorithm)
        {
            return ToHex(algorithm.ComputeHash(bytes));
        }
    }

    private static string ToHex(byte[] bytes)
    {
        StringBuilder sb = new StringBuilder(bytes.Length * 2);
        for (int i = 0; i < bytes.Length; i++)
        {
            sb.Append(bytes[i].ToString("x2"));
        }
        return sb.ToString();
    }

    #endregion
}