using System.Diagnostics;
using System.Globalization;
using System.IO;
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
        public double DurationOfADataRecord { get; set; }
        public int[] NumberOfSamplesInEachDataRecord {  get; set; }
    }

    /// <summary>
    /// Fields
    /// </summary>
    // Connection Constants
    private const int PORT  = 1212;
    private const int TRIES = 5;
    // Network 
    private TcpListener _tcpListener;
    private TcpClient? _tcpClient;
    // File
    private FileStream?    _fileStream;
    private BDFHeaderInfo? _bdfHeaderInfo;


    /// <summary>
    /// Constructors/Destructors
    /// </summary>
    internal EDUConnection()
    {
        _tcpListener   = new TcpListener(IPAddress.Any, PORT);
        _tcpListener.Start();
        _fileStream    = null;
        _bdfHeaderInfo = null;
        _tcpClient     = null;
    }

    ~EDUConnection()
    {
        _tcpListener.Stop();
        _tcpClient?.Dispose();
        _fileStream?.Close();
    }

    /// <summary>
    /// Methods
    /// </summary>
    internal BDF_ERROR AcceptClient()
    {
        Console.WriteLine("[BDF:] Listening to port...");
        while (_tcpClient == null)
        {
            try
            {
                _tcpClient = _tcpListener.AcceptTcpClient();
            }
            catch(Exception) 
            {
            }
        }
        return BDF_ERROR.NO_ERROR;
    }

    internal BDF_ERROR CloseTCPConnection()
    {
        _tcpClient?.Close();
        return BDF_ERROR.NO_ERROR;
    }

    internal BDF_ERROR DiscoverDevice(string address)
    {
        Console.WriteLine($"[BDF:] Trying to establish connection to esp32 over address \'{address}\'...");
        Console.WriteLine("[BDF:] Discovering device.");
        
        UdpClient _udpClient = new UdpClient();
        //_udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, PORT));
        //_udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
        //_udpClient.EnableBroadcast = true;
        //_udpClient.Client.EnableBroadcast = true;
        _udpClient.Client.ReceiveTimeout = 3000;

        IPEndPoint source = new IPEndPoint(0, 0);
        BDF_ERROR @return = BDF_ERROR.NO_DEVICE;
        for (var @try = 0; @try < TRIES; @try++)
        {
            try
            {
                _udpClient.Send(BDF_COMMANDS.DISCOVER.ASCII(), BDF_COMMANDS.DISCOVER.ASCII().Length, address, PORT);
                var recvBuffer = _udpClient.Receive(ref source);

                if (recvBuffer.SequenceEqual(BDF_COMMANDS.DISCOVER.ASCII())) // Loops back
                {
                    recvBuffer = _udpClient.Receive(ref source);
                }
                
                if (recvBuffer.SequenceEqual(BDF_COMMANDS.ACKNOWLEDGE.ASCII()))
                {
                    //_esp32 = source;
                    @return = BDF_ERROR.NO_ERROR;
                    break;
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
        _udpClient.Dispose();
        return @return;
    }

    internal BDF_ERROR DemandHeaders()
    {
        const int BDF_HEADER_SIZE = 256;
        // Offsets in bdf header
        const int NUMBER_OF_DATA_RECORDS_OFFSET = 8 + 80 + 80 + 8 + 8 + 8 + 44;
        const int DURATION_OF_A_DATA_RECORD_OFFSET = NUMBER_OF_DATA_RECORDS_OFFSET + 8;
        const int NUMBER_OF_SIGNALS_OFFSET = DURATION_OF_A_DATA_RECORD_OFFSET + 8;

        int read_bytes;

        byte[] header = new byte[BDF_HEADER_SIZE];

        // Demand header
        NetworkStream stream = _tcpClient.GetStream();
        stream.Write(BDF_COMMANDS.REQ_HEADER.ASCII());

        // Read general header
        read_bytes = stream.Read(header, 0, BDF_HEADER_SIZE);
        if (read_bytes <= 0) // Error Handling
        {
            Console.WriteLine("[BDF:] Error unable to read bdf header.");
            return BDF_ERROR.UNRECEIVED_HEADER;
        }
        string headerStr = Encoding.ASCII.GetString(header);
        Console.WriteLine($"[BDF:] Received header:\n{headerStr}");
        string numberOfSignalsStr = headerStr.Substring(NUMBER_OF_SIGNALS_OFFSET, 4);
        int numberOfSignals = int.Parse(numberOfSignalsStr);
        if (numberOfSignals == -1)
        {
            return BDF_ERROR.UNKNOWN_NUMBER_OF_SIGNALS;
        }
        string durationOfADataRecordStr = Encoding.ASCII.GetString(header, DURATION_OF_A_DATA_RECORD_OFFSET, 8);
        double durationOfADataRecord = double.Parse(durationOfADataRecordStr, CultureInfo.InvariantCulture);

        // Read signal headers
        int SIGNAL_HEADERS_SIZE = BDF_HEADER_SIZE * numberOfSignals;
        byte[] signalHeaders = new byte[SIGNAL_HEADERS_SIZE];
        stream.Write(BDF_COMMANDS.REQ_RECORD_HEADERS.ASCII()); // Demand record headers
        for(int read = 0; read < signalHeaders.Length;)
        {
            int readFromStream = stream.Read(signalHeaders, read, SIGNAL_HEADERS_SIZE - read);
            read += readFromStream;
        }

        string signalHeadersStr = Encoding.ASCII.GetString(signalHeaders);
        Console.WriteLine($"[BDF:] Received signal headers:\n{signalHeadersStr}");

        // Read out the size of each channel
        int NUMBER_OF_SAMPLES_IN_DATA_RECORD_OFFSET = numberOfSignals * (16 + 80 + 8 + 8 + 8 + 8 + 8 + 80);
        _bdfHeaderInfo = new BDFHeaderInfo
        {
            DurationOfADataRecord           = durationOfADataRecord,
            NumberOfSamplesInEachDataRecord = new int[numberOfSignals],
        };
        for (int dataRecord = 0, numberOfSamplesIndex = NUMBER_OF_SAMPLES_IN_DATA_RECORD_OFFSET; dataRecord < numberOfSignals; dataRecord++, numberOfSamplesIndex += 8)
        {
            string numberOfSamplesStr = signalHeadersStr.Substring(numberOfSamplesIndex, 8);
            _bdfHeaderInfo.NumberOfSamplesInEachDataRecord[dataRecord] = int.Parse(numberOfSamplesStr);
        }

        double time = PromptRecordDuration();
        int numberOfDataRecords = (int)Math.Round(time / durationOfADataRecord);
        string numberOfDataRecordsStr = numberOfDataRecords.ToString();
        byte[] numberOfDataRecordsBytes = new byte[8];
        Array.Fill(numberOfDataRecordsBytes, (byte)0x20);
        int _ = Encoding.ASCII.GetBytes(numberOfDataRecordsStr, numberOfDataRecordsBytes);
        Array.Copy(numberOfDataRecordsBytes, 0, header, NUMBER_OF_DATA_RECORDS_OFFSET, 8);
        _bdfHeaderInfo.NumberOfDataRecords = numberOfDataRecords;

        CreateFile(header, signalHeaders);

        Console.WriteLine("[BDF:] Press any key to start transmission.");
        Console.ReadKey();
        return BDF_ERROR.NO_ERROR;
    }

    private double PromptRecordDuration()
    {
        double time = 0.5f;
        for (bool invalidTime = true; invalidTime;)
        {
            Console.WriteLine("[BDF:] How many seconds should the measurement be?: ");
            string timeStr = Console.ReadLine();
            try
            {
                time = double.Parse(timeStr, CultureInfo.InvariantCulture);
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
        return Math.Ceiling(time / _bdfHeaderInfo.DurationOfADataRecord) * _bdfHeaderInfo.DurationOfADataRecord;
    }

    private void CreateFile(byte[] header, byte[] dataRecordHeaders)
    {
        // Create file
        Console.WriteLine("[BDF:] Received all bdf data record headers.");
        string fileStr = string.Empty;
        bool fileCreatePromptDone = false;
        string defaultFile = "test.bdf";
        while (!fileCreatePromptDone)
        {
            Console.Write($"[BDF:] In which file should the data be stored (default: \'{defaultFile}\'): ");
            fileStr = Console.ReadLine();
            if (fileStr == string.Empty)
            {
                fileStr = defaultFile;
            }

            if (File.Exists(fileStr))
            {
                Console.Write($"[BDF:] The file '{fileStr}' already exists. Do you want to override it?: (y/n/default=y)");
                ConsoleKeyInfo key;
                do
                {
                    key = Console.ReadKey(true);
                    Console.ReadLine();
                }
                while (!(key.Key == ConsoleKey.Y || key.Key == ConsoleKey.N || key.Key == ConsoleKey.Enter));

                if (key.Key == ConsoleKey.Y || key.Key == ConsoleKey.Enter)
                {
                    bool fileDeleted = false;
                    fileCreatePromptDone = true;
                    do
                    {
                        try
                        {
                            File.Delete(fileStr);
                            fileDeleted = true;
                        } 
                        catch (Exception)
                        {
                            Console.Write($"[BDF:] Unable to delete {fileStr}. Make it is not used. Press any key to try again...");
                            key = Console.ReadKey();
                        }
                    } while (!fileDeleted);
                }
            }
            else
            {
                fileCreatePromptDone = true;
            }
        }
        Console.WriteLine($"[BDF:] Writing to \'{fileStr}\'");
        _fileStream = new FileStream(fileStr, FileMode.Create, FileAccess.Write);
        _fileStream.Write(header, 0, header.Length);
        Console.WriteLine($"[BDF:] Written general header of length {header.Length}.");
        _fileStream.Write(dataRecordHeaders, 0, dataRecordHeaders.Length);
        Console.WriteLine($"[BDF:] Written record headers of length {dataRecordHeaders.Length}.");
        _fileStream.Flush(); // Flush to file.
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
        if (_fileStream == null || _bdfHeaderInfo == null)
        {
            return BDF_ERROR.NO_FILE;
        }
        
        const int NODE_SIZE = 3; // 24-Bit
        int bufferSize = NODE_SIZE * _bdfHeaderInfo.NumberOfSamplesInEachDataRecord.Sum();

        NetworkStream netStream = _tcpClient.GetStream();

        // Create cmd and send package
        byte[] timeBytes         = Encoding.ASCII.GetBytes(_bdfHeaderInfo.NumberOfDataRecords.ToString());
        byte[] cmd               = BDF_COMMANDS.REQ_RECORDS.ASCII();
        byte[] startCommand      = new byte[timeBytes.Length + 1 + BDF_COMMANDS.REQ_RECORDS.ASCII().Length];
        Buffer.BlockCopy(cmd, 0, startCommand, 0, cmd.Length);
        startCommand[cmd.Length] = Convert.ToByte(' ');
        Buffer.BlockCopy(timeBytes, 0, startCommand, cmd.Length + 1, timeBytes.Length);
        netStream.Write(startCommand);

        Stopwatch stopwatch = Stopwatch.StartNew();
        Task readRecordsTask = Task.Run(() =>
        {
            byte[] buffer            = new byte[bufferSize];
            int bytesWrittenToBuffer = 0;
            int bytesWritten         = 0;
            int bytesToWrite         = buffer.Length * _bdfHeaderInfo.NumberOfDataRecords;
            while (bytesWritten != bytesToWrite)
            {
                int bytesRead = netStream.Read(buffer, bytesWrittenToBuffer, buffer.Length - bytesWrittenToBuffer);
                if(bytesRead > 0)
                {
                    bytesWrittenToBuffer += bytesRead;
                    if (bytesWrittenToBuffer == buffer.Length)
                    {
                        _fileStream.Write(buffer, 0, buffer.Length);
                        bytesWritten += buffer.Length;
                        bytesWrittenToBuffer = 0;
                        Console.WriteLine($"Received and written {bytesWritten}/{bytesToWrite} Bytes to {_fileStream.Name}");
                    }
                }
            }
        });

        
        readRecordsTask.Wait();
        stopwatch.Stop();
        Console.WriteLine($"Time elapsed for transmission: {stopwatch.ElapsedMilliseconds} ms");
        _fileStream.Close();
        _fileStream = null;

        return BDF_ERROR.NO_ERROR;
    }
}
