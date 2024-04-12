using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Mars;
using System.Linq;
using UnityEngine.Networking;
using System.IO;
using System.Threading.Tasks;

namespace SeriesImageViewer
{
    public class DicomImageViewer : MonoBehaviour
    {
        [SerializeField] private RawImage volumeImage;
        [SerializeField] private Slider volumeSlider;

        private int width;
        private int height;
        private int slices;
        private readonly List<Color[]> slicedColors = new();

        public async void Setup(string seriesId, string url)
        {
            // cache 있으면 쓰고 없으면 cache 만들기
            string file = await GetNrrdCache(seriesId, url);
            if (file != "")
            {
                await LoadNrrdData(file);
                ShowSlice();
            }
        }

        void Start()
        {
            volumeSlider.onValueChanged.AddListener(delegate { ApplyVolumeImage(); });
        }

        void ApplyVolumeImage()
        {
            Texture2D volumeTexture = new(width, height);
            volumeTexture.SetPixels(slicedColors[(int)volumeSlider.value]);
            volumeTexture.Apply();
            volumeImage.texture = volumeTexture;
        }

        public void ShowSlice()
        {
            // slider 의 시작 지점이 항상 index 0 이 되도록 설정
            volumeSlider.value = 0;
            volumeSlider.maxValue = slices - 1;
            ApplyVolumeImage();
        }

        public async Task<string> GetNrrdCache(string seriesId, string volumeURL)
        {
            volumeImage.texture = null;
            string volumeFile = Application.persistentDataPath + "/" + seriesId + ".nrrd";

            if (!File.Exists(volumeFile)) volumeFile = await DownloadNrrdFile(seriesId, volumeURL, volumeFile);
            return volumeFile;
        }

        async Task LoadNrrdData(string volumeFile)
        {
            NrrdRaw nrrdData = await NrrdRaw.LoadAsync(volumeFile);
            NrrdHeader nrrdHeader = nrrdData.Header;
            width = nrrdHeader.sizes[0];
            height = nrrdHeader.sizes[1];
            slices = nrrdHeader.sizes[2];

            short[] imageArray = nrrdData.Int16Data.ToArray();
            short minValue = imageArray.Min();
            short maxValue = imageArray.Max();
            float[] normalizedArray = imageArray.Select(value => (value - minValue) / (float)(maxValue - minValue)).ToArray();

            for (int i = 0; i < slices; i++)
            {
                float[] slicedImage = normalizedArray.Skip(width * height * i).Take(width * height).ToArray();
                Color[] slicedImageColor = slicedImage.Select(x => new Color(x, x, x)).ToArray();
                slicedColors.Add(slicedImageColor);
            }
        }

        async Task<string> DownloadNrrdFile(string seriesId, string volumeURL, string volumeFile)
        {
            UnityWebRequest reqVolume = UnityWebRequest.Get(volumeURL);
            var sendRequest = reqVolume.SendWebRequest();

            while (!sendRequest.isDone)
            {
                await Task.Yield();
            }

            if (reqVolume.result != UnityWebRequest.Result.Success)
            {
                Debug.Log($"Dicom Series ID {seriesId} 의 Volume 파일은 존재하지 않습니다.");
                volumeSlider.maxValue = 0;
                volumeFile = "";
            }

            else
            {
                File.WriteAllBytes(volumeFile, reqVolume.downloadHandler.data);
            }
            return volumeFile;
        }
    }
}