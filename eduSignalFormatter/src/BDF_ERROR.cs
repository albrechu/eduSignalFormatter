namespace bdf
{
    public enum BDF_ERROR
    {
        [Error("No error")]
        NO_ERROR = 0,
        [Error("Could not find bdf device.")]
        NO_DEVICE,
        [Error("Did not receive a header. Possibly a timeout.")]
        UNRECEIVED_HEADER,
        [Error("File was not opened. Demand headers first.")]
        NO_FILE,
        [Error("A unknown number of signals is invalid.")]
        UNKNOWN_NUMBER_OF_SIGNALS,
        [Error("The given IP address is not valid.")]
        NO_VALID_IP,
    }
}
