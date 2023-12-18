internal class Program
{
    private static void Main(string[] args)
    {
        EDUConnection connection = new EDUConnection();
        const string hello = "==========================================================\n" +
                             "*                   BDF SIGNAL FORMATTER                 *\n" +
                             "==========================================================\n";
        Console.Write(hello);
        while (true)
        {

            Console.Write("[BSF:] Press enter to connect to device or Alt+Q to quit...");
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey();
                if (key.Modifiers == ConsoleModifiers.Alt && key.Key == ConsoleKey.Q)
                {
                    goto exit;
                }
            }
            while (!(key.Key == ConsoleKey.Enter));


            Console.Clear();

            //connection.BDFErrorHandling(connection.Connect(discoverIP));
            connection.BDFErrorHandling(connection.AcceptClient());
            connection.BDFErrorHandling(connection.DemandHeaders());
            connection.BDFErrorHandling(connection.ReadDataRecords());
        }

        exit:
            var _ = connection.CloseTCPConnection();
    }
}