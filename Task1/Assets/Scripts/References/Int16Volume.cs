// no external dependency

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

#nullable enable
namespace Mars
{
    public partial class Int16Volume
    { // https://bitbucket.org/alkee_skia/mars3/issues/506/mars-processor-volume#comment-60842558
        public enum Version : ulong
        {
            V1_1 = 0x4999_0001,
            V1_2 = 0x4999_0002,
            V1_3 = 0x4999_0003
        }

        public (float x, float y, float z) Resolution { get; set; } // m scale. (mm * 0.001f)
        public (int x, int y, int z) Dimension { get; set; }
        public short[] Data { get; set; } = Array.Empty<short>();
        public (short min, short max) DataRange { get; set; }
        public Dictionary<string, string> Meta { get; set; } = new Dictionary<string, string>();

        /// <summary>
        ///     file 에 volume 데이터를 씀
        /// </summary>
        /// <param name="filePath">출력될 파일</param>
        /// <param name="compress">압축여부</param>
        /// <returns>쓰여진 데이터 bytes</returns>
        public async Task<long> SaveAsync(string filePath, bool compress)
        {
            using (var file = new FileStream(filePath, FileMode.Create))
            {
                return await SaveAsync(file, compress);
            }
        }

        /// <summary>
        ///     stream 에 volume 데이터를 씀
        /// </summary>
        /// <param name="stream">출력 stream</param>
        /// <param name="compress">압축여부</param>
        /// <returns>쓰여진 데이터 bytes</returns>
        public async Task<long> SaveAsync(Stream stream, bool compress)
        {
            if (compress == false) return await SaveAsync_v1_1(stream, compress); // 압축안한다면 이전 방식 그대로

            // save 는 항상 최신의 방식으로
            return await SaveAsync_v1_3(stream);
        }

        /// <summary>
        ///     file 에서 Volume 을 읽어옴
        /// </summary>
        /// <param name="filePath">읽을 파일 경로</param>
        /// <exception cref="ApplicationException">데이터 형식이 일치하지 않는 경우</exception>
        /// <exception cref="FileNotFoundException">파일을 찾을 수 없는 경우</exception>
        /// <returns>읽혀진 volume</returns>
        public static async Task<Int16Volume> LoadAsync(string filePath)
        {
            using (var file = new FileStream(filePath, FileMode.Open))
            {
                return await LoadAsync(file);
            }
        }

        /// <summary>
        ///     stream 에서 Volume 을 읽어옴
        /// </summary>
        /// <param name="stream">입력되는 stream</param>
        /// <exception cref="ApplicationException">데이터 형식이 일치하지 않는 경우</exception>
        /// <returns>읽혀진 volume</returns>
        public static async Task<Int16Volume> LoadAsync(Stream stream)
        {
            var oldGzip = await IsGzipStream(stream);
            if (oldGzip) return await LoadAsync_v1_1(stream, oldGzip);
            var versionIdentifier = await ReadULongAsync(stream, true);
            switch((Version)versionIdentifier)
            {
                case Version.V1_1: return await LoadAsync_v1_1(stream, false);
                case Version.V1_2: return await LoadAsync_v1_2(stream);
                case Version.V1_3: return await LoadAsync_v1_3(stream);
            }

            // invalid file
            throw new ApplicationException($"unknown file identifier : {versionIdentifier:X8}");
        }

        #region 각 버전별 load / save 함수들

        private async Task<long> SaveAsync_v1_3(Stream stream)
        {
            var version = (ulong) Version.V1_3;
            var beginPos = stream.Position;

            // WRITE Version Info
            await WriteULongAsync(stream, version); // begin sign

            // WRITE Volume Information
            await WriteArrayAsync(stream, new float[] { Resolution.x, Resolution.y, Resolution.z });
            await WriteArrayAsync(stream, new int[] { Dimension.x, Dimension.y, Dimension.z });
            await WriteArrayAsync(stream, new short[] { DataRange.min, DataRange.max });

            // WRITE META
            await WriteDictionaryAsync(stream, Meta);

            // TODD: Meta Data와 구분되는,
            // 뒤쪽 Data의 정보만 담는 DATA HEADER (압축방식 + 원본 크기 + 압축된 크기...)를 작성

            // Write Header(예정) + Data
            var data = Data is null 
                ? Array.Empty<byte>()
                : GetBytes(Data);
            var compressedData = data.Length == 0
                ? Array.Empty<byte>()
                : Deflate.Compress(data);
            
            await WriteArrayAsync(stream, new int[] { data.Length, compressedData.Length });

            // compressed data
            await stream.WriteAsync(compressedData, 0, compressedData.Length);

            // tail information
            await WriteULongAsync(stream, version); // end sign
            await stream.FlushAsync(); // flush 하지않고 stream 이 close 되는 경우 마지막 block 이 실제로 쓰여지지 않을 수 있다.
            return stream.Position - beginPos;
        }

