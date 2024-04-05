using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if FO_DICOM
using FellowOakDicom;
#endif

#nullable enable

namespace Mars
{
    public static class DicomHelper
    {
        // https://github.com/fo-dicom/fo-dicom/blob/development/DICOM/DicomElement.cs#L158
        // fo-dicom에서 여러개의 문자열로 되어있는 경우 구분자 문자열 사이에 "\\"을 추가하여 구분.
        public const string ARRAY_SEPARATOR = "\\"; // string 이 아닌 char 를 사용하는 경우 Unity(.NET Standard 2.0) 호환성 문제
        public const short CT_HU_MIN = -1024;
        public const short CT_HU_MAX = 3071;
        /// <summary>
        ///     DICOM 에 정의되지 않은 문자 set 이 포함된 경우 시도할 encoding
        /// </summary>
        /// <remarks>
        ///     빈드시 multibyte encoding 이어야 함
        /// </remarks>
        public static Encoding FallbackEncoding = Encoding.GetEncoding("euc-kr");

        public static string DicomJoin<T>(this T[] dicomValues)
        {
            return string.Join(ARRAY_SEPARATOR, dicomValues);
        }

        public static string[] DicomSplit(this string joinedDicomValues)
        {
            return joinedDicomValues.Split(ARRAY_SEPARATOR.ToCharArray());
        }


        public static bool IsLocalizer(string[]? dicomImageTypes)
        {
            if (dicomImageTypes == null || dicomImageTypes.Length == 0) return false;
            return dicomImageTypes.Contains("LOCALIZER", StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsAxial(float[]? dicomImageOrientationPatient)
        {
            if (dicomImageOrientationPatient == null || dicomImageOrientationPatient.Length != 6) return false;
            return IsAixal(DigitizedOrientation(dicomImageOrientationPatient));
        }

        public static bool IsCt(string? dicomModality)
        {
            if (string.IsNullOrWhiteSpace(dicomModality)) return false;
            return string.Compare(dicomModality, "CT", true) == 0;
        }

        public static bool HasImagePositionPatient(float[]? imagePositionPatient)
        {
            if (imagePositionPatient == null || imagePositionPatient.Length != 3) return false;
            return true;
        }

        public static bool HasImageOrientationPatient(float[]? imageOrientationPatient)
        {
            if (imageOrientationPatient == null || imageOrientationPatient.Length != 6) return false;
            return true;
        }

        public static NrrdVector[] GetDirectionVectors(IEnumerable<double> imageOrientationPatient)
        {
            if (imageOrientationPatient.Count() != 6)
                throw new ArgumentException($"invalid size : {imageOrientationPatient.Count()}", nameof(imageOrientationPatient));

            // dicom 데이터에 이미 normalized 되어있는 데이터라고 가정
            var rlDir = new NrrdVector(imageOrientationPatient.Take(3).ToArray());
            var apDir = new NrrdVector(imageOrientationPatient.Skip(3).ToArray());
            var isDir = rlDir.Cross(apDir);
            return new NrrdVector[]
            {
                rlDir,
                apDir,
                isDir
            };
        }

        public static NrrdVector[] GetDirectionVectors(IEnumerable<float> imageOrientationPatient)
        {
            return GetDirectionVectors(imageOrientationPatient.Cast<double>().ToArray());

        }

        #region helpers

        private static readonly int[] AXIAL_SUPINE_DIR = { 1, 0, 0, 0, 1, 0 };
        private static readonly int[] AXIAL_PRONE_DIR = { 1, 0, 0, 0, -1, 0 };

        private static bool IsAixal(IEnumerable<int> digitizedOrientation)
        {
            return digitizedOrientation.SequenceEqual(AXIAL_SUPINE_DIR)
                | digitizedOrientation.SequenceEqual(AXIAL_PRONE_DIR);
        }

        public static IEnumerable<int> DigitizedOrientation(IEnumerable<float> vector)
        {
            static int pred(float x)
            {
                if (x > 0.5f) return 1;
                if (x < -0.5f) return -1;
                return 0;
            }

            return vector.Select(pred);
        }

        #endregion helpers

#if FO_DICOM
        public static string? GetString(this DicomDataset ds, DicomTag tag, Encoding fallbackEncoding)
        { // 
            if (ds.Contains(tag) == false) return null;
            var value = ds.GetString(tag);
            var needEncoding = tag.DictionaryEntry.ValueRepresentations.Any(
                vr => // http://dicom.nema.org/medical/dicom/current/output/chtml/part05/sect_6.2.html
                vr.IsString // PN, UT, LO, LT, SH, ST
            );
            if (needEncoding == false) return value;
            // DICOM text ecoding 범위 밖인 character 를 포함하는 경우 문제 처리
            //  https://github.com/dev-skia/shared-cs/issues/2#issuecomment-1712553622
            var encoding = ds.Contains(DicomTag.SpecificCharacterSet)
                ? DicomEncoding.GetEncoding(ds.GetString(DicomTag.SpecificCharacterSet)) // https://github.com/technocratz/fo-dicom-master/blob/master/DICOM/DicomEncoding.cs
                : Encoding.ASCII; // single byte character
            if (encoding.IsSingleByte == false) // "ISO 2022 IR 149" 등 multi-byte characterset
                return value; // 알아서 encoding 잘 되었겠지...
            var rawBytes = ds.GetValues<byte>(tag);
            if (rawBytes == null || rawBytes.Length == 0) return value;
            if (rawBytes.Any(c => (int)c > 127) == false) // single-byte characterset 벙위 이내.
                return value;

            // 잘못된 Encoding(지정한 encoding 과 다른 characterset 포함)            
            var encodedBytes = Encoding.Convert(fallbackEncoding, Encoding.UTF8, rawBytes);
            var encoded = Encoding.UTF8.GetString(encodedBytes);
            return encoded;
        }
#endif
    }
}

#nullable restore