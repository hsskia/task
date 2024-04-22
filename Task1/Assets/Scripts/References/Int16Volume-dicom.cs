#if FO_DICOM // dependency

using FellowOakDicom; // https://fo-dicom.github.io/stable/v5/index.html
using FellowOakDicom.Log;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Imaging.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace Mars
{
    public partial class Int16Volume
    { // Dicom 관련 등 Mars-Processor 에서 사용하기 어려운 부분을 extension method 로 제공
        /// <summary>
        ///     dicom file 들을 이용해 Int16Volume 을 생성
        /// </summary>
        /// <param name="dicomFilePaths">volume 생성에 사용할 dicom files</param>
        /// <param name="logger">로깅용</param>
        /// <param name="excludeUnavailable">사용할 수 없는 파일이 있을 때 가능하다면 exception 발생하지 않고 제외</param>
        /// <exception cref="ArgumentException">
        ///     dicomFilePath 가 비어있는 경우
        /// </exception>
        /// <exception cref="ApplicationException">
        ///     volume 에 사용될 수 없는 파일 또는 정보가 포함되는 경우
        /// </exception>
        /// <returns></returns>
        public static async Task<Int16Volume> LoadAsync(
            IEnumerable<string> dicomFilePaths, 
            bool excludeUnavailable, 
            ILogger? logger = null
        )
        { // Int16Volume 내에 static 함수로 load 함수(Int16Volume)가 있다면 좋겠지만, 불가능하기때문에 extension method 로 구현

            var slices = await CreateDicomSlices(dicomFilePaths, excludeUnavailable, logger);

            // validation : volume 을 만들 수 크기인지 확인 - 일단 하드코딩
            const int MINIMAL_SLICE_COUNT = 10;
            const int MAXIMUM_SLICE_COUNT = 1_200; // https://bitbucket.org/alkee_skia/mars-tool/issues/10
            if (slices.Count < MINIMAL_SLICE_COUNT || slices.Count > MAXIMUM_SLICE_COUNT)
            {
                throw new ApplicationException(
                    $"invalid count of dicom images({slices.Count}/{dicomFilePaths.Count()})" +
                    $"to create volume. MIN:{MINIMAL_SLICE_COUNT}, MAX:{MAXIMUM_SLICE_COUNT}");
            }

            var volume = CreateLPIVolume(slices,
                out var dimX, out var dimY, out var dimZ, 
                out var min, out var max, out var zLength);
            var first = slices.FirstOrDefault();
            var zResolution = (zLength + first!.SliceThickness) / slices.Count;
            logger?.Log(LogLevel.Info, $"volume read. zLength={zLength}, thickness={first.SliceThickness}, count ={slices.Count}/{dicomFilePaths.Count()}");
            return new Int16Volume
            {
                Dimension = (first.Columns, first.Rows, slices.Count),
                Data = volume,
                DataRange = (min, max),
                Resolution = (first.PixelSpacing.x * 0.001f, first.PixelSpacing.y * 0.001f, zResolution * 0.001f), // mm scale to m scale
                Meta = CreateDicomMeta(first),
            };
        }

        private static async Task<List<DicomSlice>> CreateDicomSlices(
            IEnumerable<string> dicomFilePaths,
            bool excludeUnavailable,
            ILogger? logger = null)
        {
            if (dicomFilePaths.Count() == 0) throw new ArgumentException("no source input", nameof(dicomFilePaths));

            // https://bitbucket.org/alkee_skia/mars-processor/src/51ef2a37e03c804e09618461bcb798d65bba6fac/Assets/Core/Scripts/DicomManager.cs#lines-63
            var slices = new List<DicomSlice>();
            DicomSlice? first = null;
            foreach (var file in dicomFilePaths)
            {
                var dicom = await DicomFile.OpenAsync(file, FileReadOption.ReadAll);
                var available = IsSupportedSlice(dicom.Dataset);
                if (excludeUnavailable && available == false)
                {
                    logger?.Log(LogLevel.Warning, $"not supported DICOM file for volume : {file}");
                    continue;
                }
                if (available == false) throw new ApplicationException($"not supported slice data : {file}");
                var slice = new DicomSlice(dicom.Dataset);
                if (first == null) first = slice;
                // orientation 은 IsSupportedSlice 에 의해 axial 만 추려져있을 것.
                if (slice.Columns != first.Columns || slice.Rows != first.Rows // should be same size
                    || slice.PixelSpacing.x != first.PixelSpacing.x || slice.PixelSpacing.y != first.PixelSpacing.y) // should be same pixel size
                {
                    var sliceInfo = $"({slice.Columns}, {slice.Rows}/{slice.PixelSpacing.x},{slice.PixelSpacing.y})";
                    var firstSliceInfo = $"({first.Columns}, {first.Rows}/{first.PixelSpacing.x},{first.PixelSpacing.y})";
                    throw new ApplicationException($"mismatched DICOM image {sliceInfo}!={firstSliceInfo} : {file}");
                }
                // TODO: 방향은 axial 이지만 x,y 위치가 다른(어긋나있는?)경우 처리

                slices.Add(slice);
            }

            return slices;
        }

        private static Dictionary<string, string> CreateDicomMeta(DicomSlice mostSuperiorSlice)
        {
            var ds = mostSuperiorSlice.Source;
            var meta = new Dictionary<string, string>();

            // DicomSlice 기본 정보
            meta.Add("META", "DICOM"); // skin 이나 lesion segmentation 의 경우에 서로 다른 값.
            meta.Add(nameof(DicomTag.SliceThickness), mostSuperiorSlice.SliceThickness.ToString()); // slice 들이 겹쳐져있는 경우 resolution.z 와 다를 수 있어 정보 유지
            meta.Add(nameof(DicomTag.PatientPosition), mostSuperiorSlice.PatientPosition);
            meta.Add(nameof(DicomTag.ImageOrientationPatient), mostSuperiorSlice.ImageOrientationPatient.DicomJoin());
            meta.Add(nameof(DicomTag.ImagePositionPatient), mostSuperiorSlice.ImagePositionPatient.DicomJoin());
            // volume 은 LIP 좌표계를 사용하기때문에
            //  most inferiror slice z position = suprerior.z - {(number of slices - 1 ) * res.z} 으로 구할 수 있을 것.

            // slice 외의 정보
            AddDicomMeta(meta, ds, nameof(DicomTag.SeriesInstanceUID), DicomTag.SeriesInstanceUID);
            AddDicomMeta(meta, ds, nameof(DicomTag.Modality), DicomTag.Modality);
            AddDicomMeta(meta, ds, nameof(DicomTag.StudyID), DicomTag.StudyID);
            AddDicomMeta(meta, ds, nameof(DicomTag.SeriesNumber), DicomTag.SeriesNumber);
            AddDicomMeta(meta, ds, nameof(DicomTag.PatientID), DicomTag.PatientID);
            AddDicomMeta(meta, ds, nameof(DicomTag.PatientName), DicomTag.PatientName);

            return meta;
        }

        private static bool AddDicomMeta(Dictionary<string, string> src, DicomDataset ds, string key, DicomTag tag)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (ds == null) throw new ArgumentNullException(nameof(ds));
            if (tag == null) throw new ArgumentNullException(nameof(tag));

            if (ds.Contains(tag) == false) // GetValue 시에 key 가 없으면 FellowOakDicom.DicomDataException 발생하는 문제를 피하기 위함
                return false;
            src.Add(key, ds.GetValue<string>(tag, 0));
            return true;
        }

        private static bool IsSupportedSlice(DicomDataset ds)
        {
            if (DicomHelper.IsCt(ds.GetSingleValueOrDefault(DicomTag.Modality, String.Empty)) == false)
                return false; // MR 의 경우 HU base 의 volume 을 사용하지 않기 때문에, 이를 지원하려면 volume 또한 변경되어야할 수도..
            if (DicomHelper.IsLocalizer(GetValuesOrNull<string>(ds, DicomTag.ImageType)))
                return false; // https://www.dclunie.com/medical-image-faq/html/part2.html#DICOMLocalizers
            if (DicomHelper.IsAxial(GetValuesOrNull<float>(ds, DicomTag.ImageOrientationPatient)) == false)
                return false; // axial 이 아닌 source 를 이용하기 위해서는 기본 방향정보도 포함할 필요.
            if (DicomHelper.HasImagePositionPatient(GetValuesOrNull<float>(ds, DicomTag.ImagePositionPatient)) == false)
                return false;
            return true;
        }

        private static T[]? GetValuesOrNull<T>(DicomDataset ds, DicomTag tag)
        {
            if (ds.Contains(tag) == false) return null;
            return ds.GetValues<T>(tag);
        }

        private static short[] LoadImageArray(DicomDataset original)
        { // DICOM image format : https://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.303.6666&rep=rep1&type=pdf
            // 압축되어있는 경우등, byte 원본을 pixel 로 바로 사용할 수 없는 경우 처리
            var ds = original.InternalTransferSyntax == DicomTransferSyntax.ExplicitVRLittleEndian
                ? original
                : original.Clone(DicomTransferSyntax.ExplicitVRLittleEndian); // 이용가능한 형태로 변환

            var pixelData = DicomPixelData.Create(ds); // https://stackoverflow.com/a/52297176
            var factory = PixelDataFactory.Create(pixelData, 0);
            if (factory is GrayscalePixelDataU16 us)
            {
                //return us.Data.Cast<short>().ToArray(); // unity 에서 ArrayTypeMismatchException 발생
                return (short[])(object)us.Data;
            }
            else if (factory is GrayscalePixelDataS16 s)
            {
                return s.Data;
            }

            throw new NotSupportedException($"not supported image source : {factory.GetType()} / {ds.InternalTransferSyntax}");
        }

        private static short[] CreateLPIVolume(List<DicomSlice> slices, 
            out int dimX, out int dimY, out int dimZ,
            out short minRange, out short maxRange, 
            out float zLength)
        { // https://bitbucket.org/alkee_skia/mars-processor/src/51ef2a37e03c804e09618461bcb798d65bba6fac/Assets/Core/Scripts/DicomManager.cs#lines-226
            // 현재는 CT 데이터로 고정
            minRange = DicomHelper.CT_HU_MIN;
            maxRange = DicomHelper.CT_HU_MAX;

            var first = slices.First();
            dimX = first.Columns;
            dimY = first.Rows;
            dimZ = slices.Count;

            var src = new short[dimX * dimY * dimZ];

            // https://bitbucket.org/alkee_skia/mars-processor/issues/505/ct-dicom-meta
            // image orientation patient z 값을 내림차순으로 정렬하면 Inferior 방향으로 slice를 얻을 수 있는 것을 확인. (Axial만 가능)
            slices.Sort((a, b) => b.ImagePositionPatient[2].CompareTo(a.ImagePositionPatient[2])); // Dicom Data 검증 과정에서 크기가 3인지 이미 확인.
            zLength = slices.First().ImagePositionPatient[2] - slices.Last().ImagePositionPatient[2];
            if (zLength <= 0) // https://bitbucket.org/alkee_skia/mars3/issues/677/dicom#comment-61275878
                throw new ApplicationException($"invalid volume thickness({zLength})");
            var orientation = DicomHelper.DigitizedOrientation(first.ImageOrientationPatient).ToArray(); // Derived된 데이터도 임시로 사용할 수 있도록 (0, 1, -1) 중 하나의 값으로 조정해 주기 위한 과정.

            int xDirection = orientation[0]; // Dicom Data 검증 과정에서 크기가 6인지 이미 확인.
            int yDirection = orientation[4];

            for (var k = 0; k < dimZ; k++)
            {
                var s = slices[k];

                for (var i = 0; i < dimX; i++)
                    for (var j = 0; j < dimY; j++)
                    {
                        int pixelIndex = (j * dimX) + i;
                        float hounsfieldValue = s.Pixels[pixelIndex] * s.RescaleSlope + s.RescaleIntercept; // https://blog.kitware.com/dicom-rescale-intercept-rescale-slope-and-itk
                        short value = Math.Max(Math.Min((short)hounsfieldValue, DicomHelper.CT_HU_MAX), DicomHelper.CT_HU_MIN);

                        var vi = xDirection < 0 ? (dimX - 1) - i : i;
                        var vj = yDirection < 0 ? (dimY - 1) - j : j;

                        int voxelIndex = (k * dimX * dimY) + (vj * dimX) + vi;
                        src[voxelIndex] = value;
                    }
            }

            return src;
        }

        private class DicomSlice
        {
            public short[] Pixels { get; }
            public int Rows { get; }
            public int Columns { get; }
            public float[] ImageOrientationPatient { get; }
            public float[] ImagePositionPatient { get; }
            public string PatientPosition { get; } = "HFS";
            public float RescaleIntercept { get; } = 0.0f;
            public float RescaleSlope { get; } = 0.0f;
            public (float x, float y) PixelSpacing { get; } = (0.0f, 0.0f);
            public float SliceThickness { get; } = 0.0f;
            public DicomDataset Source { get; }

            public DicomSlice(DicomDataset ds)
            {
                Pixels = LoadImageArray(ds);
                Rows = ds.GetSingleValue<int>(DicomTag.Rows);
                Columns = ds.GetSingleValue<int>(DicomTag.Columns);
                PatientPosition = ds.GetString(DicomTag.PatientPosition);
                ImageOrientationPatient = ds.GetValues<float>(DicomTag.ImageOrientationPatient);
                ImagePositionPatient = ds.GetValues<float>(DicomTag.ImagePositionPatient);
                RescaleSlope = ds.GetSingleValue<float>(DicomTag.RescaleSlope);
                RescaleIntercept = ds.GetSingleValue<float>(DicomTag.RescaleIntercept);
                PixelSpacing = (ds.GetValue<float>(DicomTag.PixelSpacing, 0), ds.GetValue<float>(DicomTag.PixelSpacing, 1));
                SliceThickness = ds.GetSingleValue<float>(DicomTag.SliceThickness);

                Source = ds;
            }
        }
    }
}

#nullable restore

#endif