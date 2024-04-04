using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Mars.db;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.EventSystems;
using StudyRow;
using Newtonsoft.Json;
using System.IO;
using Mars;
using System.Linq;
using UnityEditor;

public class DicomImageViewer : MonoBehaviour
{
    [SerializeField] private ScrollRect studyScrollview;
    [SerializeField] private ScrollRect seriesScrollview;

    [SerializeField] private GameObject scrollviewContent;
    [SerializeField] private GameObject rowContent;
    [SerializeField] private GameObject seriesContent;
    [SerializeField] private GameObject searchContent;

    [SerializeField] private Button seriesButton;
    [SerializeField] private Button searchButton;
    [SerializeField] private Button resetButton;

    [SerializeField] private InputField searchInputField;
    [SerializeField] private Text searchText;

    [SerializeField] private RawImage volumeImage;
    [SerializeField] private Slider volumeSlider;

    private const string dicomURL = "http://10.10.20.173:5080/v2/Dicom/";
    private const string dicomVolumeURL = "http://10.10.20.173:5080/dicom/";
    private readonly Dictionary<string, GameObject> dicomStudyIdRowContents = new();
    private readonly Dictionary<GameObject, string> dicomStudyRowContentsId = new();
    private readonly Dictionary<Button, List<string>> dicomSeriesButtonsData = new();

    void OnClickStudyRow()
    {
        GameObject studyRowContent = EventSystem.current.currentSelectedGameObject.transform.parent.gameObject;

        // 클릭된 버튼의 부모 object (study data row) 를 key, 해당 row 의 studyId 를 value
        string studyId = dicomStudyRowContentsId[studyRowContent];
        RemoveSeriesObject();
        StartCoroutine(GetSeriesData(studyId));
        SetStudyInVisible(studyId);

    }

    void OnClickReset()
    {
        SetStudyVisible();
        RemoveSeriesObject();
    }


    void OnClickSearch(List<DicomStudy> dicomStudyList)
    {
        string keyword = searchText.text;
        foreach (DicomStudy dicomData in dicomStudyList)
        {
            // patientID 나 patientName 뿐만 아니라 전체 field 를 기준으로 keyword 찾기
            string dicomStudyString = JsonConvert.SerializeObject(dicomData);
            dicomStudyIdRowContents[dicomData.id.ToString()].SetActive(false);
            if (dicomStudyString.Contains(keyword)) dicomStudyIdRowContents[dicomData.id.ToString()].SetActive(true);

        }
    }

    void OnClickSeriesData()
    {
        volumeImage.texture = null;
        volumeImage.gameObject.SetActive(true);
        volumeSlider.gameObject.SetActive(true);
        Button seriesDataButton = EventSystem.current.currentSelectedGameObject.GetComponent<Button>();
        string seriesId = dicomSeriesButtonsData[seriesDataButton][0];
        string volumePath = dicomSeriesButtonsData[seriesDataButton][1];
        string volumeFile = Application.persistentDataPath + "/" + seriesId + ".nrrd";
        if (!File.Exists(volumeFile)) StartCoroutine(GetVolumeData(seriesId, volumeFile, volumePath));
        else ShowVolumeImage(volumeFile);
    }

    async void ShowVolumeImage(string volumeFile)
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

        Dictionary<int, Color[]> slicedArrayDict = new();
        for (int i = 0; i < slices; i++)
        {
            float[] slicedImageArray = normalizedArray.Skip(width * height * i).Take(width * height).ToArray();
            Color[] slicedColors = slicedImageArray.Select(x => new Color(x, x, x)).ToArray();
            slicedArrayDict[i] = slicedColors;
        }

        // slider 의 시작 지점이 항상 index 0 이 되도록 설정
        volumeSlider.value = 0;
        volumeSlider.maxValue = slices - 1; // slice 의 index 가 0부터 시작했기 때문에 -1

        Texture2D volumeTexture = new(width, height);
        volumeTexture.SetPixels(slicedArrayDict[0]);
        volumeTexture.Apply();
        volumeImage.texture = volumeTexture;

