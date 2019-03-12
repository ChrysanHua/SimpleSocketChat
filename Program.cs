using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketSingleSend
{
    class Program
    {
        private const string ANSWER_FLAG = ">>OK<<";
        private const string IP_FLAG = ">>IP<<";
        private const int MAX_BYTE_SIZE = 256;
        private const int PORT = 10019;
        private const int AS_PORT = 11019;
        static readonly IPAddress localIP = Dns.GetHostAddresses(Dns.GetHostName()).Last();

        static bool sendFail = true;
        static Socket udpSender;
        static Socket udpReceiver;
        static Dictionary<IPAddress, IPAddress> senderDic;

        #region UtilMethod
        static bool SteadySend(string str, IPEndPoint targetIPE)
        {
            int count = 3;
            while (count > 0)
            {
                count--;
                if (UDPSend(str, targetIPE))
                {
#if DEBUG
                    Console.WriteLine("<<<<<<<<send '{0}' to: {1}", str, targetIPE);
#endif
                    return true;
                }
            }
            return false;
        }

        static IPAddress StrToIP(string ipStr)
        {
            try
            {
                return IPAddress.Parse(ipStr);
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.Message);
#endif
            }
            return null;
        }

        static IPEndPoint CreateIPE(IPAddress ip, int port)
        {
            return new IPEndPoint(ip, port);
        }

        static EndPoint CreateEmptyEP()
        {
            return CreateIPE(IPAddress.Any, 0);
        }

        #endregion

        static void Main(string[] args)
        {
            Console.WriteLine("1. Send ;\r\n2. Receive ;");
#if DEBUG
            Console.WriteLine("3. Loop Send ;");
#endif
            Console.WriteLine("Make a choice to do(1 or 2):");

            string choice = Console.ReadLine();
            if (choice == "1")
            {
                //do Send
                InitSocket(true, true);
                IPAddress targetIP = null;
                while (targetIP == null)
                {
                    Console.WriteLine("Enter the other side's IP:");
                    targetIP = StrToIP(Console.ReadLine().Trim());
                }
                Console.WriteLine("Write down your text:");
                Console.WriteLine("-----------------------------------");
                string str = null;
                do
                {
                    str = Console.ReadLine();
                    if (str.Length > (MAX_BYTE_SIZE / 2 - 8))
                    {
                        Console.WriteLine("<<<<<<<<<<<Too Long!");
                        continue;
                    }
                    if (str.Length == 0)
                    {
                        Console.WriteLine("<<<<<<<<<<<Can not be empty!");
                        continue;
                    }
                    if (!SendText(str, targetIP))
                        Console.WriteLine("<<<<<<<<<<<The other side may not have received!");
                } while (str != "exit");
            }
            else if (choice == "2")
            {
                //do Receive
                InitSocket(true, false);
                Console.WriteLine("Local IP is: " + localIP);
                Console.WriteLine("Waiting to receive the Msg...");
                Console.WriteLine("-----------------------------------");
                string msg = null;
                do
                {
                    msg = ReceiveText();
                    if (msg != null)
                        Console.WriteLine(msg);
                } while (msg != "exit");
            }
#if DEBUG
            else if (choice == "3")
            {
                //Loop Send for Test
                InitSocket(true, true);
                IPAddress targetIP = null;
                string str = null;
                int count = 0;
                while (targetIP == null)
                {
                    Console.WriteLine("Enter the other side's IP:");
                    targetIP = StrToIP(Console.ReadLine().Trim());
                }
                Console.WriteLine("-----------------------------------");
                while (true)
                {
                    str = $"Send Test {++count} -> {DateTime.Now.ToShortTimeString()}";
                    Console.WriteLine(str);
                    if (!SendText(str, targetIP))
                        Console.WriteLine("<<<<<<<<<<<The other side may not have received!");
                    Thread.Sleep(5000);
                }
            }
#endif
            udpReceiver?.Close();
            udpSender?.Close();
        }

        static string ReceiveText()
        {
            //UDP Mode
            EndPoint remoteIPE = CreateEmptyEP();
            string receiveStr = UDPReceive(ref remoteIPE);
            if (receiveStr == null)
                return receiveStr;
            //judge whether it is IP or not and check in
            if (IPCheckIn(receiveStr, remoteIPE))
                return null;
            //get the useful IP
            IPAddress answerIP = IPCheckOut(remoteIPE);
            if (answerIP == null)
                return receiveStr;
            //Send answer
            SteadySend(ANSWER_FLAG + receiveStr, CreateIPE(answerIP, AS_PORT));
            return receiveStr;
        }

        static bool SendText(string str, IPAddress targetIP)
        {
            //UDP Mode
            EndPoint remoteIPE = CreateEmptyEP();
            //send localIP to Receiver if sendFail
            if (sendFail)
                SendIP(targetIP);
            //clean up the buffer
            FlushReceiveBuf();
            if (UDPSend(str, CreateIPE(targetIP, PORT)))
            {
                //Receive answer
                string answerStr = null;
                do
                {
                    answerStr = UDPReceive(ref remoteIPE);
#if DEBUG
                    Console.WriteLine(">>>>>>>>>get '{0}' from: {1}", answerStr, remoteIPE);
#endif
                    if (answerStr != null &&
                        answerStr.StartsWith(ANSWER_FLAG) && answerStr.EndsWith(str))
                    {
                        sendFail = false;
                        return true;
                    }
                } while (answerStr != null);
            }
            sendFail = true;
            return false;
        }

        static bool UDPSend(string str, EndPoint targetIPE)
        {
            try
            {
                byte[] strByte = Encoding.UTF8.GetBytes(str);
                int sendLen = udpSender.SendTo(strByte, SocketFlags.None, targetIPE);
                if (sendLen == strByte.Length)
                    return true;
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.Message);
#endif
            }
            return false;
        }

        static string UDPReceive(ref EndPoint remoteIPE)
        {
            try
            {
                byte[] strByte = new byte[MAX_BYTE_SIZE];
                int receiveLen = udpReceiver.ReceiveFrom(strByte,
                    SocketFlags.None, ref remoteIPE);
                if (receiveLen > 0)
                    return Encoding.UTF8.GetString(strByte, 0, receiveLen);
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.Message);
#endif
            }
            return null;
        }

        static void InitSocket(bool udpMode, bool sendMode)
        {
            //Init Socket
            if (udpMode)
            {
                udpReceiver = new Socket(AddressFamily.InterNetwork,
                    SocketType.Dgram, ProtocolType.Udp);
                udpSender = new Socket(AddressFamily.InterNetwork,
                    SocketType.Dgram, ProtocolType.Udp)
                {
                    SendTimeout = 800
                };
                if (sendMode)
                {
                    udpReceiver.Bind(CreateIPE(localIP, AS_PORT));
                    udpReceiver.ReceiveTimeout = 2500;
                }
                else
                {
                    udpReceiver.Bind(CreateIPE(localIP, PORT));
                    senderDic = new Dictionary<IPAddress, IPAddress>();
                }
            }
        }

        static void FlushReceiveBuf()
        {
            //this method will only be invoked before receiving a answer
            EndPoint remoteIPE = CreateEmptyEP();
            //clean up the Receive Buffer
            while (udpReceiver.Available != 0)
            {
#if DEBUG
                Console.WriteLine("---------get '{0}' from: {1}",
                    UDPReceive(ref remoteIPE), remoteIPE);
#else
                UDPReceive(ref remoteIPE);
#endif
            }
        }

        static IPAddress IPCheckOut(EndPoint remoteIPE)
        {
            //get the useful IP if it has been checked in
            IPAddress remoteIP = ((IPEndPoint)remoteIPE).Address;
            //check if the IP has been checked in
            if (senderDic.ContainsKey(remoteIP))
                return senderDic[remoteIP];
            else
                return null;
        }

        static bool IPCheckIn(string receiveStr, EndPoint remoteIPE)
        {
            //check in the Sender's IP
            if (!receiveStr.StartsWith(IP_FLAG))
                return false;
            IPAddress receiveIP = null;
            //Value
            receiveIP = StrToIP(receiveStr.Substring(IP_FLAG.Length));
            if (receiveIP == null)
                return false;
            //Key
            IPAddress remoteIP = ((IPEndPoint)remoteIPE).Address;
            senderDic[remoteIP] = receiveIP;
            return true;
        }

        static void SendIP(IPAddress targetIP)
        {
            //this method will only be invoked before sending the text
            //send localIP to the Receiver
            SteadySend(IP_FLAG + localIP, CreateIPE(targetIP, PORT));
        }

    }
}
