using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Mars.db;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using StudyRow;
using Newtonsoft.Json;
using SeriesImageViewer;
using System;
using System.Threading.Tasks;

public class DicomTableView : MonoBehaviour
{
    [SerializeField] private ScrollRect studyScrollview;
    [SerializeField] private ScrollRect seriesScrollview;

    [SerializeField] private GameObject canvas;
    [SerializeField] private GameObject scrollviewContent;
    [SerializeField] private GameObject rowContent;
    [SerializeField] private GameObject seriesContent;
    [SerializeField] private GameObject searchContent;
    [SerializeField] private GameObject volumeContent;

    [SerializeField] private Button seriesButton;
    [SerializeField] private Button searchButton;
    [SerializeField] private Button resetButton;

    [SerializeField] private InputField searchInputField;
    [SerializeField] private Text searchText;

    private GameObject newVolumeContent;

    private const string dicomURLBase = "http://10.10.20.173:5080/v2/Dicom/";
    private const string dicomVolumeURLBase = "http://10.10.20.173:5080/dicom/";
    private List<DicomStudy> dicomStudyList = new();
    private List<DicomSeries> dicomSeriesList = new();
    private readonly Dictionary<string, GameObject> dicomStudyIdObjects = new();
    private readonly Dictionary<GameObject, string> dicomStudyObjectsId = new();
    private readonly Dictionary<Button, Tuple<string, string>> dicomSeriesButtonsData = new();

    async void Start()
    {
        await LoadStudyData();
        MakeDicomStudyTable(dicomStudyList);
        Setup();
    }

    void Setup()
    {
        searchInputField.textComponent = searchText;
        searchButton.onClick.AddListener(() => OnClickSearch());
        resetButton.onClick.AddListener(OnClickReset);
    }

    async void OnClickStudyData()
    {
        GameObject studyRowContent = EventSystem.current.currentSelectedGameObject.transform.parent.gameObject;
        string studyId = dicomStudyObjectsId[studyRowContent];
        RemoveSeriesObjects();
        await LoadSeriesData(studyId);
        MakeDicomSeriesTexts();
        SetObjectsVisible(false, studyId);
    }

    void OnClickReset()
    {
        RemoveSeriesObjects();
        SetObjectsVisible(true);
    }

    void OnClickSearch()
    {
        string keyword = searchText.text;
        foreach (DicomStudy dicomData in dicomStudyList)
        {
            // patientID 나 patientName 뿐만 아니라 전체 field 를 기준으로 keyword 찾기
            string dicomStudyString = JsonConvert.SerializeObject(dicomData);
            dicomStudyIdObjects[dicomData.id.ToString()].SetActive(false);
            if (dicomStudyString.Contains(keyword)) dicomStudyIdObjects[dicomData.id.ToString()].SetActive(true);
        }
    }

    void OnClickSeriesData()
    {
        Button seriesDataButton = EventSystem.current.currentSelectedGameObject.GetComponent<Button>();
        string seriesId = dicomSeriesButtonsData[seriesDataButton].Item1;
        string volumeFilePath = dicomSeriesButtonsData[seriesDataButton].Item2;
        if (string.IsNullOrEmpty(volumeFilePath))
        {
            Debug.Log($"Dicom Series ID {seriesId} 의 Volume 파일은 존재하지 않습니다.");
            return;
        }
        string volumeURL = dicomVolumeURLBase + volumeFilePath;
        Destroy(newVolumeContent);
        newVolumeContent = Instantiate(volumeContent, canvas.transform);
        DicomImageViewer imageViewer = newVolumeContent.GetComponent<DicomImageViewer>();
        imageViewer.SetupImageAndSlider(seriesId, volumeURL);
    }

    // 데이터의 크기만큼 화면이 출력되도록
    void ResetScrollview()
    {
        studyScrollview.gameObject.SetActive(false);
        studyScrollview.gameObject.SetActive(true);
    }

