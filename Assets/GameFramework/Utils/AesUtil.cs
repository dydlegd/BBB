using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// 通用 AES 加解密工具类，支持字符串和字节数组的加解密，提供密钥生成、密码派生等功能。
/// <para>使用示例：</para>
/// <code>
/// // ===== 1. 生成随机 Key =====
/// byte[] key = AesUtil.GenerateKey();   // 256位(32字节)
///
/// // ===== 2. 字符串加解密（自动管理 IV，推荐）=====
/// string original = "Hello World";
/// string encrypted = AesUtil.Encrypt(original, key);        // Base64密文，IV 自动内嵌
/// string decrypted = AesUtil.Decrypt(encrypted, key);       // "Hello World"
///
/// // ===== 3. 字符串加解密（手动指定 IV）=====
/// byte[] iv = AesUtil.GenerateIV();
/// string encrypted2 = AesUtil.Encrypt(original, key, iv);
/// string decrypted2 = AesUtil.Decrypt(encrypted2, key, iv);
///
/// // ===== 4. 字节数组加解密 =====
/// byte[] data = Encoding.UTF8.GetBytes("Secret Data");
/// byte[] encryptedBytes = AesUtil.Encrypt(data, key);      // IV 自动内嵌
/// byte[] decryptedBytes = AesUtil.Decrypt(encryptedBytes, key);
///
/// // ===== 5. 基于密码加解密（无需记忆 Key，只需记住密码）=====
/// string cipher = AesUtil.EncryptWithPassword("原始文本", "MyPassword123");
/// string plain  = AesUtil.DecryptWithPassword(cipher, "MyPassword123");
/// </code>
/// </summary>
public static class AesUtil
{
    public const int DefaultKeySize = 256;
    public const int DefaultBlockSize = 128;
    public const int Pbkdf2Iterations = 10000;
    public const int SaltSize = 16;

    private const int KeyByteLength = DefaultKeySize / 8;
    private const int IVByteLength = DefaultBlockSize / 8;

    #region Key & IV Generation

    /// <summary>
    /// 生成随机 AES 密钥（256位 / 32字节）
    /// </summary>
    public static byte[] GenerateKey()
    {
        return GenerateRandomBytes(KeyByteLength);
    }

    /// <summary>
    /// 生成随机 AES 密钥，指定位数（128 / 192 / 256）
    /// </summary>
    public static byte[] GenerateKey(int keySize)
    {
        return GenerateRandomBytes(keySize / 8);
    }

    /// <summary>
    /// 生成随机 IV（128位 / 16字节）
    /// </summary>
    public static byte[] GenerateIV()
    {
        return GenerateRandomBytes(IVByteLength);
    }

    #endregion

    #region String Encrypt / Decrypt（自动管理 IV，不填 iv 参数）

    /// <summary>
    /// 加密字符串，IV 自动生成并嵌入密文。返回格式：Base64(IV[16] + 密文)
    /// </summary>
    public static string Encrypt(string plainText, byte[] key)
    {
        if (string.IsNullOrEmpty(plainText)) return null;
        byte[] encrypted = Encrypt(Encoding.UTF8.GetBytes(plainText), key);
        return encrypted != null ? Convert.ToBase64String(encrypted) : null;
    }

    /// <summary>
    /// 解密由 Encrypt(string, key) 生成的密文
    /// </summary>
    public static string Decrypt(string cipherText, byte[] key)
    {
        if (string.IsNullOrEmpty(cipherText)) return null;
        byte[] decrypted = Decrypt(Convert.FromBase64String(cipherText), key);
        return decrypted != null ? Encoding.UTF8.GetString(decrypted) : null;
    }

    #endregion

    #region String Encrypt / Decrypt（手动指定 IV）

    /// <summary>
    /// 加密字符串，手动指定 IV，返回 Base64 密文
    /// </summary>
    public static string Encrypt(string plainText, byte[] key, byte[] iv)
    {
        if (string.IsNullOrEmpty(plainText)) return null;
        byte[] encrypted = Encrypt(Encoding.UTF8.GetBytes(plainText), key, iv);
        return encrypted != null ? Convert.ToBase64String(encrypted) : null;
    }

    /// <summary>
    /// 解密 Base64 密文（对应手动指定 IV 的加密）
    /// </summary>
    public static string Decrypt(string cipherText, byte[] key, byte[] iv)
    {
        if (string.IsNullOrEmpty(cipherText)) return null;
        byte[] decrypted = Decrypt(Convert.FromBase64String(cipherText), key, iv);
        return decrypted != null ? Encoding.UTF8.GetString(decrypted) : null;
    }

    #endregion

    #region Byte[] Encrypt / Decrypt（自动管理 IV）

    /// <summary>
    /// 加密字节数组，IV 自动生成并嵌入密文。返回格式：IV[16] + 密文
    /// </summary>
    public static byte[] Encrypt(byte[] data, byte[] key)
    {
        if (data == null || data.Length == 0) return null;
        byte[] iv = GenerateIV();
        byte[] cipherData = Transform(data, aes => aes.CreateEncryptor(), key, iv);
        return cipherData != null ? Combine(iv, cipherData) : null;
    }

    /// <summary>
    /// 解密由 Encrypt(data, key) 生成的密文
    /// </summary>
    public static byte[] Decrypt(byte[] data, byte[] key)
    {
        if (data == null || data.Length <= IVByteLength) return null;
        byte[] iv = new byte[IVByteLength];
        byte[] cipherData = new byte[data.Length - IVByteLength];
        Buffer.BlockCopy(data, 0, iv, 0, IVByteLength);
        Buffer.BlockCopy(data, IVByteLength, cipherData, 0, cipherData.Length);
        return Transform(cipherData, aes => aes.CreateDecryptor(), key, iv);
    }

