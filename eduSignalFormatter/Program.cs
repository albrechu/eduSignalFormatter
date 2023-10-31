

using System.Net;
using System.Net.Sockets;
using System.Text;

internal class Program
{

    private static void Discover(UdpClient udpClient, int port)
    {
        var discoverMessage = Encoding.ASCII.GetBytes("EDF-DISCOVER");
        string broadcast = IPAddress.Broadcast.ToString();
        
        udpClient.Send(discoverMessage, discoverMessage.Length, "192.168.2.255", port);

    }

    private static void Main(string[] args)
    {
        const int PORT  = 1212;
        const int TRIES = 5;

        const string hello = "==========================================================\n" +
                             "*                  EDF+ SIGNAL FORMATTER                 *\n" +
                             "==========================================================\n";
        System.Console.Write(hello);

        System.Console.WriteLine("[ESF:] Trying to establish connection to esp32...");
        UdpClient udpClient = new UdpClient();

        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, PORT));
        //udpClient.JoinMulticastGroup(multicastIPAddresse, group);
        udpClient.Client.ReceiveTimeout = 3000;

        IPAddress? esp32 = null;

        var from = new IPEndPoint(0, 0);
        var task = Task.Run(() =>
        {
            int @try = 0;
            while (true)
            {
                try
                {
                    var recvBuffer = udpClient.Receive(ref from);

                    var recvMsg = Encoding.ASCII.GetString(recvBuffer);
                    if (recvMsg != "EDF-DISCOVER") // Loops back
                    {
                        System.Console.WriteLine("[ESF:] Received response from device: {}.", recvMsg);
                    }


                    if (Encoding.UTF8.GetString(recvBuffer) == "OK")
                    {
                        esp32 = from.Address;
                    }
                    //var okMessage = Encoding.UTF8.GetBytes("OK");
                    //udpClient.Send(okMessage, okMessage.Length, from.Address.ToString(), from.Port);
                }
                catch (SocketException exception)
                {
                    if(@try == TRIES)
                    {
                        System.Console.WriteLine("[ESF:] Response timeout. Exits...");
                        break;
                    }
                    @try++;
                    System.Console.WriteLine("[ESF:] Response timeout. Trying again.");
                    Discover(udpClient, PORT);
                    
                }

                if(esp32 != null)
                {
                    System.Console.WriteLine("==================Connection established==================");
                    while (true)
                    {
                    }
                }
            }
        });
        
        System.Console.WriteLine("[ESF:] Discovering device.");
        Discover(udpClient, PORT);

        task.Wait();
    }
}