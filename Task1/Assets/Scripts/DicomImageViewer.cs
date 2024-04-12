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

        public async void SetupImageAndSlider(string seriesId, string url)
        {
            // cache 있으면 쓰고 없으면 cache 만들기
            string file = await GetNrrdCache(seriesId, url);
            await LoadNrrdData(file);
            volumeSlider.maxValue = slices - 1;
            ShowSlice();
        }

        public void ShowSlice()
        {
            Texture2D volumeTexture = new(width, height);
            volumeTexture.SetPixels(slicedColors[(int)volumeSlider.value]);
            volumeTexture.Apply();
            volumeImage.texture = volumeTexture;
        }

        void Start()
        {
            volumeSlider.onValueChanged.AddListener(delegate { ShowSlice(); });
        }

        public async Task<string> GetNrrdCache(string seriesId, string volumeURL)
        {
            string volumeFile = Application.persistentDataPath + "/" + seriesId + ".nrrd";
            if (!File.Exists(volumeFile)) await DownloadNrrdFile(volumeURL, volumeFile);
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

        async Task DownloadNrrdFile(string volumeURL, string volumeFile)
        {
            UnityWebRequest reqVolume = UnityWebRequest.Get(volumeURL);
            var sendRequest = reqVolume.SendWebRequest();

            while (!sendRequest.isDone)
            {
                await Task.Yield();
            }

            if (reqVolume.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(reqVolume.error);
            }

            else
            {
                File.WriteAllBytes(volumeFile, reqVolume.downloadHandler.data);
            }
        }
    }
}