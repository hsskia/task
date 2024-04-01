using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

#nullable disable // C#8 Nullable Reference wanring(CS8618) 을 피하기 위해

namespace Mars
{
    partial class Int16Volume
    {
        private static async Task WriteULongAsync(Stream stream, ulong v)
        {
            var bytes = BitConverter.GetBytes(v);
            await stream.WriteAsync(bytes, 0, bytes.Length);
        }

        private static async Task WriteIntAsync(Stream stream, int v)
        {
            var bytes = BitConverter.GetBytes(v);
            await stream.WriteAsync(bytes, 0, bytes.Length);
        }

        private static async Task WriteStringAsync(Stream stream, string s, System.Text.Encoding encoding = null)
        {
            if (s == null) throw new ArgumentNullException(nameof(s)); // null 인 경우 지원하지 않음
            if (encoding == null) encoding = System.Text.Encoding.UTF8;

            var bytes = encoding.GetBytes(s);
            var length = bytes.Length;
            await WriteIntAsync(stream, length);
            await WriteArrayAsync(stream, bytes);
        }

        private static async Task WriteDictionaryAsync(Stream stream,
            Dictionary<string, string> dic,
            System.Text.Encoding encoding = null)
        {
            if (encoding == null) encoding = System.Text.Encoding.UTF8;

            await WriteIntAsync(stream, dic?.Count ?? 0);
            if (dic == null) return;

            foreach (var pair in dic)
            {
                // dictionary count 를 기록했기 때문에 반드시 같은 수 의 key-value 가 포함되어야하므로
                // break 나 continue 를 내부에 두면 안된다.

                // dictionary key 는 null 일수 없음 ; https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2#remarks
                await WriteStringAsync(stream, pair.Key, encoding);
                // value 가 null 인경우 null 을 데이터에 포함할 수 없기 때문에 공백으로 대체
                await WriteStringAsync(stream, pair.Value ?? "", encoding);
            }
        }

        private static async Task WriteArrayAsync<T>(Stream stream, T[] value) where T : struct
        {
            var buffer = GetBytes(value);
            await stream.WriteAsync(buffer, 0, buffer.Length);
        }

        private static async Task<ulong> ReadULongAsync(Stream stream, bool peek = false)
        {
            var beginPos = peek ? stream.Position : 0;
            var buffer = new byte[sizeof(ulong)];
            await stream.ReadAsync(buffer, 0, buffer.Length);
            var value = BitConverter.ToUInt64(buffer, 0);
            if (peek) stream.Position = beginPos;
            return value;
        }

        private static async Task<int> ReadIntAsync(Stream stream, bool peek = false)
        {
            var beginPos = peek ? stream.Position : 0;
            var buffer = new byte[sizeof(int)];
            await stream.ReadAsync(buffer, 0, buffer.Length);
            var value = BitConverter.ToInt32(buffer, 0);
            if (peek) stream.Position = beginPos;
            return value;
        }

        private static async Task<string> ReadStringAsync(Stream stream,
            bool peek = false,
            System.Text.Encoding encoding = null)
        {
            if (encoding == null) encoding = System.Text.Encoding.UTF8;
            var beginPos = peek ? stream.Position : 0;

            var length = await ReadIntAsync(stream);
            var bytes = await ReadArrayAsync<byte>(stream, length);

            if (peek) stream.Position = beginPos;

            return encoding.GetString(bytes);
        }

        private static async Task<Dictionary<string, string>> ReadDictionaryAsync(
            Stream stream,
            bool peek = false)
        {
            var beginPos = peek ? stream.Position : 0;
            var dic = new Dictionary<string, string>();
            var dicCnt = await ReadIntAsync(stream);
            for (int i = 0; i < dicCnt; i++)
            {
                var key = await ReadStringAsync(stream);
                var value = await ReadStringAsync(stream);
                dic[key] = value;
            }

            if (peek) stream.Position = beginPos;

            return dic;
        }

        private static async Task<T[]> ReadArrayAsync<T>(Stream stream,
            int arrayCount,
            bool peek = false) where T : struct
        {
            // Position 을 사용할 수 없는 stream 인경우 peek 할 수 없음(stream.Position 에서- exception)
            var beginPos = peek ? stream.Position : 0;

            var buffer = new byte[arrayCount * Marshal.SizeOf(default(T))];
            var bytesToRead = buffer.Length;
            while (bytesToRead > 0)
            {
                var readbytes = await stream.ReadAsync(buffer, buffer.Length - bytesToRead, bytesToRead);
                if (readbytes == 0)
                    break; // end of stream
                bytesToRead -= readbytes;
            }

            if (bytesToRead > 0) // ReadAsync 는 요청한 모든 데이터를 읽지 못할 수 있다.
                Array.Resize(ref buffer, buffer.Length - bytesToRead);

            if (peek) stream.Position = beginPos;

            return GetArray<T>(buffer);
        }

        private static byte[] GetBytes<T>(T[] src) where T : struct
        {
            var ret = new byte[src.Length * Marshal.SizeOf(default(T))]; // sizeof(T) 사용할 수 없어서(CS0233) 대체.
            Buffer.BlockCopy(src, 0, ret, 0, ret.Length);
            return ret;
        }

        private static T[] GetArray<T>(byte[] src) where T : struct
        {
            var ret = new T[src.Length / Marshal.SizeOf(default(T))];
            Buffer.BlockCopy(src, 0, ret, 0, src.Length);
            return ret;
        }
    }
}

#nullable restore