        private async Task<long> SaveAsync_v1_2(Stream stream)
        {
            var version = (ulong) Version.V1_2;

            var beginPos = stream.Position;
            await WriteULongAsync(stream, version); // begin sign

            // volume information
            await WriteArrayAsync(stream, new float[] { Resolution.x, Resolution.y, Resolution.z });
            await WriteArrayAsync(stream, new int[] { Dimension.x, Dimension.y, Dimension.z });
            await WriteArrayAsync(stream, new short[] { DataRange.min, DataRange.max });

            var data = GetBytes(Data);
            var compressedData = Lzf.Compress(data);
            // data block size
            await WriteArrayAsync(stream, new int[] { data.Length, compressedData.Length });

            // compressed data
            await stream.WriteAsync(compressedData, 0, compressedData.Length);

            // tail information
            await WriteULongAsync(stream, version); // end sign
            await stream.FlushAsync(); // flush 하지않고 stream 이 close 되는 경우 마지막 block 이 실제로 쓰여지지 않을 수 있다.
            return stream.Position - beginPos;
        }

        private async Task<long> SaveAsync_v1_1(Stream stream, bool compress)
        {
            var working = compress
                ? new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Compress)
                : stream;

            var beginPos = stream.Position;
            var versionIdbytes = BitConverter.GetBytes((ulong)Version.V1_1);
            await working.WriteAsync(versionIdbytes, 0, versionIdbytes.Length); // begin sign
            await WriteArrayAsync(working, new float[] { Resolution.x, Resolution.y, Resolution.z });
            await WriteArrayAsync(working, new int[] { Dimension.x, Dimension.y, Dimension.z });
            await WriteArrayAsync(working, Data);
            await WriteArrayAsync(working, new short[] { DataRange.min, DataRange.max });
            await working.WriteAsync(versionIdbytes, 0, versionIdbytes.Length); // end sign
            await working.FlushAsync(); // flush 하지않고 stream 이 close 되는 경우 마지막 block 이 실제로 쓰여지지 않을 수 있다.

            return stream.Position - beginPos;
        }

        public static async Task<Int16Volume> LoadAsync_v1_3(Stream stream)
        {
            var versionId = await ReadULongAsync(stream);
            if (versionId != (ulong)Version.V1_3)
                throw new ApplicationException($"invalid version. {versionId} != {Version.V1_3}");

            // volume information
            var res = await ReadArrayAsync<float>(stream, 3);
            var dim = await ReadArrayAsync<int>(stream, 3);
            var range = await ReadArrayAsync<short>(stream, 2);

            // meta data
            var meta = await ReadDictionaryAsync(stream);

            // data block size
            var blockSize = await ReadArrayAsync<int>(stream, 2);
            var originalSize = blockSize[0];
            var compressedSize = blockSize[1];

            // read compressed data and decompress
            var compressedData = await ReadArrayAsync<byte>(stream, compressedSize);
            if (compressedData == null)
                throw new ApplicationException($"invalid data. {originalSize}, {compressedSize}, {stream.Position}");
            var originalData = Deflate.Decompress(compressedData);

            if (originalData.Length != originalSize)
                throw new ApplicationException($"invalid data block size. {originalData.Length} != {originalSize}");
            var tailId = await ReadULongAsync(stream);
            if (versionId != tailId) throw new ApplicationException($"invalid end sign {tailId} != {versionId}");

            return new Int16Volume
            {
                Resolution = (res[0], res[1], res[2]),
                Dimension = (dim[0], dim[1], dim[2]),
                DataRange = (range[0], range[1]),
                Data = GetArray<short>(originalData),
                Meta = meta
            };
        }

