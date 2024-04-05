using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mars
{
    public class NrrdHeader
    { // NRRD header - http://teem.sourceforge.net/nrrd/format.html
        // TODO: ? 포함된 key 에 따라 VERSION 이 달라질 수 있도록
        public const int VERSION = 4; // default working version spec.
        private static readonly System.Text.Encoding TextEncoding = new System.Text.UTF8Encoding(false); // Encoding.UTF8 을 사용하면 BOM 이 붙어버리므로 new UTF8Encoding(false) 사용

        #region field specification ordering / minimalist header

        public DataType type { get; set; } // http://teem.sourceforge.net/nrrd/format.html#type
        public int dimension => sizes.Length; // http://teem.sourceforge.net/nrrd/format.html#dimension
        public int[] sizes { get; set; } = Array.Empty<int>(); // http://teem.sourceforge.net/nrrd/format.html#sizes
        public Encoding encoding { get; set; } // http://teem.sourceforge.net/nrrd/format.html#encoding

        #endregion field specification ordering / minimalist header

#nullable enable

        #region Basic field specifications

        public int? block_size { get; set; } // type 이 block 인 경우. http://teem.sourceforge.net/nrrd/format.html#blocksize
        public Endian endian { get; set; } // http://teem.sourceforge.net/nrrd/format.html#endian
        public string? content { get; set; } // http://teem.sourceforge.net/nrrd/format.html#content
        public double? min { get; set; } // http://teem.sourceforge.net/nrrd/format.html#min
        public double? max { get; set; } // http://teem.sourceforge.net/nrrd/format.html#max
        public double? old_min { get; set; } // http://teem.sourceforge.net/nrrd/format.html#oldmin
        public double? old_max { get; set; } // http://teem.sourceforge.net/nrrd/format.html#oldmax
        public string? data_file { get; set; } // http://teem.sourceforge.net/nrrd/format.html#detached
        public int? line_skip { get; set; } // http://teem.sourceforge.net/nrrd/format.html#lineskip
        public int? byte_skip { get; set; } // http://teem.sourceforge.net/nrrd/format.html#byteskip
        public string? number { get; set; } // http://teem.sourceforge.net/nrrd/format.html#number
        public string? sample_units { get; set; } // http://teem.sourceforge.net/nrrd/format.html#sampleunits

        #endregion Basic field specifications

        #region Space and orientation information

        public Space? space { get; set; } // http://teem.sourceforge.net/nrrd/format.html#space
        public int? space_dimension { get; set; } // http://teem.sourceforge.net/nrrd/format.html#spacedimension
        public string[]? space_units { get; set; } // ex) default: ["mm", "mm", "mm"] http://teem.sourceforge.net/nrrd/format.html#spaceunits
        public NrrdVector? space_origin { get; set; }  // http://teem.sourceforge.net/nrrd/format.html#spaceorigin
        public NrrdVector[]? space_directions { get; set; } // http://teem.sourceforge.net/nrrd/format.html#spacedirections
        public NrrdVector[]? measurement_frame { get; set; } // http://teem.sourceforge.net/nrrd/format.html#measurementframe

        #endregion Space and orientation information

        #region Per-axis field specifications

        // Per-axis specifications can only appear after the dimension field specification.
        public double[]? spacings { get; set; } // http://teem.sourceforge.net/nrrd/format.html#spacings

        public double[]? thicknesses { get; set; } // http://teem.sourceforge.net/nrrd/format.html#thicknesses
        public double[]? axis_mins { get; set; } // http://teem.sourceforge.net/nrrd/format.html#axismins
        public double[]? axis_maxs { get; set; } // http://teem.sourceforge.net/nrrd/format.html#axismaxs

        public Center[]? centers { get; set; } // alias:centerings http://teem.sourceforge.net/nrrd/format.html#centers
        public string[]? lables { get; set; } // http://teem.sourceforge.net/nrrd/format.html#labels
        public string[]? units { get; set; } // http://teem.sourceforge.net/nrrd/format.html#units

        public Kind[]? kinds { get; set; } // http://teem.sourceforge.net/nrrd/format.html#kinds

        #endregion Per-axis field specifications

        public Dictionary<string, string> key_value { get; set; } = new Dictionary<string, string>();

        // member field indexer
        public string? this[string field]
        {
            get
            {
                var fieldProperty = FindNrrdField(field);
                if (fieldProperty is null)
                    throw new KeyNotFoundException($"NRRD header property not found : {field}");
                return GetValueString(this, fieldProperty);
            }
            set
            {
                var fieldProperty = FindNrrdField(field);
                if (fieldProperty is null)
                    throw new KeyNotFoundException($"NRRD header property not found : {field}");
                if (fieldProperty.SetMethod is null) // TODO: logging - get 만 있는 field.
                    return;
                SetValueFromString(this, fieldProperty, value);
            }
        }

#nullable restore

        public async Task WriteAsync(Stream s)
        {
            var writer = new StreamWriter(s, TextEncoding);
            writer.NewLine = "\n";

            await WriteVersionAndCommentAsync(writer);
            await WriteFieldsAsync(writer, this);
            await WriteKeyValueAsync(writer, key_value);

            // end of header
            await writer.WriteLineAsync("");
            await writer.FlushAsync();
        }

        public static async Task<NrrdHeader> ReadAsync(Stream stream, bool strictVersion = false)
        {
            var version = await ReadNrrdMagicAsync(stream);
            if (strictVersion && version > VERSION) // TODO: 높은 버전이라도 호환성이 유지될 수 있으므로 강제로 읽기 옵션.
                throw new NotSupportedException($"not supported NRRD version");

            // read header
            int lineCount = 1; // debugging 시에 사용할 읽은 줄 수
            var header = new NrrdHeader();
            while (true)
            {
                await Task.Yield();
                var line = stream.ReadLine(TextEncoding)?.Trim(); // (await reader.ReadLineAsync())?.Trim();
                if (line is null) //
                    throw new FormatException($"invalid nrrd format");
                if (line.Length == 0) // end of header
                    break;

                ++lineCount;
                if (line[0] == '#') // comment
                    continue;
                var (nrrdField, kv) = Parse(line);
                if (nrrdField == false) // <key>:=<value>
                    header.key_value[kv.Key] = kv.Value;
                else // <field>: <desc>
                    header[kv.Key] = kv.Value;
            }
            return header;
        }

        public static async Task<bool> IsNrrdFile(string filePath)
        {
            if (File.Exists(filePath) == false) return false;
            using var reader = new StreamReader(filePath);
            var line = await reader.ReadLineAsync();
            return IsNrrdHeader(line);
        }

        public static bool IsNrrdStream(Stream stream)
        {
            var backup = stream.Position;
            var line = stream.ReadLine(TextEncoding);
            // restore
            stream.Position = backup;
            return IsNrrdHeader(line);
        }

        #region enumeration types

        // string - enum 규칙
        //   시작 _ 은 제거
        //   "___" -->  "-"
        //   "__"  -->  " "
        //   "_"   -->  "_" (no change)

        public enum Space
        {
            right___anteriror___superior = 301,
            RAS, // = right___anteriror___superior, ; 입력된(원본) 값을 유지할 수 있도록 대입(=)은 하지 않음
            left___anterior___superior,
            LAS, // = left_anterior_superior,
            left___posterior___superior,
            LPS, // = left_posterior_superior,
            scanner___xyz,
            _3D___right___handed,
            _3D___left___handed,

            right___anterior___superior___time = 401,
            RAST, // = right_anterior_superior_time,
            left___anterior___superior___time,
            LAST, // = left_anterior_superior_time,
            left___posterior___superior___time,
            LPST, // = left_posterior_superior_time,
            scanner___xyz___time,
            _3D___right___handed___time,
            _3D___left___handed___time,
        }

        public enum DataType
        {
            // 1 byte types : 100 ~ 199
            // 2 bytes types : 200 ~ 299
            // ...
            single__char = 101, int8, int8_t,

            unsigned__char = 151, uchar, uint8, uint8_t,

            @short = 201, short__int, signed__short, signed__short__int, int16, int16_t,

            @ushort = 251, unsigned__short, unsigned__short__int, uint16, uint16_t,

            @int = 401, signed__int, int32, int32_t,

            @uint = 451, unsigned__int, uint32, uint32_t,

            @float = 400,

            longlong = 801, long__long, long__long__int, signed__long__long, signed__long__long__int, int64, int64_t,

            ulonglong = 851, unsigned__long__long, unsigned__long__long__int, uint64, uint64_t,

            @double = 800,

            block = 1, // An opaque chunk of memory with user-defined size (via the "block size:" specifier)
        }

        public enum Encoding
        {
            raw,
            txt, text, ascii,
            hex,
            gz, gzip,
            bz2, bzip2,
        }

        public enum Endian
        {
            little,
            big
        }

        public enum Center
        {
            cell,
            node,
            none
        }

        public enum Kind
        {
            domain,
            space,
            time,
        }

        #endregion enumeration types

        public NrrdHeader Clone(bool deep)
        {
            var clone = (NrrdHeader)MemberwiseClone();
            if (deep == false) // shallow
                return clone;
            clone.key_value = new Dictionary<string, string>(key_value);
            return clone;
        }

        #region Internal helpers

#nullable enable

        private static PropertyInfo? FindNrrdField(string name)
        {
            if (name == "key_value") // nrrd field 가 아닌 별도의 key-value spec 정보
                return null;
            var type = typeof(NrrdHeader);
            return type.GetProperties()
                .FirstOrDefault(x => NormalizeFieldName(x.Name) == NormalizeFieldName(name));
        }

        private static string? GetValueString(NrrdHeader h, PropertyInfo field)
        {
            var value = field.GetValue(h);
            if (value is null) return null;
            return ToNrrdString(value);
        }

        private static object? Parse(Type type, string? value)
        {
            if (value is null) return null;
            // stripping nullable
            if (IsNullable(type)) type = type.GenericTypeArguments[0];

            if (type == typeof(string)) return value;
            if (type.IsEnum) return ParseEnum(type, value);
            if (type.IsArray) return ParseArray(type, value);

            // default using - Static Parse function
            var parseMethod = type.GetMethod("Parse"
                , BindingFlags.Static | BindingFlags.Public
                , null
                , new Type[] { typeof(string) }
                , null);
            if (parseMethod is null)
                throw new InvalidOperationException($"Parse method not found in {type.Name}");
            return parseMethod.Invoke(null, new object[] { value });
        }

        private static void SetValueFromString(NrrdHeader h, PropertyInfo field, string? value)
        {
            if (value is null)
            {
                if (IsNullable(field.PropertyType) == false)
                    throw new InvalidOperationException($"the field of Nrrd header should not be a null : {field.Name}");
                field.SetValue(h, null);
                return;
            }

            field.SetValue(h, Parse(field.PropertyType, value));
        }

#nullable restore

        /// <summary>
        ///     string 입력(nrrd field)과 property 이름의 비교를 하기위해 같은 string 으로 만들어줌
        /// </summary>
        /// <param name="nrrdField">nrrd spec 에서의 field 또는 <see cref="NrrdHeader"/>의 property 이름</param>
        /// <returns>같은 property 와 field 에 대응해 일치하는 이름</returns>
        private static string NormalizeFieldName(string nrrdField)
        {
            return nrrdField
                .ToLower()
                // 변수이름으로 사용할 수 없는 모든 character 제거
                .Replace("_", "")
                .Replace(" ", "")
                ;
        }

        private static bool IsNullable(Type t)
        {
            return Nullable.GetUnderlyingType(t) != null;
        }

        private static object ParseEnum(Type enumType, string value)
        {
            // 내부 enum naming 규칙으로 변환
            if (char.IsNumber(value[0])) value = "_" + value;
            value = value
                .Replace(" ", "__")
                .Replace("-", "___")
                ;
            return Enum.Parse(enumType, value);
        }

        private static object ParseArray(Type arrType, string value)
        {
            // 괄호안에 들어있지 않은 공백을 기준으로 array 작성(배열은 공백으로 분리)
            var elems = Regex.Split(value, @"(?!\(.*)\s(?![^(]*?\))");
            var elemType = arrType.GetElementType()!; // type.IsArray 확인을 했으므로
            var arr = Array.CreateInstance(elemType, elems.Length);
            for (var i = 0; i < arr.Length; ++i)
            {
                arr.SetValue(Parse(elemType, elems[i]), i);
            }
            return arr;
        }

        /// <summary>
        ///     nrrd spec 에 맞춰 값을 string 으로 표현
        /// </summary>
        private static string ToNrrdString(object obj)
        {
            var type = obj.GetType();
            if (type.IsEnum) return EnumToString(obj);
            if (type.IsArray) return ArrayToString(obj);

            // default ToString
            return obj.ToString() ?? "<null>";
        }

        private static string EnumToString(object enumObj)
        {
            var original = enumObj.ToString()!;
            var nrrdValue = original
                .TrimStart('_')
                .Replace("___", "-")
                .Replace("__", " ")
                ;
            return nrrdValue;
        }

        private static string ArrayToString(object arrObj)
        {
            var arr = (Array)arrObj;
            var values = new string[arr.Length];
            for (var i = 0; i < arr.Length; ++i)
            {
#pragma warning disable CS8601, CS8602 // .GetValue 가 .NET 6 에서 null 을 return 할 수있어 .NET standard 2.0 과 sepc 이 달라 warning
                values[i] = arr.GetValue(i).ToString();
#pragma warning restore
            }
            return string.Join(" ", values);
        }

        private static string Escape(string unescaped)
        { // minimal escaping scheme by NRRD specification
            return unescaped
                .Replace(@"\", @"\\")
                .Replace("\r", @"\r")
                .Replace("\n", @"\n")
                .Replace("\t", @"\t")
                ;
        }

        private static string Unescape(string escaped)
        {
            // string.Format(escaped) 를 사용하고자했으나, {0} 과 같은 string 도 escape 되어버리는 문제
            return escaped
                .Replace(@"\\", @"\")
                .Replace(@"\r", "\r")
                .Replace(@"\n", "\n")
                .Replace(@"\t", "\t")
                ;
        }

        private static bool IsNrrdHeader(string firstLine)
        {
            if (string.IsNullOrWhiteSpace(firstLine)) return false;
            try
            {
                return GetVersionNumber(firstLine) > 0;
            }
            catch
            {
                return false;
            }
        }

        private static int GetVersionNumber(string firstLine)
        { // http://teem.sourceforge.net/nrrd/format.html#general.1
            const string NRRD_MAGIC = "NRRD00";
            if (firstLine.Length < NRRD_MAGIC.Length + 2) // not enough size
                throw new FormatException($"not enough nrrd magic : {firstLine}");

            var versionPos = firstLine.IndexOf(NRRD_MAGIC, StringComparison.OrdinalIgnoreCase);
            if (versionPos != 0)
                throw new FormatException($"invalid nrrd magic : {firstLine}");
            var versionString = firstLine.Substring(NRRD_MAGIC.Length);
            versionString = versionString.TrimStart('.'); // NRRD00.01 for circa 1998 files handling
            if (int.TryParse(versionString, out var version))
            {
                return version;
            }
            throw new FormatException($"invalid nrrd magic version : {firstLine}");
        }

        /// <summary>
        ///     nrrd header 의 한 줄 parsing
        /// </summary>
        /// <returns>nrrdField : field-desc 입력인 경우 true, key-value 입력인 경우 false</returns>
        /// <exception cref="FormatException">nrrd header 규칙에 맞지 않는 format 일 경우</exception>
        private static (bool nrrdField, KeyValuePair<string, string> kv) Parse(string trimmedHeaderLine)
        {
            if (trimmedHeaderLine is null)
                throw new ArgumentNullException(nameof(trimmedHeaderLine));
            var sepPos = trimmedHeaderLine.IndexOf(':');
            if (sepPos < 1)
                throw new FormatException($"no field/key separator");

            var k = Unescape(trimmedHeaderLine.Substring(0, sepPos).TrimEnd());
            var v = Unescape(trimmedHeaderLine.Substring(sepPos + 1).TrimStart());

            if (v.Length > 0 && v[0] == '=') // // <key>:=<value>
                return (false, new KeyValuePair<string, string>(k, v.Substring(1)));

            // <field>: <desc>
            return (true, new KeyValuePair<string, string>(k, v));
        }

        private static async Task WriteVersionAndCommentAsync(StreamWriter writer)
        {
            // version
            await writer.WriteLineAsync($"NRRD{VERSION:0000}");

            // comments(same as slicer's)
            await writer.WriteLineAsync($"# Complete NRRD file format specification at:");
            await writer.WriteLineAsync($"# http://teem.sourceforge.net/nrrd/format.html");
        }

        /// <summary>
        ///     nrrd header spec 에 따라 fields-desc 출력
        /// </summary>
        /// <remarks><see cref="NrrdHeader.key_value"/>는 제외하고 field-desc sepc 의 정보만 쓰게 됨</remarks>
        private static async Task WriteFieldsAsync(StreamWriter writer, NrrdHeader header)
        {
            const string FIELD_DESC_SEPARATOR = ": ";
            var fields = typeof(NrrdHeader).GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance);
            // class 에 정의된 field 순서대로
            foreach (var f in fields)
            {
                if (f.Name == "Item") // index 기본 정의된 property
                    continue;
                if (f.Name == "key_value") // 추후에 별도 처리
                    continue;
                var nrrdFieldName = f.Name
                    .Replace("_", " ") // property name 에 _ 을 제거하면 nrrd spec 에 명시된 field name
                    ;
                var fieldValue = GetValueString(header, f);
                if (fieldValue is null) continue;
                var line = $"{Escape(nrrdFieldName)}{FIELD_DESC_SEPARATOR}{Escape(fieldValue)}";
                await writer.WriteLineAsync(line);
            }
        }

        private static async Task WriteKeyValueAsync(StreamWriter writer, Dictionary<string, string> key_value)
        {
            const string KEY_VALUE_SEPARATOR = ":=";
            foreach (var kv in key_value)
            {
                var line = $"{Escape(kv.Key)}{KEY_VALUE_SEPARATOR}{Escape(kv.Value)}";
                await writer.WriteLineAsync(line);
            }
        }

        /// <summary>
        ///     nrrd spec 에서의 NRRD format 구분자(NRRD magic)를 읽음
        /// </summary>
        /// <returns>읽어진 정보에서 NRRD version 정보</returns>
        /// <exception cref="FormatException">nrrd spec 에 맞지 않는 데이터인 경우</exception>
        private static async Task<int> ReadNrrdMagicAsync(Stream stream)
        {
            await Task.Yield();
            var firstLine = stream.ReadLine(TextEncoding);
            if (firstLine is null)
                throw new FormatException($"invalid nrrd format");
            return GetVersionNumber(firstLine);
        }

        #endregion Internal helpers
    }
}