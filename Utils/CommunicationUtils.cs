using System.IO;
using System.Text;

/// <summary>
/// Static class with additional methods used in communication.
/// </summary>
public static class CommunicationUtils
{
    /// <summary>
    /// Converts memory stream into string.
    /// </summary>
    /// <returns>The string.</returns>
    /// <param name="ms">Memory Stream.</param>
    /// <param name="encoding">Encoding.</param>
    public static string StreamToString(MemoryStream ms, Encoding encoding)
    {
        string readString = "";
        if (encoding == Encoding.UTF8)
        {
            using (var reader = new StreamReader(ms, encoding))
            {
                readString = reader.ReadToEnd();
            }
        }
        return readString;
    }
}
