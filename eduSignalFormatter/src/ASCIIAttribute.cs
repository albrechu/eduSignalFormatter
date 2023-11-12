using System.Text;

public class ASCIIAttribute : Attribute
{
    public ASCIIAttribute(string cmdStr)
    {
        ASCII = Encoding.ASCII.GetBytes(cmdStr);
    }
    
    public byte[] ASCII { get; }
}

