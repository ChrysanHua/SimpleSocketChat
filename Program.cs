using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketSingleSend
{
    class Program
    {
        private const string ANSWER_FLAG = ">>OK<<";
        private const string IP_FLAG = ">>IP<<";
        private const string CRYPTO_KEY = "SimpleSocketChat";
        private const int MAX_STR_LEN = 120;
        private const int PORT = 10019;
        private const int AS_PORT = 11019;
        private const int BC_PORT = 19019;
        static readonly IPAddress localIP = Dns.GetHostAddresses(Dns.GetHostName()).Last();

        static bool sendFail = true;
        static Messenger udpSender;
        static Messenger udpReceiver;
        static Messenger broadcaster;
        static Dictionary<IPAddress, IPAddress> senderDic;

        #region UtilMethod
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

            string choice = Console.ReadLine().Trim();
            if (choice == "1")
            {
                //do Send
                InitMessenger(true, true);
                IPAddress targetIP = null;
                while (targetIP == null)
                {
                    Console.WriteLine("Enter the other side's IP or press Enter directly:");
                    string ipStr = Console.ReadLine().Trim();
                    if (string.IsNullOrEmpty(ipStr))
                    {
                        Console.WriteLine("Waiting to auto connect...");
                        EndPoint remoteIPE = CreateEmptyEP();
                        ipStr = broadcaster.ReceiveBroadcast(ref remoteIPE);
                        if (ipStr != null && ipStr.StartsWith(IP_FLAG))
                            ipStr = ipStr.Substring(IP_FLAG.Length);
                    }
                    targetIP = StrToIP(ipStr);
                }
                Console.WriteLine("Connect to Receiver: {0}", targetIP);
                Console.WriteLine("Write down your text:");
                Console.WriteLine("-----------------------------------");
                string str = null;
                do
                {
                    str = Console.ReadLine();
                    if (str == null)
                        break;
                    else if (str.Length > MAX_STR_LEN)
                    {
                        Console.WriteLine("<<<<<<<<<<<Too Long!");
                        continue;
                    }
                    else if (str.Length == 0)
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
                InitMessenger(true, false);
                Console.WriteLine("Local IP is: " + localIP);
                Console.WriteLine("Waiting to receive the Msg...");
                broadcaster.Broadcasting(IP_FLAG + localIP, BC_PORT);
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
                InitMessenger(true, true);
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
            broadcaster?.Close();
            udpReceiver?.Close();
            udpSender?.Close();
        }

        static string ReceiveText()
        {
            //UDP Mode
            EndPoint remoteIPE = CreateEmptyEP();
            string receiveStr = udpReceiver.UDPReceive(ref remoteIPE);
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
            udpSender.SteadySend(ANSWER_FLAG + CryptoUtil.SHA256Hash(receiveStr),
                CreateIPE(answerIP, AS_PORT));
            return receiveStr;
        }

        static bool SendText(string str, IPAddress targetIP)
        {
            //UDP Mode
            EndPoint remoteIPE = CreateEmptyEP();
            //send localIP to Receiver if sendFail
            if (sendFail)
                udpSender.SteadySend(IP_FLAG + localIP, CreateIPE(targetIP, PORT));
            //clean up the buffer
            udpReceiver.FlushReceiveBuf();
            if (udpSender.UDPSend(str, CreateIPE(targetIP, PORT)))
            {
                //Receive answer
                if (udpReceiver.ReceiveLastOne(ref remoteIPE) ==
                    ANSWER_FLAG + CryptoUtil.SHA256Hash(str))
                {
                    sendFail = false;
                    return true;
                }
            }
            sendFail = true;
            return false;
        }

        static void InitMessenger(bool udpMode, bool sendMode)
        {
            CryptoUtil.Init(CRYPTO_KEY, CRYPTO_KEY);
            //Init Socket
            udpSender = new Messenger(udpMode);
            if (sendMode)
            {
                udpReceiver = new Messenger(udpMode, CreateIPE(localIP, AS_PORT), 2500);
                broadcaster = new Messenger(udpMode, CreateIPE(localIP, BC_PORT));
            }
            else
            {
                udpReceiver = new Messenger(udpMode, CreateIPE(localIP, PORT));
                broadcaster = new Messenger(udpMode);
                senderDic = new Dictionary<IPAddress, IPAddress>();
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


    }
}
