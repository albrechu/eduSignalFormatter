
namespace bdf
{
    public enum BDF_COMMANDS
    {
        [ASCII("BDF_DISCOVER")]
        DISCOVER,
        [ASCII("OK")]
        OK,
        [ASCII("BDF_REQ_HEADER")]
        REQ_HEADER,
        [ASCII("BDF_REQ_RECORD_HEADERS")]
        REQ_RECORD_HEADERS, // Start sending the record headers of the 
        [ASCII("BDF_REQ_RECORDS")] 
        REQ_RECORDS, // Start sending data records with measurement time in seconds. E.g. 'BDF_SEND_RECORDS 0.005'.
    }
}
