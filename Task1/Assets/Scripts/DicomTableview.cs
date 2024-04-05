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
using SeriesImageViewer;

public class DicomTableView : MonoBehaviour
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

    private DicomImageViewer imageViewer;

    private const string dicomURL = "http://10.10.20.173:5080/v2/Dicom/";
    private const string dicomVolumeURL = "http://10.10.20.173:5080/dicom/";
    private readonly Dictionary<string, GameObject> dicomStudyIdRowContents = new();
    private readonly Dictionary<GameObject, string> dicomStudyRowContentsId = new();
    private readonly Dictionary<Button, List<string>> dicomSeriesButtonsData = new();

    void OnClickStudyRow()
    {
        GameObject studyRowContent = EventSystem.current.currentSelectedGameObject.transform.parent.gameObject;
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
        imageViewer.ResetImageAndSlider();
        Button seriesDataButton = EventSystem.current.currentSelectedGameObject.GetComponent<Button>();
        string seriesId = dicomSeriesButtonsData[seriesDataButton][0];
        string volumePath = dicomSeriesButtonsData[seriesDataButton][1];
        string volumeFile = Application.persistentDataPath + "/" + seriesId + ".nrrd";
        if (!File.Exists(volumeFile)) StartCoroutine(imageViewer.GetVolumeData(seriesId, volumeFile, volumePath, dicomVolumeURL));
        else imageViewer.ShowVolumeImage(volumeFile);
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
        // study data가 활성화되면 series 데이터와 volume image 는 비활성화되어 화면이 겹치는 것 방지
        seriesScrollview.gameObject.SetActive(false);
        searchContent.SetActive(true);
        imageViewer.SetVolumeVisible(false);
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
        resetButton.onClick.AddListener(OnClickReset);
        imageViewer = FindObjectOfType<DicomImageViewer>();
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
}