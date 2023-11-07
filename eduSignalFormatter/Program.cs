

using System.Net;
using System.Net.Sockets;
using System.Text;

class EDUConnection
{
    // Commands
    private string DISCOVER_CMD          = "EDF-DISCOVER";
    private string HEADER_CMD            = "EDF-HEADER";
    private string DATARECORDHEADERS_CMD = "EDF-RECORDHEADERS";
    private string RECORD_CMD            = "EDF-RECORD";
    // Connection Constants
    private const int PORT               = 1212;
    private const int TRIES              = 4;

    private UdpClient   _udpClient;
    private TcpClient   _tcpClient;
    private IPAddress?  _esp32;
    private FileStream? _fileStream;   

    internal EDUConnection()
    {
        _esp32      = null;
        _udpClient  = new UdpClient();
        _tcpClient  = new TcpClient();
        _fileStream = null;
    }

    ~EDUConnection()
    {
        _udpClient.Close();
        _tcpClient.Close();
        _fileStream?.Close();
    }


    internal void ConnectTCP()
    {
        if(_esp32 != null)
        {
            _tcpClient.Connect(_esp32, PORT);
        }
        else
        {
            Console.WriteLine("**Error** Something went wrong.");
        }
    }

    private void Discover()
    {
        var discoverMessage = Encoding.ASCII.GetBytes(DISCOVER_CMD);
        _udpClient.Send(discoverMessage, discoverMessage.Length, "192.168.2.255", PORT);
    }

    internal bool DiscoverDevice()
    {
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, PORT));
        _udpClient.Client.ReceiveTimeout = 3000;

        IPEndPoint source = new IPEndPoint(0, 0);
        for (var @try = 0; @try < TRIES; @try++)
        {
            try
            {
                Discover();
                var recvBuffer = _udpClient.Receive(ref source);

                var recvMsg = Encoding.ASCII.GetString(recvBuffer);
                if(recvMsg == "EDF-DISCOVER") // Loops back
                {
                    recvBuffer = _udpClient.Receive(ref source);
                }
                else 
                {
                    System.Console.WriteLine("[ESF:] Received response from device: {}.", recvMsg);
                }

                if (Encoding.UTF8.GetString(recvBuffer) == "OK")
                {
                    _esp32 = source.Address;
                    return true;
                }
                //var okMessage = Encoding.UTF8.GetBytes("OK");
                //udpClient.Send(okMessage, okMessage.Length, from.Address.ToString(), from.Port);
            }
            catch (SocketException)
            {
                if (@try == TRIES)
                {
                    System.Console.WriteLine("[ESF:] Response timeout. Exits...");
                    break;
                }
                @try++;
                System.Console.WriteLine("[ESF:] Response timeout. Trying again.");
            }
        }
        return false;
    }

    internal void DemandHeaders()
    {
        const int BDF_HEADER_SIZE = 256;

        byte[] header = new byte[_tcpClient.ReceiveBufferSize];
        const int NUMBER_OF_DATA_RECORDS_OFFSET = 8 + 2 * 80 + 3 * 8 + 44 + 8;

        // Demand header
        byte[] demandMsg = Encoding.ASCII.GetBytes(HEADER_CMD);
        NetworkStream stream = _tcpClient.GetStream();
        stream.Write(demandMsg);

        // Read general header

        stream.Read(header);
        string numberOfDataRecordsStr = Encoding.ASCII.GetString(header, NUMBER_OF_DATA_RECORDS_OFFSET, 8);
        int numberOfDataRecords = int.Parse(numberOfDataRecordsStr);
        if(numberOfDataRecords == -1)
        {
            // Unknown number of data records. Do something
        }

        // Read data record headers
        int dataRecordHeadersSize = BDF_HEADER_SIZE * numberOfDataRecords;
        demandMsg = Encoding.ASCII.GetBytes(DATARECORDHEADERS_CMD);
        byte[] dataRecordHeaders = new byte[dataRecordHeadersSize];
        int dataRecordHeaderOffset = 0;
        while(dataRecordHeaderOffset != dataRecordHeadersSize)
        {
            stream.Read(dataRecordHeaders, dataRecordHeaderOffset, dataRecordHeadersSize - dataRecordHeaderOffset);
        }

        // Create file
        Console.WriteLine("[BSF:] Received all bdf data record headers.");
        string? fileStr = null;
        bool fileCreatePromptDone = false;
        string defaultFile = "test.bdf";
        while (!fileCreatePromptDone)
        {
            Console.Write("[BSF:] In which file should the data be stored (default: {}): ", defaultFile);
            fileStr = Console.ReadLine();
            fileStr ??= defaultFile;
            if (File.Exists(fileStr))
            {
                Console.Write("[BSF:] The file '{}' already exists. Do you want to override it?: (y/n/default=y)");
                var key = Console.ReadKey();
                if(key.Key == ConsoleKey.Y || key.Key == ConsoleKey.Enter)
                {
                    fileCreatePromptDone = true;
                }
            }
        }

        _fileStream = new FileStream(fileStr, FileMode.Create, FileAccess.Write);
        _fileStream.Write(header, 0, header.Length);
        _fileStream.Write(dataRecordHeaders, header.Length, dataRecordHeaders.Length);
    }

    internal void ReadDataRecords()
    {
        if(_fileStream == null)
        {
            throw new InvalidOperationException("File was not opened. Demand headers first.");
        }

        NetworkStream stream = _tcpClient.GetStream();
        Task readRecordsTask = Task.Run(() =>
        {
            // TODO: Read stream
            
            lock (this)
            {

            }
        });

        // TODO: Write to file simultaneously
        _fileStream.Position = _fileStream.Length;

    }
}

internal class Program
{
    private static void Main(string[] args)         
    {
        const int PORT  = 1212;
        const int TRIES = 5;

        const string hello = "==========================================================\n" +
                             "*                   BDF SIGNAL FORMATTER                 *\n" +
                             "==========================================================\n";
        System.Console.Write(hello);

        System.Console.WriteLine("[BSF:] Trying to establish connection to esp32...");
        System.Console.WriteLine("[BSF:] Discovering device.");

        EDUConnection connection = new EDUConnection();
        if (!connection.DiscoverDevice()) return;
        connection.ConnectTCP();
        connection.DemandHeaders();
    }
}