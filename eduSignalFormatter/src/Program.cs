internal class Program
{
    private static void Main(string[] args)
    {
        while(true)
        {
            const string hello = "==========================================================\n" +
                                 "*                   BDF SIGNAL FORMATTER                 *\n" +
                                 "==========================================================\n";
            Console.Write(hello);

            Console.Write("[BSF:] Press any key to connect to device...");
            var _ = Console.Read();

            EDUConnection connection = new EDUConnection();
            connection.BDFErrorHandling(connection.DiscoverDevice());
            connection.BDFErrorHandling(connection.ConnectTCP());
            connection.BDFErrorHandling(connection.DemandHeaders());
            connection.BDFErrorHandling(connection.ReadDataRecords());

            Console.Clear();
        }
    }
}