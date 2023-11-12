using System.Reflection;

namespace bdf
{

    public static class StringExtensions
    {
        public static T GetAttribute<T>(this Enum @enum) where T : Attribute
        {
            Type type = @enum.GetType();
            FieldInfo? field = type.GetField(@enum.ToString());

            if(field == null)
            {
                throw new Exception(@enum.ToString() + " of type " + type.ToString() + " does not have a attribute of type " + typeof(T).ToString());
            }

    #pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            Attribute attribute = Attribute.GetCustomAttribute(field, typeof(T), false);
    #pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
    #pragma warning disable CS8603 // Possible null reference return.
            return attribute as T;
    #pragma warning restore CS8603 // Possible null reference return.
        }

        public static byte[] ASCII(this BDF_COMMANDS cmd) => cmd.GetAttribute<ASCIIAttribute>().ASCII;
        public static string Error(this BDF_ERROR cmd) => cmd.GetAttribute<ErrorAttribute>().Error;
    }
}

