using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Mars.db;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.EventSystems;
using StudyRow;
using Newtonsoft.Json;

public class DicomTableview : MonoBehaviour
{

    [SerializeField] private ScrollRect studyScrollview;
    [SerializeField] private ScrollRect seriesScrollview;

    [SerializeField] private GameObject scrollviewContent;
    [SerializeField] private GameObject rowContent;
    [SerializeField] private GameObject seriesContent;
    [SerializeField] private GameObject searchContent;

    [SerializeField] private Text seriesText;

    [SerializeField] private InputField inputField;
    [SerializeField] private Text searchText;
    [SerializeField] private string keyword;

    [SerializeField] private Button resetButton;

    private const string dicomURL = "http://10.10.20.173:5080/v2/Dicom/";
    private List<DicomStudy> dicomStudyList;
    private readonly Dictionary<string, GameObject> dicomStudyRowContents = new();
    private readonly List<Text> dicomSeriesTexts = new();

    public void OnClickStudyRow()
    {
        GameObject studyRowButton = EventSystem.current.currentSelectedGameObject;
        string studyId = studyRowButton.transform.parent.name.Split(")")[^1];

        if (studyId != "RowContent")
        {
            RemoveSeriesObject();
            StartCoroutine(GetSeriesData(studyId));
            SetStudyVisibility(false, studyId);
        }
    }

    public void OnClickReset()
    {
        SetStudyVisibility(true);
        RemoveSeriesObject();
    }

    public void OnClickSearch()
    {
        keyword = searchText.text;
        foreach (DicomStudy dicomData in dicomStudyList)
        {
            // patientID 나 patientName 뿐만 아니라 전체 field 를 기준으로 keyword 찾기
            string dicomStudyString = JsonConvert.SerializeObject(dicomData);
            dicomStudyRowContents[dicomData.id.ToString()].SetActive(false);
            if (dicomStudyString.Contains(keyword)) dicomStudyRowContents[dicomData.id.ToString()].SetActive(true);

        }
    }

    // 스크롤뷰 초기화 -> 데이터의 크기만큼 화면이 출력되도록
    void ResetScrollview()
    {
        studyScrollview.gameObject.SetActive(false);
        studyScrollview.gameObject.SetActive(true);
    }

    void RemoveSeriesObject()
    {
        foreach (Text seriesRowText in dicomSeriesTexts)
        {
            Destroy(seriesRowText.gameObject);
        }
        dicomSeriesTexts.Clear();
    }

    void SetStudyVisibility(bool studyDataVisible, string studyId = "")
    {
        if (studyDataVisible)
        {
            // study data가 활성화되면 series 데이터는 비활성화되어 화면이 겹치는 것 방지
            seriesScrollview.gameObject.SetActive(false);
            searchContent.SetActive(true);
            resetButton.gameObject.SetActive(false);
        }
        else
        {
            seriesScrollview.gameObject.SetActive(true);
            searchContent.SetActive(false);
            resetButton.gameObject.SetActive(true);
        }

        foreach (string rowStudyId in dicomStudyRowContents.Keys)
        {
            dicomStudyRowContents[rowStudyId].SetActive(true);
            if (studyDataVisible) continue;
            if (rowStudyId != studyId) dicomStudyRowContents[rowStudyId].SetActive(false);

        }

        ResetScrollview();
    }

    void AddDicomSeriesRow(DicomSeries dicomSeries)
    {
        string seriesValue = "-------------------------------------------------------------------------------------------- \n";

        foreach (var property in typeof(DicomSeries).GetProperties())
        {
            object val = property.GetValue(dicomSeries);
            seriesValue += $"{property.Name}: {val} \n";
        }

        Text seriesData = Instantiate(seriesText, seriesContent.transform);
        dicomSeriesTexts.Add(seriesData);
        seriesData.text = seriesValue;
        seriesData.gameObject.SetActive(true);
    }

    void AddDicomStudyRow(DicomStudy dicomStudy)
    {
        GameObject newStudyRow = Instantiate(rowContent, scrollviewContent.transform);
        DicomStudyRow dicomStudyRow = newStudyRow.GetComponent<DicomStudyRow>();
        dicomStudyRowContents.Add(dicomStudy.id.ToString(), newStudyRow);
        dicomStudyRow.SetStudyData(dicomStudy);
        newStudyRow.name += dicomStudy.id.ToString();
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
            dicomStudyList = JsonConvert.DeserializeObject<List<DicomStudy>>(reqStudy.downloadHandler.text);
            inputField.textComponent = searchText;
            foreach (DicomStudy studyData in dicomStudyList)
            {
                AddDicomStudyRow(studyData);
            }
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
        }
    }

}