        // slider 에 새로운 데이터를 사용하기위해 초기화
        volumeSlider.onValueChanged.RemoveAllListeners();
        volumeSlider.onValueChanged.AddListener((value) => OnSliderValueChanged(value, volumeTexture, slicedArrayDict));
    }

    void OnSliderValueChanged(float sliderValue, Texture2D volumeTexture, Dictionary<int, Color[]> slicedColorDict)
    {
        volumeTexture.SetPixels(slicedColorDict[(int)sliderValue]);
        volumeTexture.Apply();
        volumeImage.texture = volumeTexture;
    }

    // 데이터의 크기만큼 화면이 출력되도록
    void ResetScrollview()
    {
        studyScrollview.gameObject.SetActive(false);
        studyScrollview.gameObject.SetActive(true);
    }

    void RemoveSeriesObject()
    {
        foreach (Button seriesRowButton in dicomSeriesButtonsData.Keys)
        {
            Destroy(seriesRowButton.gameObject);
        }
        dicomSeriesButtonsData.Clear();
    }

    void SetStudyVisible()
    {
        // study data가 활성화되면 series 데이터와 Volume 이미지는 비활성화되어 화면이 겹치는 것 방지
        seriesScrollview.gameObject.SetActive(false);
        searchContent.SetActive(true);
        volumeImage.gameObject.SetActive(false);
        volumeSlider.gameObject.SetActive(false);
        resetButton.gameObject.SetActive(false);

        foreach (string rowStudyId in dicomStudyIdRowContents.Keys)
        {
            dicomStudyIdRowContents[rowStudyId].SetActive(true);
        }
        ResetScrollview();
    }

    void SetStudyInVisible(string studyId)
    {
        seriesScrollview.gameObject.SetActive(true);
        seriesButton.gameObject.SetActive(true);
        searchContent.SetActive(false);
        resetButton.gameObject.SetActive(true);

        foreach (string rowStudyId in dicomStudyIdRowContents.Keys)
        {
            dicomStudyIdRowContents[rowStudyId].SetActive(false);
        }
        dicomStudyIdRowContents[studyId].SetActive(true);
        ResetScrollview();
    }

    void AddDicomSeriesRow(DicomSeries dicomSeries)
    {
        string seriesValue = "";

        foreach (var property in typeof(DicomSeries).GetProperties())
        {
            object val = property.GetValue(dicomSeries);
            seriesValue += $"{property.Name}: {val} \n";
        }

        Button seriesData = Instantiate(seriesButton, seriesContent.transform);
        seriesData.onClick.AddListener(OnClickSeriesData);
        seriesData.GetComponentInChildren<Text>().text = seriesValue;
        dicomSeriesButtonsData[seriesData] = new List<string> { dicomSeries.id.ToString(), dicomSeries.volumeFilePath };
    }

    void AddDicomStudyRow(DicomStudy dicomStudy)
    {
        GameObject newStudyRow = Instantiate(rowContent, scrollviewContent.transform);
        Button[] newStudyRowButtons = newStudyRow.GetComponentsInChildren<Button>();
        foreach (Button button in newStudyRowButtons) { button.onClick.AddListener(OnClickStudyRow); }

        DicomStudyRow dicomStudyRow = newStudyRow.GetComponent<DicomStudyRow>();
        dicomStudyIdRowContents[dicomStudy.id.ToString()] = newStudyRow;
        dicomStudyRowContentsId[newStudyRow] = dicomStudy.id.ToString();
        dicomStudyRow.SetStudyData(dicomStudy);
    }

    void Start()
    {
        StartCoroutine(GetStudyData());
    }

    IEnumerator GetStudyData()
    {
        UnityWebRequest reqStudy = UnityWebRequest.Get(dicomURL + "Study");
        yield return reqStudy.SendWebRequest();

        if (reqStudy.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(reqStudy.error);
        }
        else
        {
            List<DicomStudy> dicomStudyList = JsonConvert.DeserializeObject<List<DicomStudy>>(reqStudy.downloadHandler.text);
            searchInputField.textComponent = searchText;
            foreach (DicomStudy studyData in dicomStudyList)
            {
                AddDicomStudyRow(studyData);
            }
            searchButton.onClick.AddListener(() => OnClickSearch(dicomStudyList));
            resetButton.onClick.AddListener(OnClickReset);
            ResetScrollview();
        }
    }

    IEnumerator GetSeriesData(string studyId)
    {
        UnityWebRequest reqSeries = UnityWebRequest.Get(dicomURL + "Series?studyId=" + studyId);
        yield return reqSeries.SendWebRequest();

        if (reqSeries.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(reqSeries.error);
        }
        else
        {
            List<DicomSeries> dicomSeriesList = JsonConvert.DeserializeObject<List<DicomSeries>>(reqSeries.downloadHandler.text);
            seriesContent.SetActive(true);
            foreach (DicomSeries seriesData in dicomSeriesList)
            {
                AddDicomSeriesRow(seriesData);
            }
            seriesButton.gameObject.SetActive(false);
        }
    }

    IEnumerator GetVolumeData(string seriesId, string volumeFile, string volumePath)
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