    #endregion

    #region Byte[] Encrypt / Decrypt（手动指定 IV）

    /// <summary>
    /// 加密字节数组（手动指定 IV）
    /// </summary>
    public static byte[] Encrypt(byte[] data, byte[] key, byte[] iv)
    {
        if (data == null || data.Length == 0) return null;
        return Transform(data, aes => aes.CreateEncryptor(), key, iv);
    }

    /// <summary>
    /// 解密字节数组（手动指定 IV）
    /// </summary>
    public static byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
    {
        if (data == null || data.Length == 0) return null;
        return Transform(data, aes => aes.CreateDecryptor(), key, iv);
    }

    #endregion

    #region AES Core

    /// <summary>
    /// AES 转换核心（加解密共用）
    /// </summary>
    private static byte[] Transform(byte[] data, Func<Aes, ICryptoTransform> createTransform, byte[] key, byte[] iv)
    {
        try
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (ICryptoTransform transform = createTransform(aes))
                using (MemoryStream ms = new MemoryStream())
                using (CryptoStream cs = new CryptoStream(ms, transform, CryptoStreamMode.Write))
                {
                    cs.Write(data, 0, data.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AesUtil] Transform failed: {e.Message}");
            return null;
        }
    }

    #endregion

    #region Password-Based Encrypt / Decrypt

    /// <summary>
    /// 使用密码加密字符串。密文格式：Base64(Salt[16] + IV[16] + 密文数据)
    /// </summary>
    public static string EncryptWithPassword(string plainText, string password)
    {
        if (string.IsNullOrEmpty(plainText) || string.IsNullOrEmpty(password)) return null;
        byte[] result = EncryptCore(Encoding.UTF8.GetBytes(plainText), password);
        return result != null ? Convert.ToBase64String(result) : null;
    }

    /// <summary>
    /// 使用密码解密由 EncryptWithPassword 生成的密文
    /// </summary>
    public static string DecryptWithPassword(string cipherText, string password)
    {
        if (string.IsNullOrEmpty(cipherText) || string.IsNullOrEmpty(password)) return null;
        byte[] result = DecryptCore(Convert.FromBase64String(cipherText), password);
        return result != null ? Encoding.UTF8.GetString(result) : null;
    }

    /// <summary>
    /// 使用密码加密字节数组
    /// </summary>
    public static byte[] EncryptBytesWithPassword(byte[] data, string password)
    {
        if (data == null || data.Length == 0 || string.IsNullOrEmpty(password)) return null;
        return EncryptCore(data, password);
    }

    /// <summary>
    /// 使用密码解密字节数组
    /// </summary>
    public static byte[] DecryptBytesWithPassword(byte[] fullData, string password)
    {
        if (fullData == null || fullData.Length < SaltSize + IVByteLength || string.IsNullOrEmpty(password))
            return null;
        return DecryptCore(fullData, password);
    }

    /// <summary>
    /// 密码加密核心：生成盐+IV → 派生密钥 → 加密 → 拼接为 salt + iv + 密文
    /// </summary>
    private static byte[] EncryptCore(byte[] data, string password)
    {
        try
        {
            byte[] salt = GenerateRandomBytes(SaltSize);
            byte[] iv = GenerateRandomBytes(IVByteLength);
            byte[] key = DeriveKey(password, salt);

            byte[] cipherData = Encrypt(data, key, iv);
            return Combine(salt, iv, cipherData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AesUtil] EncryptCore failed: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 密码解密核心：拆解 salt+iv+密文 → 派生密钥 → 解密
    /// </summary>
    private static byte[] DecryptCore(byte[] fullData, string password)
    {
        try
        {
            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IVByteLength];
            int cipherLen = fullData.Length - SaltSize - IVByteLength;
            byte[] cipherData = new byte[cipherLen];

            Buffer.BlockCopy(fullData, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(fullData, SaltSize, iv, 0, IVByteLength);
            Buffer.BlockCopy(fullData, SaltSize + IVByteLength, cipherData, 0, cipherLen);

            byte[] key = DeriveKey(password, salt);
            return Decrypt(cipherData, key, iv);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AesUtil] DecryptCore failed: {e.Message}");
            return null;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// 拼接多个字节数组
    /// </summary>
    private static byte[] Combine(params byte[][] arrays)
    {
        int totalLen = 0;
        for (int i = 0; i < arrays.Length; i++)
            totalLen += arrays[i].Length;

        byte[] result = new byte[totalLen];
        int offset = 0;
        for (int i = 0; i < arrays.Length; i++)
        {
            Buffer.BlockCopy(arrays[i], 0, result, offset, arrays[i].Length);
            offset += arrays[i].Length;
        }
        return result;
    }

    /// <summary>
    /// 通过 PBKDF2 从密码派生 AES 密钥
    /// </summary>
    public static byte[] DeriveKey(string password, byte[] salt, int keySize = DefaultKeySize)
    {
        using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(
            password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256))
        {
            return pbkdf2.GetBytes(keySize / 8);
        }
    }

    /// <summary>
    /// 生成密码学安全随机字节
    /// </summary>
    private static byte[] GenerateRandomBytes(int length)
    {
        byte[] bytes = new byte[length];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return bytes;
    }

    #endregion
}