using System.IO;
using System.IO.Compression;

namespace Mars
{
    public class Deflate
    {
        public static byte[] Compress(byte[] data)
        {
            byte[] outData = new byte[0];

            using (var outStream = new MemoryStream())
            {
                using (var compressor = new DeflateStream(outStream, CompressionMode.Compress, true))
                {
                    compressor.Write(data, 0, data.Length);
                    compressor.Flush();
                    compressor.Close();
                }
                outData = outStream.ToArray();
            }

            return outData;
        }

        public static byte[] Decompress(byte[] data)
        {
            byte[] outData = new byte[0];

            using (var outStream = new MemoryStream())
            {
                using (var compressStream = new MemoryStream(data))
                {
                    using (var compressor = new DeflateStream(compressStream, CompressionMode.Decompress))
                    {
                        compressor.CopyTo(outStream);
                    }
                }
                outData = outStream.ToArray();
            }

            return outData;
        }
    }
}