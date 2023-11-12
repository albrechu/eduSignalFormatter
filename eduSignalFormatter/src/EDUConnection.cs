using System.Net;
using System.Net.Sockets;
using System.Text;

using bdf;



internal class EDUConnection
{
    /// <summary>
    /// Types
    /// </summary>
    private class BDFHeaderInfo
    {
        public int   NumberOfDataRecords { get; set; }
        public float DurationOfADataRecord { get; set; }
        public int[] NumberOfSamplesInEachDataRecord {  get; set; }
    }

    /// <summary>
    /// Fields
    /// </summary>
    // Connection Constants
    private const int PORT  = 1212;
    private const int TRIES = 5;
    // Network 
    private UdpClient      _udpClient;
    private TcpClient      _tcpClient;
    private IPAddress?     _esp32;
    // File
    private FileStream?    _fileStream;
    private BDFHeaderInfo? _bdfHeaderInfo;


    /// <summary>
    /// Constructors/Destructors
    /// </summary>
    internal EDUConnection()
    {
        _udpClient     = new UdpClient();
        _tcpClient     = new TcpClient();
        _esp32         = null;
        _fileStream    = null;
        _bdfHeaderInfo = null;
    }

    ~EDUConnection()
    {
        _udpClient.Close();
        _tcpClient.Close();
        _fileStream?.Close();
    }

    /// <summary>
    /// Methods
    /// </summary>
    internal BDF_ERROR ConnectTCP()
    {
        if (_esp32 != null)
        {
            _tcpClient.Connect(_esp32, PORT);
            return BDF_ERROR.NO_ERROR;
        }
        return BDF_ERROR.NO_DEVICE;
    }

    private void Discover()
    {
        _udpClient.Send(BDF_COMMANDS.DISCOVER.ASCII(), BDF_COMMANDS.DISCOVER.ASCII().Length, "192.168.2.255", PORT);
    }

