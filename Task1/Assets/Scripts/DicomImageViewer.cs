using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Mars;
using System.Linq;
using UnityEngine.Networking;
using System.IO;
using System.Collections;

namespace SeriesImageViewer
{
    public class DicomImageViewer : MonoBehaviour
    {
        [SerializeField] private RawImage volumeImage;
        [SerializeField] private Slider volumeSlider;

        void OnSliderValueChanged(float sliderValue, Texture2D volumeTexture, Dictionary<int, Color[]> slicedColors)
        {
            volumeTexture.SetPixels(slicedColors[(int)sliderValue]);
            volumeTexture.Apply();
            volumeImage.texture = volumeTexture;
        }

        public void ResetImageAndSlider()
        {
            volumeImage.texture = null;
            SetVolumeVisible(true);
        }

        public void SetVolumeVisible(bool volumeVisible)
        {
            volumeImage.gameObject.SetActive(volumeVisible);
            volumeSlider.gameObject.SetActive(volumeVisible);
        }

        public async void ShowVolumeImage(string volumeFile)
        {
            NrrdRaw nrrdData = await NrrdRaw.LoadAsync(volumeFile);
            NrrdHeader nrrdHeader = nrrdData.Header;
            int width = nrrdHeader.sizes[0];
            int height = nrrdHeader.sizes[1];
            int slices = nrrdHeader.sizes[2];

            short[] imageArray = nrrdData.Int16Data.ToArray();
            short minValue = imageArray.Min();
            short maxValue = imageArray.Max();
            float[] normalizedArray = imageArray.Select(value => (value - minValue) / (float)(maxValue - minValue)).ToArray();

            Dictionary<int, Color[]> slicedColors = new();
            for (int i = 0; i < slices; i++)
            {
                float[] slicedImage = normalizedArray.Skip(width * height * i).Take(width * height).ToArray();
                Color[] slicedImageColor = slicedImage.Select(x => new Color(x, x, x)).ToArray();
                slicedColors[i] = slicedImageColor;
            }

            // slider 의 시작 지점이 항상 index 0 이 되도록 설정
            volumeSlider.value = 0;
            volumeSlider.maxValue = slices - 1;

            Texture2D volumeTexture = new(width, height);
            volumeTexture.SetPixels(slicedColors[0]);
            volumeTexture.Apply();
            volumeImage.texture = volumeTexture;

            // slider 에 새로운 데이터를 사용하기위해 초기화
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.onValueChanged.AddListener((value) => OnSliderValueChanged(value, volumeTexture, slicedColors));
        }

        public IEnumerator GetVolumeData(string seriesId, string volumeFile, string volumePath, string dicomVolumeURL)
        {
            string volumeURLPath = dicomVolumeURL + volumePath;

            UnityWebRequest reqVolume = UnityWebRequest.Get(volumeURLPath);
            yield return reqVolume.SendWebRequest();

            if (reqVolume.result != UnityWebRequest.Result.Success)
            {
                Debug.Log($"Dicom Series ID {seriesId} 의 Volume 파일은 존재하지 않습니다.");
                volumeSlider.maxValue = 0;
            }
            else
            {
                File.WriteAllBytes(volumeFile, reqVolume.downloadHandler.data);
                ShowVolumeImage(volumeFile);
            }
        }
    }
}