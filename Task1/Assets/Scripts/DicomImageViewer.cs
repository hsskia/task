using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Mars;
using System.Linq;
using UnityEngine.Networking;
using System.IO;
using System.Threading.Tasks;
using System.Collections;

namespace SeriesImageViewer
{
    public class DicomImageViewer : MonoBehaviour
    {
        [SerializeField] private GameObject volumePopUp;
        [SerializeField] private Slider axialSlider;
        [SerializeField] private Slider coronalSlider;
        [SerializeField] private Slider sagittalSlider;
        [SerializeField] private RawImage axialImage;
        [SerializeField] private RawImage coronalImage;
        [SerializeField] private RawImage sagittalImage;
        [SerializeField] private Button exitButton;

        private int width;
        private int height;
        private int slices;
        private readonly List<Color[]> axialColors = new();
        private readonly List<Color[]> coronalColors = new();
        private readonly List<Color[]> sagittalColors = new();

        void OnClickVolumeExit()
        {
            Destroy(volumePopUp);
        }

        public async void SetupImageAndSlider(string seriesId, string url)
        {
            // cache 있으면 쓰고 없으면 cache 만들기
            string file = await GetNrrdCache(seriesId, url);
            await LoadNrrdData(file);
            SliderInitialSetup();
            ShowImages();
        }

        void SliderInitialSetup()
        {
            axialSlider.maxValue = slices - 1;
            coronalSlider.maxValue = slices - 1;
            sagittalSlider.maxValue = slices - 1;
        }

        public void ShowImages()
        {
            ShowSlice("axial", axialImage);
            ShowSlice("coronal", coronalImage);
            ShowSlice("sagittal", sagittalImage);
        }

        public void ShowSlice(string plane, RawImage volumeImage)
        {
            Texture2D volumeTexture = new(width, height);

            switch (plane)
            {
                case "axial":
                    volumeTexture.SetPixels(axialColors[(int)axialSlider.value]);
                    break;

                case "coronal":
                    volumeTexture.SetPixels(coronalColors[(int)coronalSlider.value]);
                    break;

                case "sagittal":
                    volumeTexture.SetPixels(sagittalColors[(int)sagittalSlider.value]);
                    break;
            }
            volumeTexture.Apply();
            volumeImage.texture = volumeTexture;
        }

        void Start()
        {
            axialSlider.onValueChanged.AddListener((value) => ShowSlice("axial", axialImage));
            coronalSlider.onValueChanged.AddListener((value) => ShowSlice("coronal", coronalImage));
            sagittalSlider.onValueChanged.AddListener((value) => ShowSlice("sagittal", sagittalImage));
            exitButton.onClick.AddListener(delegate { OnClickVolumeExit(); });
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
                axialColors.Add(slicedImageColor);
                coronalColors.Add(slicedImageColor);
                sagittalColors.Add(slicedImageColor);
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