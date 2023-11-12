public class ErrorAttribute : Attribute
{
    public ErrorAttribute(string error)
    {
        Error = error;
    }

    public string Error { get; }
}

