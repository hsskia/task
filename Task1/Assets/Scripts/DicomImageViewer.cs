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
        float[,,] volume3DArray;
        private readonly List<Color[]> axialColors = new();
        private readonly List<Color[]> coronalColors = new();
        private readonly List<Color[]> sagittalColors = new();
        private const string AXIAL = "axial";
        private const string CORONAL = "coronal";
        private const string SAGITTAL = "sagittal";


        void OnClickVolumeExit()
        {
            Destroy(volumePopUp);
        }

        public async void SetupImageAndSlider(string seriesId, string url)
        {
            // cache 있으면 쓰고 없으면 cache 만들기
            string file = await GetNrrdCache(seriesId, url);
            float[] volumeArray = await LoadNrrdData(file);
            Make3DArray(volumeArray);

            SliderInitialSetup();
            MakeAllPlanesColors();
            ShowImages();
        }

        void SliderInitialSetup()
        {
            axialSlider.maxValue = slices - 1;
            coronalSlider.maxValue = width - 1;
            sagittalSlider.maxValue = height - 1;
        }

        public void ShowImages()
        {
            ApplyTexture(AXIAL);
            ApplyTexture(CORONAL);
            ApplyTexture(SAGITTAL);
        }

        public void ApplyTexture(string plane)
        {
            switch (plane)
            {
                case AXIAL:
                    Texture2D axialTexture = new(width, height);
                    axialTexture.SetPixels(axialColors[(int)axialSlider.value]);
                    axialTexture.Apply();
                    axialImage.texture = axialTexture;
                    break;

                case CORONAL:
                    Texture2D coronalTexture = new(height, slices);
                    coronalTexture.SetPixels(coronalColors[(int)coronalSlider.value]);
                    coronalTexture.Apply();
                    coronalImage.texture = coronalTexture;
                    break;

                case SAGITTAL:
                    Texture2D sigittalTexture = new(width, slices);
                    sigittalTexture.SetPixels(sagittalColors[(int)sagittalSlider.value]);
                    sigittalTexture.Apply();
                    sagittalImage.texture = sigittalTexture;
                    break;
            }
        }

        void Start()
        {
            axialSlider.onValueChanged.AddListener((value) => ApplyTexture(AXIAL));
            coronalSlider.onValueChanged.AddListener((value) => ApplyTexture(CORONAL));
            sagittalSlider.onValueChanged.AddListener((value) => ApplyTexture(SAGITTAL));
            exitButton.onClick.AddListener(delegate { OnClickVolumeExit(); });
        }

        public async Task<string> GetNrrdCache(string seriesId, string volumeURL)
        {
            string volumeFile = Application.persistentDataPath + "/" + seriesId + ".nrrd";
            if (!File.Exists(volumeFile)) await DownloadNrrdFile(volumeURL, volumeFile);
            return volumeFile;
        }

        async Task<float[]> LoadNrrdData(string volumeFile)
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
            return normalizedArray;
        }

        void Make3DArray(float[] normalizedArray)
        {
            int index = 0;
            volume3DArray = new float[width, height, slices];
            for (int z = 0; z < slices; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        volume3DArray[x, y, z] = normalizedArray[index++];
                    }
                }
            }
        }

        void MakeAllPlanesColors()
        {
            MakeAxialColors();
            MakeCoronalColors();
            MakeSagittalColors();
        }

        void MakeAxialColors()
        {
            for (int z = 0; z < slices; z++)
            {
                Color[] axialSliceColor = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float pixelValue = volume3DArray[x, y, z];
                        axialSliceColor[y * width + x] = new Color(pixelValue, pixelValue, pixelValue);
                    }
                }
                axialColors.Add(axialSliceColor);
            }
        }

        void MakeCoronalColors()
        {
            for (int y = 0; y < height; y++)
            {
                Color[] coronalSliceColor = new Color[slices * width];
                for (int z = 0; z < slices; z++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float pixelValue = volume3DArray[x, y, z];
                        coronalSliceColor[z * width + x] = new Color(pixelValue, pixelValue, pixelValue);
                    }
                }
                coronalColors.Add(coronalSliceColor);
            }
        }

        void MakeSagittalColors()
        {
            for (int x = 0; x < width; x++)
            {
                Color[] sagittalSliceColor = new Color[slices * height];
                for (int z = 0; z < slices; z++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        float pixelValue = volume3DArray[x, y, z];
                        sagittalSliceColor[z * height + y] = new Color(pixelValue, pixelValue, pixelValue);
                    }
                }
                sagittalColors.Add(sagittalSliceColor);
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