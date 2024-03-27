using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Mars.db;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.EventSystems;
using System.Resources;
using JetBrains.Annotations;
using System.Text.RegularExpressions;
using UnityEditor;
using Unity.IO.LowLevel.Unsafe;
using StudyRow;

public class DicomDataSearch : MonoBehaviour
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
    [SerializeField] private Button searchButton;
    [SerializeField] private string keyword;

    private const string dicomURL = "http://10.10.20.173:5080/v2/Dicom/";
    private string studyId;
    private JArray dicomStudyData;

    public void OnClickId()
    {
        GameObject idButton = EventSystem.current.currentSelectedGameObject;
        studyId = idButton.GetComponentInChildren<Text>().text;

        // ID 버튼을 클릭했을 때 처음으로 원상복구 되도록
        if (studyId == "ID")
        {
            SetStudyVisibility(true);
            RemoveSeriesObject();
        }
        else
        {
            RemoveSeriesObject();
            StartCoroutine(GetSeriesData());
            SetStudyVisibility(false);
        }
    }

    public void OnClickSearch()
    {
        keyword = searchText.text;
        List<string> searchIdList = new List<string>();

        foreach (JObject item in dicomStudyData)
        {
            if (item.ToString().Contains(keyword))
            {
                searchIdList.Add(item["id"].ToString());
            }
        }

        VisibilityDicomSearchKeyword(searchIdList);
    }

    void VisibilityDicomSearchKeyword(List<string> searchedData)
    {
        Transform[] childObject = scrollviewContent.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < childObject.Length; i++)
        {
            string childName = childObject[i].name;
            string childId = Regex.Replace(childName, @"[^0-9]", "");

            // keyword 가 포함된 데이터들의 ID 가 포함된 list 에
            // 해당되는 object 면 활성화, keyword 가 포함 안 되었으면 비활성화
            if (searchedData.Count == dicomStudyData.Count)
            {
                childObject[i].gameObject.SetActive(true);
            }
            else if (childName.Contains("Clone") &
            (!searchedData.Contains(childId))) // id가 일치하는 data 외에는 모두 비활성화
            {
                childObject[i].gameObject.SetActive(false);
            }
        }

        ResetScrollview();
    }

    // 스크롤뷰 초기화 -> 데이터의 크기만큼 화면이 출력되도록
    void ResetScrollview()
    {
        studyScrollview.gameObject.SetActive(false);
        studyScrollview.gameObject.SetActive(true);
    }

    // Series 데이터 항목 삭제
    void RemoveSeriesObject()
    {
        Transform[] childObject = seriesContent.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < childObject.Length; i++)
        {

            // 원본 제외 복사한 항목 모두 삭제
            if (childObject[i].name.Contains("Clone"))
            {
                Destroy(childObject[i].gameObject);
            }
        }
    }

    // Study data 활성화
    void SetStudyVisibility(bool check)
    {
        if (check)
        {
            // study data가 활성화되면 series 데이터는 비활성화되어 화면이 겹치는 것 방지
            seriesScrollview.gameObject.SetActive(false);
            searchContent.gameObject.SetActive(true);
        }
        else
        {
            seriesScrollview.gameObject.SetActive(true);
            seriesText.gameObject.SetActive(true);
            searchContent.SetActive(false);
        }

        Transform[] childObject = scrollviewContent.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < childObject.Length; i++)
        {
            childObject[i].gameObject.SetActive(true);
            if (check) continue;

            string childName = childObject[i].name;
            string childId = Regex.Replace(childName, @"[^0-9]", "");

            if (childName.Contains("Clone") &
            (childId != studyId)) // id가 일치하는 data 외에는 모두 비활성화
            {
                childObject[i].gameObject.SetActive(false);
            }
        }

        ResetScrollview();

    }

    void AddDicomSeriesRows(JArray dicomSeries)
    {
        seriesContent.SetActive(true);
        inputField.textComponent = searchText;

        foreach (JObject item in dicomSeries)
        {
            DicomSeries series = item.ToObject<DicomSeries>();
            string seriesValue = "-------------------------------------------------------------------------------------------- \n";

            foreach (var property in typeof(DicomSeries).GetProperties())
            {
                object val = property.GetValue(series);
                seriesValue += $"{property.Name}: {val} \n";
            }
            Text seriesData = (Text)Instantiate(seriesText, seriesContent.transform);
            seriesData.text = seriesValue;

        }
        seriesText.gameObject.SetActive(false);
    }

    void AddDicomStudyRows(JArray dicomStudy)
    {
        foreach (JObject item in dicomStudy)
        {
            GameObject newRow = Instantiate(rowContent, scrollviewContent.transform);
            DicomStudyRow dicomStudyRow = newRow.GetComponent<DicomStudyRow>();
            DicomStudy study = item.ToObject<DicomStudy>();
            dicomStudyRow.SetStudyData(study);
            newRow.name = newRow.name + study.id.ToString();
        }
        ResetScrollview();
    }

    void Start()
    {
        StartCoroutine(GetStudyData());
    }

    IEnumerator GetStudyData()
    {
        // http 접근 후 data GET
        UnityWebRequest reqStudy = UnityWebRequest.Get(dicomURL + "Study");
        yield return reqStudy.SendWebRequest();

        if (reqStudy.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(reqStudy.error);
        }
        else
        {
            dicomStudyData = JArray.Parse(reqStudy.downloadHandler.text);
            AddDicomStudyRows(dicomStudyData);

        }
    }

    IEnumerator GetSeriesData()
    {
        UnityWebRequest reqSeries = UnityWebRequest.Get(dicomURL + "Series?studyId=" + studyId);
        yield return reqSeries.SendWebRequest();

        if (reqSeries.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(reqSeries.error);
        }
        else
        {
            JArray dicomSeriesData = JArray.Parse(reqSeries.downloadHandler.text);
            AddDicomSeriesRows(dicomSeriesData);
        }
    }

}