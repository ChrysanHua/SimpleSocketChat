using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SocketSingleSend
{
    static class CryptoUtil
    {
        //Because of AesCryptoServiceProvider.KeySize = 256
        private const int DEFAULT_KEYSIZE = 256;

        private static byte[] keyByte;
        private static byte[] ivByte;
        private static string key;
        private static string iv;

        public static string Key
        {
            get => key;
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentNullException("Key");
                if (value.Length != (DEFAULT_KEYSIZE / 16))
                    throw new ArgumentException("Key");
                key = value;
                keyByte = StrToByte(key);
            }
        }

        public static string IV
        {
            get => iv;
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentNullException("IV");
                iv = value;
                ivByte = StrToByte(iv);
            }
        }

        static CryptoUtil()
        {
            using (AesCryptoServiceProvider aesCSP = new AesCryptoServiceProvider())
            {
                keyByte = aesCSP.Key;
                ivByte = aesCSP.IV;
            }
        }

        #region UtilMethod
        public static byte[] StrToByte(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        public static string ByteToStr(byte[] strByte, int count = 0)
        {
            if (count == 0)
                return Encoding.UTF8.GetString(strByte);
            else
                return Encoding.UTF8.GetString(strByte, 0, count);
        }

        public static string ByteToBase64(byte[] strByte)
        {
            return Convert.ToBase64String(strByte);
        }

        public static byte[] Base64ToByte(string base64Str)
        {
            return Convert.FromBase64String(base64Str);
        }

        public static string ByteToHexStr(byte[] strByte)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in strByte)
            {
                sb.Append(item.ToString("x2"));
            }
            return sb.ToString();
        }

        public static byte[] IntToByte(int num)
        {
            return BitConverter.GetBytes(num);
        }

        public static int ByteToInt(byte[] numByte)
        {
            return BitConverter.ToInt32(numByte, 0);
        }

        #endregion

        public static void Init(string key, string iv)
        {
            Key = key;
            IV = iv;
        }

        public static void Init(byte[] keyBuf, byte[] ivBuf)
        {
            if (keyBuf == null || ivBuf == null)
                throw new ArgumentNullException("keyBuf / ivBuf");
            if (keyBuf.Length != (DEFAULT_KEYSIZE / 8))
                throw new ArgumentException("keyBuf");
            key = (iv = null);
            keyByte = keyBuf;
            ivByte = ivBuf;
        }

        public static byte[] AESCrypto(byte[] inputByte, bool encrypt)
        {
            if (inputByte == null || inputByte.Length == 0)
                throw new ArgumentNullException("inputByte");
            using (AesCryptoServiceProvider aesCSP = new AesCryptoServiceProvider())
            {
                try
                {
                    ICryptoTransform cryptor = encrypt ? aesCSP.CreateEncryptor(keyByte, ivByte)
                        : aesCSP.CreateDecryptor(keyByte, ivByte);
                    byte[] outputByte = cryptor.TransformFinalBlock(inputByte,
                        0, inputByte.Length);
                    aesCSP.Clear();
                    return outputByte;
                }
#if DEBUG
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
#else
                catch { }
#endif
            }
            return inputByte;
        }

        public static byte[] Encrypt(byte[] dataByte)
        {
            //AES Mode
            return AESCrypto(dataByte, true);
        }

        public static byte[] Encrypt(byte[] dataByte, byte[] keyByte, byte[] ivByte)
        {
            Init(keyByte, ivByte);
            return Encrypt(dataByte);
        }

        public static string Encrypt(string str)
        {
            //ToBase64String
            return ByteToBase64(Encrypt(StrToByte(str)));
        }

        public static string Encrypt(string str, string key, string iv)
        {
            Init(key, iv);
            return Encrypt(str);
        }

        public static byte[] Decrypt(byte[] dataByte)
        {
            //AES Mode
            return AESCrypto(dataByte, false);
        }

        public static byte[] Decrypt(byte[] dataByte, byte[] keyByte, byte[] ivByte)
        {
            Init(keyByte, ivByte);
            return Decrypt(dataByte);
        }

        public static string Decrypt(string enStr)
        {
            //FromBase64String
            return ByteToStr(Decrypt(Base64ToByte(enStr)));
        }

        public static string Decrypt(string enStr, string key, string iv)
        {
            Init(key, iv);
            return Decrypt(enStr);
        }

        #region HashMethod
        public static byte[] SHA256Hash(byte[] strByte)
        {
            if (strByte == null || strByte.Length == 0)
                throw new ArgumentNullException("strByte");
            using (SHA256Managed sha = new SHA256Managed())
            {
                try
                {
                    byte[] hashByte = sha.ComputeHash(strByte);
                    sha.Clear();
                    return hashByte;
                }
#if DEBUG
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
#else
                catch { }
#endif
            }
            return strByte;
        }

        public static string SHA256Hash(string str)
        {
            return ByteToHexStr(SHA256Hash(StrToByte(str)));
        }

        #endregion

    }
}
