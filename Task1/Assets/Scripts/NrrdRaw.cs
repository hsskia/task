using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Mars
{
    public class NrrdRaw
    {
        public NrrdHeader Header { get; private set; }

        #region DataBlock accessor

        // 실제 데이터는 byte[](raw)로 들고있지만, 여러 type 들의 데이터를 지원하기 위해
        //   Memory<T> / Span<T> 를 이용해 acess 하도록

        public Memory<sbyte> Int8Data { get; private set; } = Memory<sbyte>.Empty;
        public Memory<byte> UInt8Data { get; private set; } = Memory<byte>.Empty;

        public Memory<short> Int16Data { get; private set; } = Memory<short>.Empty;
        public Memory<ushort> UInt16Data { get; private set; } = Memory<ushort>.Empty;
        public Memory<int> Int32Data { get; private set; } = Memory<int>.Empty;
        public Memory<uint> UInt32Data { get; private set; } = Memory<uint>.Empty;

        #endregion DataBlock accessor

        // 압축되어있지않은 전체 내용 데이터(data block)
        protected byte[] raw;

        public byte[] GetBytes() => raw;

        public NrrdRaw(NrrdHeader header, byte[] rawData)
        {
            // 생성자에서는 비동기를 사용할 수 없으므로 filestream 등을 사용하는 생성자는 사용하지 않음

            Header = header;
            raw = rawData;

            switch (header.type.ByteSize())
            {
                case 1:
                    if (header.type.IsUnsigned())
                        UInt8Data = rawData;
                    else
                        Int8Data = Cast<byte, sbyte>(rawData);
                    break;

                case 2:
                    if (header.type.IsUnsigned())
                        UInt16Data = Cast<byte, ushort>(rawData);
                    else
                        Int16Data = Cast<byte, short>(rawData);
                    break;

                case 4:
                    if (header.type.IsUnsigned())
                        UInt32Data = Cast<byte, uint>(rawData);
                    else
                        Int32Data = Cast<byte, int>(rawData);
                    break;
                // TODO: 추가 type 지원, swith 안쓰는 방법??
                default:
                    throw new NotSupportedException($"not supported data type : {header.type}");
            }
        }

        public NrrdRaw(int[] sizes, NrrdHeader.DataType type) // creating empty volume
            : this(CreateSlicerHeader(sizes), new byte[type.TotalByteSize(sizes)])
        {
            Header.type = type;
        }

        public async Task SaveAsync(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Create);
            await SaveAsync(fs);
        }

        /// <summary>
        ///     nrrd 파일 저장
        /// </summary>
        /// <param name="stream">저장 대상</param>
        /// <param name="customDataBlockEncoder">datablock(<see cref="raw"/>)이 외부 encoding 을 필요로 하는 경우 설정</param>
        public async Task SaveAsync(Stream stream, Stream customDataBlockEncoder = null)
        {
            await Header.WriteAsync(stream);
            if (customDataBlockEncoder is null)
            {
                // saving data block by encoding description in header
                await WriteDataBlock(stream, Header.encoding, raw);
                return;
            }
            await customDataBlockEncoder.WriteAsync(raw, 0, raw.Length);
            await stream.FlushAsync();
        }

        public static async Task<NrrdRaw> LoadAsync(string filePath, bool strictVersion = false)
        {
            using var fs = new FileStream(filePath, FileMode.Open);
            return await LoadAsync(fs, strictVersion);
        }

        public static async Task<NrrdRaw> LoadAsync(Stream stream, bool strictVersion = false)
        {
            var header = await NrrdHeader.ReadAsync(stream, strictVersion);

            // read binary body
            var raw = await ReadAllDataBlock(stream, header.encoding);
            if (raw.Length != header.type.TotalByteSize(header.sizes))
            {
                throw new ApplicationException($"datablock load failed. {raw.Length} bytes read. should read {header.type.TotalByteSize(header.sizes)} bytes");
            }
            return new NrrdRaw(header, raw);
        }

        #region internal helpers

        private static NrrdHeader CreateSlicerHeader(int[] sizes)
        {
            // slicer 에서 설정되는 header 들을 그대로 적용
            //   type, space_direction 제외

            var dimSize = sizes.Length;
            return new NrrdHeader
            {
                sizes = sizes,
                type = NrrdHeader.DataType.@short,
                space = NrrdHeader.Space.left___posterior___superior,
                kinds = (new NrrdHeader.Kind[dimSize]).Select(x => NrrdHeader.Kind.domain).ToArray(),
                endian = NrrdHeader.Endian.little,
                encoding = NrrdHeader.Encoding.gzip,
                space_origin = new NrrdVector(dimSize),
            };
        }

        private static async Task<byte[]> ReadToEndAsync(Stream s)
        {
            // *주의 * Stream 종류에 따라, Position 이나 Seek 을 지원하지 않을 수 있어
            //   이들을 사용할 수 없음.
            using MemoryStream buffer = new MemoryStream();
            await s.CopyToAsync(buffer);
            return buffer.ToArray();
        }

        private static async Task<byte[]> ReadAllDataBlock(Stream s, NrrdHeader.Encoding encoding)
        {
            var streams = new Dictionary<NrrdHeader.Encoding, Stream> {
                { NrrdHeader.Encoding.raw, s },
                { NrrdHeader.Encoding.gzip, new System.IO.Compression.GZipStream(s, System.IO.Compression.CompressionMode.Decompress)  },
            };
            if (streams.ContainsKey(encoding) == false)
                throw new NotSupportedException($"not supported encoding : {encoding}");

            return await ReadToEndAsync(streams[encoding]);
        }

        private static async Task WriteDataBlock(Stream s, NrrdHeader.Encoding encoding, byte[] data)
        {
            var streams = new Dictionary<NrrdHeader.Encoding, Stream> {
                { NrrdHeader.Encoding.raw, s },
                { NrrdHeader.Encoding.gzip, new System.IO.Compression.GZipStream(s, System.IO.Compression.CompressionMode.Compress) },
            };
            if (streams.TryGetValue(encoding, out var stream) == false)
                throw new NotSupportedException($"not supported encoding : {encoding}");

            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync(); // 온전한 데이터가 파일에 쓰여지기 전에 파일을 사용하는 경우 문제 생기지 않도록
        }

        #region Memory<T> casting

        // https://stackoverflow.com/questions/54511330/how-can-i-cast-memoryt-to-another
        private static Memory<TTo> Cast<TFrom, TTo>(Memory<TFrom> from)
            where TFrom : unmanaged
            where TTo : unmanaged
        {
            // avoid the extra allocation/indirection, at the cost of a gen-0 box
            if (typeof(TFrom) == typeof(TTo)) return (Memory<TTo>)(object)from;

            return new CastMemoryManager<TFrom, TTo>(from).Memory;
        }

        private sealed class CastMemoryManager<TFrom, TTo>
            : MemoryManager<TTo>
            where TFrom : unmanaged
            where TTo : unmanaged
        {
            private readonly Memory<TFrom> _from;

            public CastMemoryManager(Memory<TFrom> from) => _from = from;

            public override Span<TTo> GetSpan()
                => MemoryMarshal.Cast<TFrom, TTo>(_from.Span);

            protected override void Dispose(bool disposing)
            { }

            public override MemoryHandle Pin(int elementIndex = 0)
                => throw new NotSupportedException();

            public override void Unpin()
                => throw new NotSupportedException();
        }

        #endregion Memory<T> casting

        #region heightmap

        // TODO: 다른 Direction에 대해서도 구현. (현재는 Anterior만 구현함)

        // Mars Processor의 Volume의 Data 자료형과,
        // NrrdRaw의 Int16Data(Memory<short>, protected raw라는 byte array 변수로 구성)와 호환되지 않는 문제가 있어,
        // heightmap관련 함수들을 모두 short 배열을 입력받는 static 함수로 작성하였음.

        /// <summary>
        ///     출력 결과물이 보여지는(향하는) 방향
        /// </summary>
        public enum HeightDirection
        {
            ANTERIOR,
            POSTERIOR,

            LEFT,
            RIGHT,

            SUPERIOR,
            INFERIOR,
        }

        /// <summary>
        ///     지정한 값 이상의 위치를 heightmap 으로 구성. 이미지로 출력하려면 <see cref="Export16bitGrayPng(int[,], float?, int)"/> 사용
        /// </summary>
        /// <param name="thresholdValue">volume 내 이 값 이상인 voxel 의 높이를 지정</param>
        /// <param name="dir">출력 결과물이 향할 방향</param>
        /// <returns>voxel 단위의 height map. 높이가 발견되지 않은 점은 -1 값을 가짐</returns>
        public static int[/*y*/, /*x*/] CreateHeightMap(NrrdHeader header, Span<short> data,
            short thresholdValue = 1, HeightDirection dir = HeightDirection.ANTERIOR)
        {
            // axial volume 기준

            const int NAN_VALUE = -1;

            // TODO: swtich 및 각 방향별 별도 함수 사용하지 않고 genric한 방식으로.
            switch (dir)
            {
                case HeightDirection.ANTERIOR:
                    return CreateHeightMapAnterior(header, data, thresholdValue, NAN_VALUE);

                case HeightDirection.POSTERIOR:
                case HeightDirection.SUPERIOR:
                case HeightDirection.INFERIOR:
                case HeightDirection.LEFT:
                case HeightDirection.RIGHT:
                default:
                    throw new NotImplementedException($"unsupported direction : {dir}");
            }
        }

        public static (float x, float y, float z) TransformByDirection(HeightDirection dir, (float x, float y, float z) origin)
        {
            switch (dir)
            {
                case HeightDirection.SUPERIOR:
                    return (-origin.x, origin.y, -origin.z);

                case HeightDirection.INFERIOR:
                    return (origin.x, origin.y, origin.z);

                case HeightDirection.ANTERIOR:
                    return (origin.x, -origin.z, origin.y);

                case HeightDirection.POSTERIOR:
                    return (-origin.x, -origin.z, -origin.y);

                case HeightDirection.LEFT:
                    return (origin.y, -origin.z, -origin.x);

                case HeightDirection.RIGHT:
                    return (-origin.y, -origin.z, origin.x);
            }
            throw new ArgumentException($"unknown direction : {(int)dir}", nameof(dir));
        }

        public static int GetIndex(NrrdHeader header, int posZ, int posY, int posX)
        {
            return posX + posY * header.sizes[0] + posZ * header.sizes[0] * header.sizes[1];
        }

        public static short GetValue(NrrdHeader header, Span<short> data,
            int posZ, int posY, int posX)
        {
            return data[GetIndex(header, posZ, posY, posX)];
        }

        #region 각 방향별 CreateHeightMap

        private static int[,] CreateHeightMapAnterior(NrrdHeader header, Span<short> data,
            short threshold, int nanValue)
        {
            var heights = new int[header.sizes[2], header.sizes[0]];

            for (var x = 0; x < header.sizes[0]; ++x)
            {
                for (var z = 0; z < header.sizes[2]; ++z)
                {
                    var height = nanValue;
                    for (var y = 0; y < header.sizes[1]; ++y)
                    {
                        if (GetValue(header, data, (header.sizes[2] - 1) - z, y, x) >= threshold)
                        {
                            height = header.sizes[1] - 1 - y;
                            break;
                        }
                    }
                    heights[z, x] = height;
                }
            }
            return heights;
        }

        #endregion 각 방향별 CreateHeightMap

        #endregion heightmap

        #endregion internal helpers
    }
}