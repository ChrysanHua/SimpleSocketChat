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
        private const int MAX_BYTE_SIZE = 256;
        static readonly IPAddress localIP = Dns.GetHostAddresses(Dns.GetHostName())[1];
        static readonly int PORT = 10019;
        static readonly int AS_PORT = 11019;

        static Socket udpSender;
        static Socket udpReceiver;

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
                IPAddress targetIP = null;
                bool flag = false;
                while (!flag)
                {
                    Console.WriteLine("Enter the other side's IP:");
                    try
                    {
                        targetIP = IPAddress.Parse(Console.ReadLine());
                        flag = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                Console.WriteLine("Write down your text:");
                Console.WriteLine("-----------------------------------");
                string str = null;
                do
                {
                    str = Console.ReadLine();
                    if (str.Length > (MAX_BYTE_SIZE / 2 - 2))
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
                Console.WriteLine("Local IP is: " + localIP);
                Console.WriteLine("Waiting to receive the Msg...");
                Console.WriteLine("-----------------------------------");
                string msg = null;
                do
                {
                    msg = ReceiveText(localIP);
                    if (msg != null)
                        Console.WriteLine(msg);
                } while (msg != "exit");
            }
#if DEBUG
            else if (choice == "3")
            {
                //Loop Send Test
                IPAddress targetIP = null;
                string str = null;
                int count = 0;
                Console.WriteLine("Enter the other side's IP:");
                targetIP = IPAddress.Parse(Console.ReadLine());
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

        static string ReceiveText(IPAddress listenIP)
        {
            IPEndPoint listenIPE = new IPEndPoint(listenIP, PORT);
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint remoteIPE = sender;
            //UDP Mode
            string receiveStr = UDPReceive(listenIPE, ref remoteIPE);
            if (receiveStr != null)
            {
                //Send answer
                IPEndPoint answerIPE = (IPEndPoint)remoteIPE;
                answerIPE.Port = AS_PORT;
                int count = 3;
                while (count > 0)
                {
                    count--;
                    if (UDPSend(ANSWER_FLAG, answerIPE))
                    {
#if DEBUG
                        Console.WriteLine("<<<<<<<<send '{0}' to: {1}", ANSWER_FLAG, answerIPE);
#endif
                        break;
                    }
                }
            }
            return receiveStr;
        }

        static bool SendText(string str, IPAddress targetIP)
        {
            IPEndPoint targetIPE = new IPEndPoint(targetIP, PORT);
            IPEndPoint answerIPE = new IPEndPoint(localIP, AS_PORT);
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint remoteIPE = sender;
            //UDP Mode
            if (UDPSend(str, targetIPE))
            {
                //Receive answer
                string answerStr = UDPReceive(answerIPE, ref remoteIPE, false);
#if DEBUG
                Console.WriteLine(">>>>>>>>>get '{0}' from: {1}", answerStr, remoteIPE);
#endif
                if (answerStr == ANSWER_FLAG)
                    return true;
            }
            return false;
        }

        static bool UDPSend(string str, EndPoint targetIPE)
        {
            if (udpSender == null)
            {
                udpSender = new Socket(AddressFamily.InterNetwork,
                SocketType.Dgram, ProtocolType.Udp);
                udpSender.SendTimeout = 800;
            }

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

        static string UDPReceive(EndPoint listenIPE, ref EndPoint remoteIPE, bool blocked = true)
        {
            if (udpReceiver == null)
            {
                //Receive_Socket的首次创建导致错过Answer信号的接收-------提升Socket创建顺序
                //answerReceive网络缓冲区的旧数据导致收到“假Answer”------添加Answer校对
                udpReceiver = new Socket(AddressFamily.InterNetwork,
                SocketType.Dgram, ProtocolType.Udp);
                udpReceiver.Bind(listenIPE);
                if (!blocked)
                    udpReceiver.ReceiveTimeout = 2500;
            }
            
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

    }
}