        private static async Task<Int16Volume> LoadAsync_v1_2(Stream stream)
        {
            var versionId = await ReadULongAsync(stream);
            if (versionId != (ulong)Version.V1_2)
                throw new ApplicationException($"invalid version. {versionId} != {Version.V1_2}");

            // volume information
            var res = await ReadArrayAsync<float>(stream, 3);
            var dim = await ReadArrayAsync<int>(stream, 3);
            var range = await ReadArrayAsync<short>(stream, 2);

            // data block size
            var blockSize = await ReadArrayAsync<int>(stream, 2);
            var originalSize = blockSize[0];
            var compressedSize = blockSize[1];

            // read compressed data and decompress
            var compressedData = await ReadArrayAsync<byte>(stream, compressedSize);
            if (compressedData == null)
                throw new ApplicationException($"invalid data. {originalSize}, {compressedSize}, {stream.Position}");
            var originalData = Lzf.Decompress(compressedData);
            if (originalData.Length != originalSize)
                throw new ApplicationException($"invalid data block size. {originalData.Length} != {originalSize}");
            var tailId = await ReadULongAsync(stream);
            if (versionId != tailId) throw new ApplicationException($"invalid end sign {tailId} != {versionId}");

            return new Int16Volume
            {
                Resolution = (res[0], res[1], res[2]),
                Dimension = (dim[0], dim[1], dim[2]),
                DataRange = (range[0], range[1]),
                Data = GetArray<short>(originalData)
            };
        }

        private static async Task<Int16Volume> LoadAsync_v1_1(Stream stream, bool gzip)
        {
            // 파일 형식에 따라 stream 선택
            var working = gzip
                ? new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress)
                : stream;

            var versionIdBytes = new byte[sizeof(ulong)];
            await working.ReadAsync(versionIdBytes, 0, versionIdBytes.Length);
            var versionId = BitConverter.ToUInt64(versionIdBytes, 0);
            if (versionId != (ulong)Version.V1_1) throw new ApplicationException($"invalid version {versionId} != {Version.V1_1}");
            var res = await ReadArrayAsync<float>(working, 3);
            var dim = await ReadArrayAsync<int>(working, 3);
            var ret = new Int16Volume
            {
                Resolution = (res[0], res[1], res[2]),
                Dimension = (dim[0], dim[1], dim[2])
            };
            ret.Data = await ReadArrayAsync<short>(working, dim[0] * dim[1] * dim[2]);
            var range = await ReadArrayAsync<short>(working, 2);
            ret.DataRange = (range[0], range[1]);
            var readbyte = await working.ReadAsync(versionIdBytes, 0, versionIdBytes.Length);
            if (readbyte != versionIdBytes.Length ||
                res == null || // 제대로 읽지 못한 경우 null
                dim == null || ret.Data == null || range == null)
            {
                throw new ApplicationException($"invalid file size");
            }
            versionId = BitConverter.ToUInt64(versionIdBytes, 0);
            if (versionId != (ulong)Version.V1_1) throw new ApplicationException($"invalid end sign {versionId} != {Version.V1_1}");