    internal BDF_ERROR DiscoverDevice()
    {
        Console.WriteLine("[BDF:] Trying to establish connection to esp32...");
        Console.WriteLine("[BDF:] Discovering device.");

        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, PORT));
        _udpClient.Client.ReceiveTimeout = 3000;

        IPEndPoint source = new IPEndPoint(0, 0);
        for (var @try = 0; @try < TRIES; @try++)
        {
            try
            {
                Discover();
                var recvBuffer = _udpClient.Receive(ref source);

                if (recvBuffer.SequenceEqual(BDF_COMMANDS.DISCOVER.ASCII())) // Loops back
                {
                    recvBuffer = _udpClient.Receive(ref source);
                }
                
                if (recvBuffer.SequenceEqual(BDF_COMMANDS.OK.ASCII()))
                {
                    _esp32 = source.Address;
                    return BDF_ERROR.NO_ERROR;
                }
                else
                {
                    Console.WriteLine("[BDF:] Received unexpected message \"" + Encoding.ASCII.GetString(recvBuffer) + "\".");
                }
            }
            catch(SocketException)
            {
                // Response timeout.
            }

            if (@try == TRIES - 1)
            {
                Console.WriteLine("[BDF:] Response timeout. Exits...");
                break;
            }
            Console.WriteLine("[BDF:] Response timeout. Trying again.");
        }
        return BDF_ERROR.NO_DEVICE;
    }

    internal BDF_ERROR DemandHeaders()
    {
        const int BDF_HEADER_SIZE = 256;
        int read_bytes;

        byte[] header = new byte[_tcpClient.ReceiveBufferSize];

        // Demand header
        NetworkStream stream = _tcpClient.GetStream();
        stream.Write(BDF_COMMANDS.REQ_HEADER.ASCII());

        // Read general header
        const int NUMBER_OF_DATA_RECORDS_OFFSET = 8 + 2 * 80 + 3 * 8 + 44 + 8;
        const int DURATION_OF_A_DATA_RECORD_OFFSET = NUMBER_OF_DATA_RECORDS_OFFSET + 8;
        read_bytes = stream.Read(header);
        if (read_bytes <= 0) // Error Handling
        {
            Console.WriteLine("[BDF:] Error unable to read bdf header.");
            return BDF_ERROR.UNRECEIVED_HEADER;
        }
        string numberOfDataRecordsStr = Encoding.ASCII.GetString(header, NUMBER_OF_DATA_RECORDS_OFFSET, 8);
        int numberOfDataRecords = int.Parse(numberOfDataRecordsStr);
        if (numberOfDataRecords == -1)
        {
            // @POTENTIALTODO: Unknown number of data records. Do something
            return BDF_ERROR.UNKNOWN_NUMBER_OF_DATA_RECORDS_UNSUPPORTED;
        }
        string durationOfADataRecordStr = Encoding.ASCII.GetString(header, DURATION_OF_A_DATA_RECORD_OFFSET, 8);
        float durationOfADataRecord = float.Parse(durationOfADataRecordStr);

        // Read data record headers
        int dataRecordHeadersSize = BDF_HEADER_SIZE * numberOfDataRecords;
        byte[] dataRecordHeaders = new byte[dataRecordHeadersSize];
        int dataRecordHeaderOffset = 0;
        stream.Write(BDF_COMMANDS.REQ_RECORD_HEADERS.ASCII()); // Demand record headers
        while (dataRecordHeaderOffset != dataRecordHeadersSize)
        {
            stream.Read(dataRecordHeaders, dataRecordHeaderOffset, dataRecordHeadersSize - dataRecordHeaderOffset);
        }

        // Read out the size of each data record
        const int NUMBER_OF_SAMPLES_IN_DATA_RECORD_OFFSET = 16 + 80 + 8 + 8 + 8 + 8 + 8 + 80;
        int numberOfSamples = 0;
        int numberOfSamplesIndex = NUMBER_OF_SAMPLES_IN_DATA_RECORD_OFFSET;
        _bdfHeaderInfo = new BDFHeaderInfo
        {
            NumberOfDataRecords             = numberOfDataRecords,
            DurationOfADataRecord           = durationOfADataRecord,
            NumberOfSamplesInEachDataRecord = new int[numberOfSamples],
        };
        for ( int i = 0; i < numberOfDataRecords; i++, numberOfSamplesIndex += BDF_HEADER_SIZE)
        {
            string numberOfSamplesStr =  Encoding.ASCII.GetString(dataRecordHeaders, numberOfSamplesIndex, 8);
            _bdfHeaderInfo.NumberOfSamplesInEachDataRecord[i] = int.Parse(numberOfSamplesStr);
        }

        // Create file
        Console.WriteLine("[BDF:] Received all bdf data record headers.");
        string? fileStr = null;
        bool fileCreatePromptDone = false;
        string defaultFile = "test.bdf";
        while (!fileCreatePromptDone)
        {
            Console.Write("[BDF:] In which file should the data be stored (default: {}): ", defaultFile);
            fileStr = Console.ReadLine();
            fileStr ??= defaultFile;
            if (File.Exists(fileStr))
            {
                Console.Write("[BDF:] The file '{}' already exists. Do you want to override it?: (y/n/default=y)");
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.Y || key.Key == ConsoleKey.Enter)
                {
                    fileCreatePromptDone = true;
                }
            }
        }

        _fileStream = new FileStream(fileStr, FileMode.Create, FileAccess.Write);
        _fileStream.Write(header, 0, header.Length);
        _fileStream.Write(dataRecordHeaders, header.Length, dataRecordHeaders.Length);

        

        return BDF_ERROR.NO_ERROR;
    }

    internal void BDFErrorHandling(BDF_ERROR error)
    {
        Console.WriteLine("Stage finished with \"" + error.Error() + "\"");

        if(error != BDF_ERROR.NO_ERROR)
        {
            Environment.Exit(0);
        }
    }

    internal BDF_ERROR ReadDataRecords()
    {
        if (_fileStream == null)
        {
            return BDF_ERROR.NO_FILE;
        }
        
        double time = 0.5f;
        for (bool invalidTime = true; invalidTime;)
        {
            Console.WriteLine("[BDF:] How many seconds should the measurement be?: ");
            string timeStr = Console.ReadLine();
            try
            {
                time = double.Parse(timeStr);
                invalidTime = false;
            }
            catch (FormatException)
            {
                Console.WriteLine("[BDF:] Wrong format");
            }
            catch (OverflowException)
            {
                Console.WriteLine("[BDF:] Value too big.");
            }
            catch (ArgumentNullException)
            {

            }
        }
        // Calculate the closest time and buffer size
        time           = Math.Ceiling(time / _bdfHeaderInfo.DurationOfADataRecord) * _bdfHeaderInfo.DurationOfADataRecord;
        int bufferSize = (int)Math.Round(_bdfHeaderInfo.NumberOfSamplesInEachDataRecord.Sum() * time);

        NetworkStream netStream = _tcpClient.GetStream();

        // Create cmd and send package
        byte[] timeBytes         = Encoding.ASCII.GetBytes(time.ToString("0.0000"));
        byte[] cmd               = BDF_COMMANDS.REQ_RECORDS.ASCII();
        byte[] startCommand      = new byte[timeBytes.Length + 1 + BDF_COMMANDS.REQ_RECORDS.ASCII().Length];
        Buffer.BlockCopy(cmd, 0, startCommand, 0, cmd.Length);
        startCommand[cmd.Length] = Convert.ToByte(' ');
        Buffer.BlockCopy(timeBytes, 0, startCommand, cmd.Length + 1, timeBytes.Length);
        netStream.Write(startCommand);

        // TODO: Indefinite Measurement
        bool endConditionMet = false;
        Task readRecordsTask = Task.Run(() =>
        {
            byte[] buffer = new byte[bufferSize];
            int bytesWritten = 0;
            while (bytesWritten != bufferSize)
            {
                int bytesRead = netStream.Read(buffer, 0, buffer.Length);
                _fileStream.Write(buffer, 0, bytesRead);
                bytesWritten += bytesRead;
            }
        });

        
        readRecordsTask.Wait();

        return BDF_ERROR.NO_ERROR;
    }
}
