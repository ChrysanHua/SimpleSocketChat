using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SocketSingleSend
{
    class Messenger
    {
        public const int MAX_BYTE_SIZE = 512;

        private Socket socket;

        public Messenger(bool udpMode)
        {
            if (udpMode)
            {
                //UDP Mdoe
                socket = new Socket(AddressFamily.InterNetwork,
                    SocketType.Dgram, ProtocolType.Udp)
                {
                    SendTimeout = 800
                };
            }
        }

        public Messenger(bool udpMode, EndPoint bindIPE, int receiveTimeout = 0)
            :this(udpMode)
        {
            socket.Bind(bindIPE);
            if (receiveTimeout != 0)
                socket.ReceiveTimeout = receiveTimeout;
        }

        public void Close()
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }

        #region UtilMethod
        public static byte[] ConcatByte(byte[] frontByte, byte[] laterByte)
        {
            byte[] allByte = new byte[frontByte.Length + laterByte.Length];
            frontByte.CopyTo(allByte, 0);
            laterByte.CopyTo(allByte, frontByte.Length);
            return allByte;
        }

        public static byte[] SubByte(byte[] dataByte, int startIndex, int count)
        {
            byte[] partByte = new byte[count];
            Array.Copy(dataByte, startIndex, partByte, 0, count);
            return partByte;
        }

        public static byte[] SplitByte(byte[] dataByte, int splitPoint, out byte[] laterByte)
        {
            //return the front part
            laterByte = SubByte(dataByte, splitPoint, dataByte.Length - splitPoint);
            return SubByte(dataByte, 0, splitPoint);
        }

        #endregion

        public bool UDPSend(byte[] strByte, EndPoint targetIPE, bool firstSend = true)
        {
            bool overMax = false;
            byte[] remainByte = null;
            if (strByte.Length >= MAX_BYTE_SIZE)
            {
                overMax = true;
                if (firstSend)
                {
                    //add byteLength to head
                    int totalLen = strByte.Length;
                    strByte = ConcatByte(CryptoUtil.IntToByte(totalLen), strByte);
#if DEBUG
                    Console.WriteLine("--------concat {0} Byte, ready to send first part...",
                        totalLen);
#endif
                }
                //get the front part(MAX_BYTE_SIZE) to send and save the later part
                strByte = SplitByte(strByte, MAX_BYTE_SIZE, out remainByte);
            }
            try
            {
                int sendLen = socket.SendTo(strByte, SocketFlags.None, targetIPE);
                if (sendLen == strByte.Length)
                {
#if DEBUG
                    Console.WriteLine("--------success to send {0} Byte, overMax: {1}",
                        sendLen, overMax);
#endif
                    if (overMax && remainByte.Length != 0)
                        return UDPSend(remainByte, targetIPE, false);
                    else
                        return true;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.Message);
#endif
            }
            return false;
        }

        public bool UDPSend(string str, EndPoint targetIPE)
        {
            //Encrypt here
            return UDPSend(CryptoUtil.StrToByte(CryptoUtil.Encrypt(str)), targetIPE);
        }

        public bool SteadySend(string str, EndPoint targetIPE)
        {
            int count = 3;
            while (count > 0)
            {
                count--;
                if (UDPSend(str, targetIPE))
                    return true;
            }
            return false;
        }


        public byte[] UDPReceive(ref EndPoint remoteIPE, int byteSize)
        {
            int remainLen = 0;
            if (byteSize > MAX_BYTE_SIZE)
            {
                remainLen = byteSize - MAX_BYTE_SIZE;
                byteSize = MAX_BYTE_SIZE;
            }
            try
            {
                byte[] strByte = new byte[byteSize];
                int strLen = socket.ReceiveFrom(strByte,
                    SocketFlags.None, ref remoteIPE);
#if DEBUG
                Console.WriteLine("--------get one part with length: {0}", strLen);
#endif
                if (strLen > 0)
                {
                    strByte = SubByte(strByte, 0, strLen);
                    if (remainLen != 0)
                    {
                        //continue to receive and concat the remainder byte
                        IPAddress beforeIP = ((IPEndPoint)remoteIPE).Address;
                        byte[] remainByte = UDPReceive(ref remoteIPE, remainLen);
                        IPAddress afterIP = ((IPEndPoint)remoteIPE).Address;
                        if (!beforeIP.Equals(afterIP))
                            throw new Exception("Information confusion between different senders");
                        else
                            strByte = ConcatByte(strByte, remainByte);
                    }
                    return strByte;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.Message);
#endif
            }
            return null;
        }

        public string UDPReceive(ref EndPoint remoteIPE)
        {
            byte[] receiveByte = UDPReceive(ref remoteIPE, MAX_BYTE_SIZE);
            if (receiveByte != null && receiveByte.Length == MAX_BYTE_SIZE)
            {
                //get byteLength from the head
                int totalLen = CryptoUtil.ByteToInt(receiveByte);
                receiveByte = SubByte(receiveByte, 4, receiveByte.Length - 4);
#if DEBUG
                Console.WriteLine("--------totalLength should be: {0}", totalLen);
#endif
                //continue to receive
                IPAddress beforeIP = ((IPEndPoint)remoteIPE).Address;
                byte[] remainByte = UDPReceive(ref remoteIPE, totalLen - receiveByte.Length);
                IPAddress afterIP = ((IPEndPoint)remoteIPE).Address;
                if (remainByte != null && beforeIP.Equals(afterIP))
                {
                    //concat the remainder byte
                    receiveByte = ConcatByte(receiveByte, remainByte);
#if DEBUG
                    Console.WriteLine("--------success receive {0} Byte from: {1}",
                        receiveByte.Length, remoteIPE);
#endif
                }
                else
                    receiveByte = null;
            }
            if (receiveByte != null)
            {
                //Decrypt here
                string msg = CryptoUtil.Decrypt(CryptoUtil.ByteToStr(receiveByte));
#if DEBUG
                Console.WriteLine(">>>>>>>>get '{0}' from: {1}", msg, remoteIPE);
#endif
                return msg;
            }
            return null;
        }

        public string ReceiveLastOne(ref EndPoint remoteIPE)
        {
            //wait to get the last Msg from the Receive Buffer
            string msg = null;
            do
            {
                msg = UDPReceive(ref remoteIPE);
            } while (socket.Available != 0);
            return msg;
        }

        public void FlushReceiveBuf()
        {
            //this method will only be invoked before receiving a answer
            EndPoint remoteIPE = new IPEndPoint(IPAddress.Any, 0);
            //clean up the Receive Buffer
            while (socket.Available != 0)
            {
                UDPReceive(ref remoteIPE);
            }
        }




    }
}
