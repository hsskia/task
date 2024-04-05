using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Mars
{
    /// <summary>
    ///     nrrd header 에 적용된 vector type. element type은 double 형으로 고정
    /// </summary>
    public class NrrdVector
    {
        private readonly double[] data;

        public int Dimension => data.Length;

        public double Length
        {
            get
            {
                return Math.Sqrt(data.Sum(x => x * x));
            }
        }

        public double this[int index]
        {
            get { return data[index]; }
            set { data[index] = value; }
        }

        public NrrdVector(int dimension)
        {
            data = new double[dimension];
        }

        public NrrdVector(double[] src, bool copy = true)
        {
            if (copy == false)
            {
                data = src;
                return;
            }
            data = new double[src.Length];
            Array.Copy(src, data, src.Length);
        }

        public NrrdVector Cross(NrrdVector b)
        {
            if (data.Length != 3 || b.data.Length != 3)
                throw new NotSupportedException($"only 3 dimensional cross product supported");
            return new NrrdVector(new double[] {
                data[1] * b.data[2] - data[2] * b.data[1],
                data[2] * b.data[0] - data[0] * b.data[2],
                data[0] * b.data[1] - data[1] * b.data[0]
            });
        }

        public double Dot(NrrdVector b)
        {
            if (data.Length != b.data.Length)
                throw new ArgumentException($"not same size: {data.Length} != {b.data.Length}", nameof(b));
            var sum = 0.0;
            for (var i = 0; i < data.Length; ++i)
            {
                sum += data[i] * b.data[i];
            }
            return sum;
        }

        public double Angle(NrrdVector b)
        {
            var dot = Dot(b);
            var mag = Length * b.Length;
            var angle = Math.Acos(dot / mag);
            return double.IsNaN(angle) ? 0.0 : angle;
        }

        public NrrdVector Normalize()
        {
            return (new NrrdVector(data)) * (1 / Length);
        }

        public static NrrdVector operator *(NrrdVector src, double scale)
        {
            return new NrrdVector(src.data.Select(x => x * scale).ToArray());
        }

        public static NrrdVector operator -(NrrdVector src)
        {
            return new NrrdVector(src.data.Select(x => -x).ToArray());
        }

        public static NrrdVector operator +(NrrdVector a, NrrdVector b)
        {
            if (a.data.Length != b.data.Length)
                throw new ArgumentException($"size mismatched for NrrdVector+ : {a.data.Length}/{b.data.Length}");
            var ret = new NrrdVector(a.data.Length);
            for (var i = 0; i < a.data.Length; ++i)
            {
                ret[i] = a.data[i] + b.data[i];
            }
            return ret;
        }

        public static NrrdVector operator -(NrrdVector a, NrrdVector b)
        {
            return a + (-b);
        }

        public override bool Equals(object obj)
        {
            var other = obj as NrrdVector;
            if (other is null) return base.Equals(obj);
            if (other.data.Length != data.Length) return false;
            return other.data.SequenceEqual(data);
        }

        public override int GetHashCode()
        { // Equals 만 있으면 CS0659 warning
            return data.Aggregate(0, (s, d) => unchecked(s + d.GetHashCode()));
        }

        // TODO: 추가적인 연산 함수들(필요한 경우)

        #region Helpers for conversion

        /// <summary>
        ///     NRRD spec 에 따른 vector 의 string(header) 표현
        /// </summary>
        public override string ToString()
        {
            return $"({string.Join(",", data.Select(o => o.ToString()))})";
        }

        public static NrrdVector Parse(string src)
        {
            src = src.Trim();
            if (src.StartsWith("(") == false || src.EndsWith(")") == false)
                throw new FormatException($"invalid format of NrrdVector : {src}");
            src = src.TrimStart('(').TrimEnd(')');
            var elems = src.Split(',');
            return new NrrdVector(
                elems.Select(x => double.Parse(x)).ToArray()
                );
        }

        public string ToDicomString()
        {
            return data.Select(x => x.ToString()).ToArray().DicomJoin();
        }

        #endregion Helpers for conversion
    }

    /// <summary>
    ///     NRRD header(field) 및 NRRD-Int16Volume 과 관련된 extension methods
    /// </summary>
    public static class NrrdExt
    {
        public static NrrdRaw ToNrrd(this Int16Volume vol)
        {
            if (vol is null)
                throw new ArgumentNullException(nameof(vol));

            var sizes = vol.Dimension.ToArray();
            var nrrd = new NrrdRaw(sizes, NrrdHeader.DataType.@short);
            nrrd.Header.space_origin = NrrdSpaceOrigin(vol);
            nrrd.Header.space_directions = NrrdSpaceDirections(vol);
            foreach (var kv in vol.Meta) // Int16Volume 의 Meta 정보를 유실하지 않도록
                nrrd.Header.key_value[kv.Key] = kv.Value;

            // TODO: Int16Volume 의 segmentation meta 정보를 slicer 와 맞출 필요.
            //    segmentation volume 의 경우 slice 에서 사용하는 segmentation 정보가 필요
            //    nrrd.Header.key_value 설정.

            // LPI (int16volume) to LPS
            var lps = FlipZ(vol.Data, vol.Dimension);
            lps.CopyTo(nrrd.Int16Data);
            return nrrd;
        }

        [Obsolete("변환해 사용하기보다 NrrdRaw 를 직접 사용하도록")]
        public static Int16Volume ToInt16Volume(this NrrdRaw nrrd, (short min, short max) dataRange)
        {
            if (nrrd is null)
                throw new ArgumentNullException(nameof(nrrd));
            if (nrrd.Header.type.ByteSize() > 2)
                throw new NotSupportedException($"not suported volume transform type : {nrrd.Header.type}");

            var vol = new Int16Volume();
            // key_value 모든 데이터를 유지하는 것이 맞나?
            // Meta 에 postion, rotation 정보 저장을 위해 먼저 생성
            vol.Meta = new Dictionary<string, string>(nrrd.Header.key_value);
            vol.Meta["META"] = "NRRD"; // nrrd 로부터 생성된 것을 태그.

            vol.Dimension = nrrd.Header.sizes.ToTuple3();
            vol.DataRange = dataRange;
            vol.Resolution = Int16VolumeResolution(nrrd.Header);

            // additional dicom meta information
            // Dictionary Add 를 사용하면 이미 존재하는 key 의 경우 exception 이 발생할 수 있으므로 index 로 대체
            if (!(nrrd.Header.space_origin is null))
                vol.Meta[DICOM_TAG_NAME_IMAGE_POSITION_PATIENT] = DicomPosition(nrrd.Header);
            if (!(nrrd.Header.space_directions is null))
                vol.Meta[DICOM_TAG_NAME_IMAGE_ORIENTATION_PATIENT] = DicomOrientation(nrrd.Header);

            // data block 설정
            var int16data = nrrd.Header.type.ByteSize() == 2
                ? nrrd.Int16Data.Span
                : Array.ConvertAll(nrrd.Int8Data.ToArray(), c => (short)c); // nrrd.Raw 를 사용하면 한번의 복사(ToArray)는 줄일 수 있을텐데..

            // LPS to LPI
            vol.Data = FlipZ(int16data, vol.Dimension);
            return vol;
        }

        public static NrrdRaw ConvertToInt16(this NrrdRaw nrrd)
        {
            if (nrrd.Header.type.ByteSize() == 2 && nrrd.Header.type.IsUnsigned() == false)
                return nrrd; // 변환 불필요
            var header = nrrd.Header.Clone(true); // 수정내용이 src 를 변경시키지 않도록
            header.type = NrrdHeader.DataType.int16; // convert target
            if (nrrd.Header.type.ByteSize() == 2 && nrrd.Header.type.IsUnsigned())
            { // uint16 to int16
                return new NrrdRaw(header, nrrd.GetBytes());
            }
            // converting to Int16
            var elementSize = nrrd.Header.sizes.TotalSize();
            var buffer = new byte[elementSize * sizeof(short)];
            var vol = (nrrd.Int8Data.Length > 0) ? ToInt16Array(nrrd.Int8Data.Span) :
                (nrrd.UInt8Data.Length > 0) ? ToInt16Array(nrrd.UInt8Data.Span) :
                (nrrd.Int32Data.Length > 0) ? ToInt16Array(nrrd.Int32Data.Span) :
                (nrrd.UInt32Data.Length > 0) ? ToInt16Array(nrrd.UInt32Data.Span) :
                null;
            if (vol == null)
                throw new NotSupportedException($"failed to convert to int16. not supported type {nrrd.Header.type}");

            Buffer.BlockCopy(vol, 0, buffer, 0, buffer.Length);
            return new NrrdRaw(header, buffer);
        }

        private static short[] ToInt16Array(Span<byte> src)
        {
            var ret = new short[src.Length];
            for (var i = 0; i < ret.Length; ++i)
            {
                ret[i] = src[i];
            }
            return ret;
        }

        private static short[] ToInt16Array(Span<sbyte> src)
        {
            var ret = new short[src.Length];
            for (var i = 0; i < ret.Length; ++i)
            {
                ret[i] = src[i];
            }
            return ret;
        }

        private static short[] ToInt16Array(Span<int> src)
        {
            var ret = new short[src.Length];
            for (var i = 0; i < ret.Length; ++i)
            {
                ret[i] = (short)src[i];
            }
            return ret;
        }

        private static short[] ToInt16Array(Span<uint> src)
        {
            var ret = new short[src.Length];
            for (var i = 0; i < ret.Length; ++i)
            {
                ret[i] = (short)src[i];
            }
            return ret;
        }

        // generic type casting 은 너무 느려 overload 된 Cast 함수로 대체(10배 이상 성능차이)
        //   https://bitbucket.org/alkee_skia/mars-processor/issues/618#comment-64518996
        //private static short[] ToInt16Array<T>(Span<T> src)
        //{
        //    var ret = new short[src.Length];
        //    for (var i = 0; i < ret.Length; ++i)
        //    {
        //        ret[i] = (short)Convert.ChangeType(src[i], typeof(short));
        //    }
        //    return ret;
        //}

        public static int ByteSize(this NrrdHeader.DataType type)
        {
            return (int)type / 100;
        }

        public static int TotalByteSize(this NrrdHeader.DataType type, int[] sizes)
        {
            return sizes.TotalSize() * type.ByteSize();
        }

        public static bool IsUnsigned(this NrrdHeader.DataType type)
        {
            var src = (int)type;
            var bytesInfoRemoved = src - (type.ByteSize() * 100);
            return bytesInfoRemoved > 50;
        }

        public static bool IsLPS(this NrrdHeader.Space space)
        {
            return space == NrrdHeader.Space.LPS || space == NrrdHeader.Space.left___posterior___superior;
        }

        public static int Dimension(this NrrdHeader.Space spc)
        {
            return (int)spc / 100;
        }

        private const string DICOM_TAG_NAME_IMAGE_POSITION_PATIENT = "ImagePositionPatient";
        private const string DICOM_TAG_NAME_IMAGE_ORIENTATION_PATIENT = "ImageOrientationPatient";

#nullable enable

        public static string? ReadLine(this Stream s, Encoding encoding)
        {
            // StreamReader 의 ReadLineAsync 는 buffer 를 사용해 실제 Position 보다
            //   더 큰 position 이동을 하게 되므로 실제 position 을 얻을 수 없어 직접 구현.

            if (s.CanSeek == false)
                throw new NotSupportedException("cannot seek");
            if (s.CanRead == false) return null;

            var begin = s.Position;
            var reader = new StreamReader(s, encoding);
            var line = reader.ReadLine();
            if (line is null)
                return null;
            const string LINE_SEPARATOR = "\n";
            var lineSepByteCount = encoding.GetByteCount(LINE_SEPARATOR);
            var lineByteCount = encoding.GetByteCount(line);
            s.Position = begin + lineByteCount + lineSepByteCount;
            return line;
        }

        private static NrrdVector NrrdSpaceOrigin(Int16Volume vol)
        {
            if (vol.Meta.TryGetValue(DICOM_TAG_NAME_IMAGE_POSITION_PATIENT, out var imagePositionPatient) == false)
                return DefaultNrrdSpaceOrigin();

            var posStr = imagePositionPatient.DicomSplit();
            if (posStr.Length != 3)
                throw new ArgumentException($"length of image position patient should be 3 ({imagePositionPatient})");

            var origin = new NrrdVector(Array.ConvertAll(posStr, (x) => double.Parse(x)));
            var isDir /*interior to superior*/ = NrrdSpaceDirections(vol)[2].Normalize();

            // LPI volume 이므로 volume 길이만큼 superior 쪽으로 이동
            var resZ = vol.Resolution.z * 1000; // m to mm scale
            var isLength = resZ * (vol.Dimension.z - 1);
            var offset = -(isDir * isLength);

            return origin + offset;
        }

        private static NrrdVector[] NrrdSpaceDirections(Int16Volume vol)
        {
            // http://dicomiseasy.blogspot.com/2013/06/getting-oriented-using-image-plane.html

            if (vol.Meta.TryGetValue(DICOM_TAG_NAME_IMAGE_ORIENTATION_PATIENT, out var value) == false)
                return DefaultSpaceDirection(vol.Resolution);

            var dicomDirections = value.DicomSplit()
                .Select(x => double.Parse(x));
            var dir = DicomHelper.GetDirectionVectors(dicomDirections);
            // dir에 resolution 을 곱한 후 *1000(m to mm) 하게되면 precision 문제로 올바른 결과를 얻을 수 없어
            //   반드시 resolution 에 * 1000 을 먼저 해주어야 한다.
            dir[0] *= (vol.Resolution.x * 1000);
            dir[1] *= (vol.Resolution.y * 1000);
            dir[2] *= (vol.Resolution.z * 1000);
            return dir;
        }

        private static NrrdVector[] DefaultSpaceDirection((float x, float y, float z) resolution)
        {
            // 일단 (1, 0, 0) (0, 1, 0) 이라고 가정
            var dir = DefaultNrrdVector3DAxis();
            dir[0] *= resolution.x;
            dir[1] *= resolution.y;
            dir[2] *= resolution.z;
            return dir
                .Select(x => x * 1000) // m to mm
                .ToArray();
        }

#nullable restore

        public static T[] ToArray<T>(this (T x, T y, T z) tup)
        {
            return new T[] { tup.x, tup.y, tup.z };
        }

        public static (T x, T y, T z) ToTuple3<T>(this T[] arr)
        {
            return (arr[0], arr[1], arr[2]);
        }

        #region internal helpers

        private static short[] FlipZ(Span<short> src, (int x, int y, int z) dim)
        {
            var ret = new short[src.Length];
            var sliceSize = dim.x * dim.y;
            for (var z = 0; z < dim.z; ++z)
            {
                var dstPos = (dim.z - z - 1) * sliceSize;
                var dst = new Span<short>(ret, dstPos, sliceSize);
                src.Slice(z * sliceSize, sliceSize).CopyTo(dst);
            }
            return ret;
        }

        public static NrrdVector[] DefaultNrrdVector3DAxis()
        {
            // static readonly로 사용했다가 NrrdVector가 class reference라 값이 바뀌는 문제가 있어서,
            // 항상 Runtime에 똑같은 값을 생성하는 함수로 구현.
            // (1 0 0) (0 1 0) image orientation patient
            return new NrrdVector[]
                {
                    new NrrdVector(new double[] { 1, 0, 0 }),
                    new NrrdVector(new double[] { 0, 1, 0 }),
                    new NrrdVector(new double[] { 0, 0, 1 }),
                };
        }

        public static NrrdVector DefaultNrrdSpaceOrigin()
        {
            // (0 0 0) image orientation position
            return new NrrdVector(new double[] { 0, 0, 0 });
        }

        public static Matrix4x4 GetTransform(this NrrdHeader header)
        {
            if (header.dimension != 3)
                throw new ArgumentException($"nrrd dimension should be 3 ({header.dimension})");
            if (header.space_directions == null)
                throw new ArgumentException("nrrd space direction value is null");
            if (header.space_directions.Length != 3
                || header.space_directions[0].Dimension != 3
                || header.space_directions[1].Dimension != 3
                || header.space_directions[2].Dimension != 3)
                throw new ArgumentException("nrrd space direction has a weird dimension");
            if (header.space_origin == null)
                throw new ArgumentException("nrrd space origin value is null");
            if (header.space_origin.Dimension != 3)
                throw new ArgumentException($"nrrd space origin dimension is weird ({header.space_origin.Dimension})");
            if (header.sizes == null)
                throw new ArgumentException("nrrd sizes value is null");
            if (header.sizes.Length != 3)
                throw new ArgumentException($"nrrd sizes length is weird ({header.sizes.Length})");

            var m0 = header.space_directions[0] * header.sizes[0];
            var m1 = header.space_directions[1] * header.sizes[1];
            var m2 = header.space_directions[2] * header.sizes[2];

            return new Matrix4x4(
                    (float)m0[0], (float)m0[1], (float)m0[2], 0,
                    (float)m1[0], (float)m1[1], (float)m1[2], 0,
                    (float)m2[0], (float)m2[1], (float)m2[2], 0,
                    (float)header.space_origin[0], (float)header.space_origin[1], (float)header.space_origin[2], 1);
        }

        public static (float x, float y, float z) GetResolution(this NrrdHeader header)
        {
            // TODO: 올바른 resolution 을 이렇게 얻는게 맞는지 확인..

            var sd = header.space_directions
                ?? DefaultNrrdVector3DAxis();

            return ((float)sd[0].Length, (float)sd[1].Length, (float)sd[2].Length);
        }

        public static (float x, float y, float z) Int16VolumeResolution(this NrrdHeader header)
        {
            var resolution = GetResolution(header);
            return (resolution.x * 0.001f, resolution.y * 0.001f, resolution.z * 0.001f); // mm scale to m
        }

        private static string DicomPosition(NrrdHeader header)
        {
            // LPS 에서 LPI 좌표로 변경
            //    NrrdSpaceOrigin 함수의 역변환
            var origin = header.space_origin
                ?? throw new ArgumentException($"no space_origin in nrrd header");
            var isDir = header.space_directions?[2]
                ?? throw new ArgumentException($"no space_directions in nrrd header");
            var resZ = isDir[2];
            isDir = isDir.Normalize();
            var isLength = resZ * (header.sizes[2] - 1);
            var offset = isDir * isLength;

            return (origin + offset).ToDicomString();
        }

        private static string DicomOrientation(NrrdHeader header)
        {
            var rlDir = header.space_directions[0].Normalize();
            var apDir = header.space_directions[1].Normalize();
            var combindedStr = new string[]
            {
                rlDir.ToDicomString(),
                apDir.ToDicomString()
            };
            return combindedStr.DicomJoin();
        }

        /// <summary>
        ///     모든 element 의 곱
        /// </summary>
        private static int TotalSize(this int[] sizes)
        {
            return sizes.Aggregate(1, (acc, x) => acc * x);
        }

        #endregion internal helpers
    }
}