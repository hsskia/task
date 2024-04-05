#if OPENCVSHARP // dependency

using OpenCvSharp; // https://github.com/shimat/opencvsharp
using System;

#nullable enable
namespace Mars
{

    public partial class Int16Volume
    {
        /// <summary>
        ///     int[ height, width ] 로 구성된 2차원 배열을 grayscale png 이미지화
        /// </summary>
        /// <param name="src">입력값</param>
        /// <param name="rescaleYratio">null 이면 원본 그대로. 주어진 비율로 height 만 rescale</param>
        /// <param name="compressionLevel">0(no compression) ~ 9(max compression) 사이의 png 압축 수준</param>
        /// <returns>png 형식으로 encoding 된 byte steam</returns>
        public static byte[] Export16bitGrayPng(int[/*y*/,/*x*/] src, float? rescaleYratio = null, int compressionLevel = 3)
        {
            if (compressionLevel < 0 || compressionLevel > 9) throw new ArgumentOutOfRangeException(nameof(compressionLevel));

            var height = src.GetLength(0);
            var width = src.GetLength(1);

            using var mat = new Mat(height, width, MatType.CV_32SC1/*int*/, src);
            using var img = mat.Normalize(0, ushort.MaxValue, NormTypes.MinMax, MatType.CV_16UC1);
            if (rescaleYratio.HasValue)
            {
                height = (int)(height * rescaleYratio.Value);
                return img
                    .Resize(new Size(width, height), interpolation: InterpolationFlags.Nearest)
                    .ImEncode(".png", new ImageEncodingParam(ImwriteFlags.PngCompression, compressionLevel));
            }
            return img.ImEncode(".png", new ImageEncodingParam(ImwriteFlags.PngCompression, compressionLevel));
        }

        public static byte[] Export16bitGrayPng(int[/*y*/,/*x*/] src, (float x, float y, float z) resolution, HeightDirection dir = HeightDirection.ANTERIOR, int compressionLevel = 3)
        {
            var res = TransformByDirection(dir, resolution);
            float yRatio = Math.Abs(res.y / res.x);
            return Export16bitGrayPng(src, yRatio, compressionLevel);
        }

        /// <summary>
        ///     axial volume 내 하나의 slice(z index) 를 png 형식으로 추출
        /// </summary>
        /// <param name="z">slice index</param>
        /// <param name="mask">null 인경우 mask 를 사용하지 않음. mask 는 0(alpha 0) 또는 1(alpha 1) 로 구성되어있어야 함</param>
        /// <param name="bytePerChannel">false 인 경우 16 bit gray scale image, true 인경우 8 bit</param>
        /// <param name="compressionLevel">0(no compression) ~ 9(max compression) 사이의 png 압축 수준</param>
        /// <returns>png 형식으로 encoding 된 byte steam</returns>
        public byte[] ExportPng(int z, Int16Volume? mask = null, bool bytePerChannel = true, int compressionLevel = 3)
        { // opencvsharp4 를 사용하므로 docker 는 `shimat/ubuntu18-dotnetcore3.1-opencv4.5.0:20201030` 이미지로부터 빌드되어야 한다.
            if (z < 0 || z >= Dimension.z) throw new ArgumentException("out of range", nameof(z));
            if (mask != null && Dimension != mask.Dimension)
                throw new ArgumentException("mismatched mask size", nameof(mask));

            // 64 bit image(16x4)의 경우 용량이 꽤 크기때문에 압축(값이 1이면 512x512 가 1M 정도)
            if (compressionLevel < 0 || compressionLevel > 9) throw new ArgumentOutOfRangeException(nameof(compressionLevel));

            var width = Dimension.x;
            var height = Dimension.y;

            var imgSize = width * height;
            var offset = imgSize * z;

            var srcBuffer = new short[imgSize]; // image slice
            Array.Copy(Data, offset, srcBuffer, 0, imgSize);

            using var src = new Mat(height, width, MatType.CV_16SC1, srcBuffer);
            // 단순히 0~max 로 NormTypes.MinMax 로 normalize 하는 경우 image 별로 다른 밝기로 나타나는 문제가 있다(이미지 내 HU 의 min max 차이 때문)
            var alpha = 1.0 / (DataRange.max - DataRange.min);
            var beta = -(double)DataRange.min / (DataRange.max - DataRange.min);
            alpha *= bytePerChannel ? byte.MaxValue : ushort.MaxValue;
            beta *= bytePerChannel ? byte.MaxValue : ushort.MaxValue;
            src.ConvertTo(src, bytePerChannel ? MatType.CV_8UC1 : MatType.CV_16UC1, alpha, beta);
            if (mask == null) return src.ImEncode(".png", new ImageEncodingParam(ImwriteFlags.PngCompression, compressionLevel));

            // masking process
            var maskBuffer = new short[imgSize]; // mask slice
            Array.Copy(mask.Data, offset, maskBuffer, 0, imgSize);
            using var maskMat = new Mat(height, width, bytePerChannel ? MatType.CV_16SC1 : MatType.CV_16UC1, maskBuffer);
            using var maskImg = maskMat.Normalize(0, bytePerChannel ? byte.MaxValue : ushort.MaxValue, NormTypes.MinMax
                , bytePerChannel ? MatType.CV_8UC1 : MatType.CV_16UC1);

            using var output = new Mat(height, width, bytePerChannel ? MatType.CV_8UC4 : MatType.CV_16UC4); // BGRA
            Cv2.MixChannels(new Mat[] { src, maskImg }, new Mat[] { output }, new int[] { // ref: https://aerocode.net/289
                0, 0, // B (img[0] to output[0])
                0, 1, // G (img[0] to output[1])
                0, 2, // R (img[0] to output[2])
                1, 3, // A (maskImg[0] to output[3])
            });

            return output.ImEncode(".png", new ImageEncodingParam(ImwriteFlags.PngCompression, compressionLevel));
        }
    }
}
#nullable restore
#endif