    void RemoveSeriesObjects()
    {
        foreach (Button seriesRowButton in dicomSeriesButtonsData.Keys)
        {
            Destroy(seriesRowButton.gameObject);
        }
        Destroy(newVolumeContent);
        dicomSeriesButtonsData.Clear();
    }

    void SetObjectsVisible(bool studyVisible, string studyId = "")
    {
        searchContent.SetActive(studyVisible);
        seriesScrollview.gameObject.SetActive(!studyVisible);
        foreach (string rowStudyId in dicomStudyIdObjects.Keys) dicomStudyIdObjects[rowStudyId].SetActive(studyVisible);
        if (!studyVisible) dicomStudyIdObjects[studyId].SetActive(true);
        ResetScrollview();
    }

    void AddDicomStudyRow(DicomStudy dicomStudy)
    {
        GameObject newStudyRow = CopyDicomStudyPrefab();
        DicomStudyRow dicomStudyRow = newStudyRow.GetComponent<DicomStudyRow>();
        dicomStudyIdObjects[dicomStudy.id.ToString()] = newStudyRow;
        dicomStudyObjectsId[newStudyRow] = dicomStudy.id.ToString();
        dicomStudyRow.SetStudyData(dicomStudy);
    }

    void AddDicomSeriesText(DicomSeries dicomSeries)
    {
        string seriesValue = "";

        foreach (var property in typeof(DicomSeries).GetProperties())
        {
            object val = property.GetValue(dicomSeries);
            seriesValue += $"{property.Name}: {val} \n";
        }
        Button newSeriesButton = CopyDicomSeriesPrefab();
        dicomSeriesButtonsData[newSeriesButton] = new Tuple<string, string>(dicomSeries.id.ToString(), dicomSeries.volumeFilePath);
        newSeriesButton.GetComponentInChildren<Text>().text = seriesValue;
    }

    void MakeDicomStudyTable(List<DicomStudy> dicomStudyList)
    {
        foreach (DicomStudy studyData in dicomStudyList)
        {
            AddDicomStudyRow(studyData);
        }
        ResetScrollview();
    }

    void MakeDicomSeriesTexts()
    {
        foreach (DicomSeries seriesData in dicomSeriesList)
        {
            AddDicomSeriesText(seriesData);
        }
    }

    GameObject CopyDicomStudyPrefab()
    {
        GameObject newStudyRow = Instantiate(rowContent, scrollviewContent.transform);
        Button[] newStudyRowButtons = newStudyRow.GetComponentsInChildren<Button>();
        foreach (Button button in newStudyRowButtons) { button.onClick.AddListener(OnClickStudyData); }
        return newStudyRow;
    }

    Button CopyDicomSeriesPrefab()
    {
        Button newSeriesButton = Instantiate(seriesButton, seriesContent.transform);
        newSeriesButton.onClick.AddListener(OnClickSeriesData);
        return newSeriesButton;
    }

    async Task LoadStudyData()
    {
        UnityWebRequest reqStudy = UnityWebRequest.Get(dicomURLBase + "Study");
        var sendRequest = reqStudy.SendWebRequest();
        while (!sendRequest.isDone)
        {
            await Task.Yield();
        }

        if (reqStudy.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(reqStudy.error);
        }
        dicomStudyList = JsonConvert.DeserializeObject<List<DicomStudy>>(reqStudy.downloadHandler.text);
    }

    async Task LoadSeriesData(string studyId)
    {
        UnityWebRequest reqSeries = UnityWebRequest.Get(dicomURLBase + "Series?studyId=" + studyId);
        var sendRequest = reqSeries.SendWebRequest();
        while (!sendRequest.isDone)
        {
            await Task.Yield();
        }

        if (reqSeries.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(reqSeries.error);
        }
        dicomSeriesList = JsonConvert.DeserializeObject<List<DicomSeries>>(reqSeries.downloadHandler.text);
    }
}