            return ret;
        }

        #endregion 각 버전별 load / save 함수들

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
        public int[/*y*/, /*x*/] CreateHeightMap(short thresholdValue = 1, HeightDirection dir = HeightDirection.ANTERIOR)
        {
            // axial volume 기준

            const int NAN_VALUE = -1;

            // TODO: swtich 및 각 방향별 별도 함수 사용하지 않고 genric한 방식으로.
            switch (dir)
            {
                case HeightDirection.ANTERIOR:
                    return CreateHeightMapAnterior(thresholdValue, NAN_VALUE);

                case HeightDirection.POSTERIOR:
                    return CreateHeightMapPosterior(thresholdValue, NAN_VALUE);

                case HeightDirection.SUPERIOR:
                    return CreateHeightMapSuperior(thresholdValue, NAN_VALUE);

                case HeightDirection.INFERIOR:
                    return CreateHeightMapInferior(thresholdValue, NAN_VALUE);

                case HeightDirection.LEFT:
                    return CreateHeightMapLeft(thresholdValue, NAN_VALUE);

                case HeightDirection.RIGHT:
                    return CreateHeightMapRight(thresholdValue, NAN_VALUE);

                default:
                    break;
            }

            throw new NotImplementedException($"unsupported direction : {dir}");
        }

        public static (float x, float y, float z) TransformByDirection(HeightDirection dir, (float x, float y, float z) origin)
        {
            switch(dir)
            {
                case HeightDirection.SUPERIOR:
                    return (-origin.x, origin.y, origin.z);
                case HeightDirection.INFERIOR:
                    return (origin.x, origin.y, -origin.z);
                case HeightDirection.ANTERIOR:
                    return (origin.x, origin.z, origin.y);
                case HeightDirection.POSTERIOR:
                    return (-origin.x, origin.z, -origin.y);
                case HeightDirection.LEFT:
                    return (origin.y, origin.z, -origin.x);
                case HeightDirection.RIGHT:
                    return (-origin.y, origin.z, origin.x);
            }
            throw new ArgumentException($"unknown direction : {(int)dir}", nameof(dir));
        }

        public int GetIndex(int posZ, int posY, int posX)
        {
            return posX + posY * Dimension.x + posZ * Dimension.x * Dimension.y;
        }

        public short GetValue(int posZ, int posY, int posX)
        {
            return Data[GetIndex(posZ, posY, posX)];
        }

        #region 각 방향별 CreateHeightMap

        // nanValue 정보가 끝까지 나타나지 않았을 때 값.
        private int[,] CreateHeightMapSuperior(short threshold, int nanValue)
        {
            var heights = new int[Dimension.y, Dimension.x];

            for (var x = 0; x < Dimension.x; ++x)
            {
                for (var y = 0; y < Dimension.y; ++y)
                {
                    var height = nanValue;
                    for (var z = 0; z < Dimension.z; ++z)
                    {
                        if (GetValue(z, y, x) >= threshold)
                        {
                            height = Dimension.z - 1 - z;
                            break;
                        }
                    }
                    heights[y, Dimension.x - 1 - x] = height;
                }
            }
            return heights;
        }

        private int[,] CreateHeightMapInferior(short threshold, int nanValue)
        { // CT acxial view 와 같은 direction
            var heights = new int[Dimension.y, Dimension.x];

            for (var x = 0; x < Dimension.x; ++x)
            {
                for (var y = 0; y < Dimension.y; ++y)
                {
                    var height = nanValue;
                    for (var z = 0; z < Dimension.z; ++z)
                    {
                        if (GetValue(Dimension.z - z - 1, y, x) >= threshold)
                        {
                            height = Dimension.z - 1 - z;
                            break;
                        }
                    }
                    heights[y, x] = height;
                }
            }
            return heights;
        }

        private int[,] CreateHeightMapAnterior(short threshold, int nanValue)
        {
            var heights = new int[Dimension.z, Dimension.x];

            for (var x = 0; x < Dimension.x; ++x)
            {
                for (var z = 0; z < Dimension.z; ++z)
                {
                    var height = nanValue;
                    for (var y = 0; y < Dimension.y; ++y)
                    {
                        if (GetValue(z, y, x) >= threshold)
                        {
                            height = Dimension.y - 1 - y;
                            break;
                        }
                    }
                    heights[z, x] = height;
                }
            }
            return heights;
        }

        private int[,] CreateHeightMapPosterior(short threshold, int nanValue)
        {
            var heights = new int[Dimension.z, Dimension.x];

            for (var x = 0; x < Dimension.x; ++x)
            {
                for (var z = 0; z < Dimension.z; ++z)
                {
                    var height = nanValue;
                    for (var y = 0; y < Dimension.y; ++y)
                    {
                        if (GetValue(z, Dimension.y - y - 1, x) >= threshold)
                        {
                            height = Dimension.y - 1 - y;
                            break;
                        }
                    }
                    heights[z, Dimension.x - 1 - x] = height;
                }
            }
            return heights;
        }

        private int[,] CreateHeightMapLeft(short threshold, int nanValue)
        {
            var heights = new int[Dimension.z, Dimension.y];

            for (var y = 0; y < Dimension.y; ++y)
            {
                for (var z = 0; z < Dimension.z; ++z)
                {
                    var height = nanValue;
                    for (var x = 0; x < Dimension.x; ++x)
                    {
                        if (GetValue(z, y, Dimension.x - 1 - x) >= threshold)
                        {
                            height = Dimension.x - 1 - x;
                            break;
                        }
                    }
                    heights[z, y] = height;
                }
            }
            return heights;
        }

        private int[,] CreateHeightMapRight(short threshold, int nanValue)
        {
            var heights = new int[Dimension.z, Dimension.y];

            for (var y = 0; y < Dimension.y; ++y)
            {
                for (var z = 0; z < Dimension.z; ++z)
                {
                    var height = nanValue;
                    for (var x = 0; x < Dimension.x; ++x)
                    {
                        if (GetValue(z, y, x) >= threshold)
                        {
                            height = Dimension.x - 1 - x;
                            break;
                        }
                    }
                    heights[z, Dimension.y - 1 - y] = height;
                }
            }
            return heights;
        }

        #endregion 각 방향별 CreateHeightMap

        #region helpers

        private static async Task<bool> IsGzipStream(Stream stream)
        {
            const ushort GZIP_LEAD_BYTES = 0x8b1f; // https://stackoverflow.com/a/11996441
            var beginPos = stream.Position;
            var identifier = new byte[sizeof(ushort)];
            await stream.ReadAsync(identifier, 0, identifier.Length);
            var gzip = BitConverter.ToUInt16(identifier, 0) == GZIP_LEAD_BYTES;
            stream.Position = beginPos;
            return gzip;
        }

        #endregion helpers
    }
}

#nullable restore