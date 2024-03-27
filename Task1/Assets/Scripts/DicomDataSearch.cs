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
using Newtonsoft.Json;

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
    [SerializeField] private string keyword;

    private const string dicomURL = "http://10.10.20.173:5080/v2/Dicom/";
    private string studyId;
    private List<DicomStudy> dicomStudyList;

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

        foreach (DicomStudy item in dicomStudyList)
        {
            // patientID 나 patientName 뿐만 아니라 전체 field 를 기준으로 keyword 찾기
            string dicomStudyString = JsonConvert.SerializeObject(item);
            if (dicomStudyString.Contains(keyword))
            {
                searchIdList.Add(item.id.ToString());
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

            // keyword 가 포함된 데이터들의 ID 로 구성된 searchedData 에
            // 해당되는 object 면 활성화, keyword 가 포함 안 되었으면 비활성화
            if (searchedData.Count == dicomStudyList.Count)
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

    // 데이터의 크기만큼 화면이 출력되도록
    void ResetScrollview()
    {
        studyScrollview.gameObject.SetActive(false);
        studyScrollview.gameObject.SetActive(true);
    }

    void RemoveSeriesObject()
    {
        Transform[] childObject = seriesContent.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < childObject.Length; i++)
        {

            // Study data 만 화면에 출력하기 위해 Series data 를 의미하는 Clone 된 Object 들을 제거
            if (childObject[i].name.Contains("Clone"))
            {
                Destroy(childObject[i].gameObject);
            }
        }
    }

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

        /* index 0번은 모든 Dicom Study Data Row 를 포함하는 GameObject 이기 때문에 이를 비활성화 시킬 경우
           Series Data 와 일치하는 Study Data 는 남겨두고자 하는 원래 목적이 사라질 수 있기 때문에
           index 를 1번 부터 시작하여 Series Data 가 표현하는 studyId 와 일치하는 Study Data Row 만
           활성화된 상태로 남겨둠.
        */
        for (int i = 1; i < childObject.Length; i++)
        {
            childObject[i].gameObject.SetActive(true);
            if (check) continue;

            string childName = childObject[i].name;
            string childId = Regex.Replace(childName, @"[^0-9]", "");

            if (childName.Contains("Clone") &
            (childId != studyId))
            {
                childObject[i].gameObject.SetActive(false);
            }
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

        Text seriesData = (Text)Instantiate(seriesText, seriesContent.transform);
        seriesData.text = seriesValue;
    }

    void AddDicomStudyRow(DicomStudy dicomStudy)
    {
        GameObject newRow = Instantiate(rowContent, scrollviewContent.transform);
        DicomStudyRow dicomStudyRow = newRow.GetComponent<DicomStudyRow>();
        dicomStudyRow.SetStudyData(dicomStudy);
        newRow.name = newRow.name + dicomStudy.id.ToString();
        ResetScrollview();
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
            List<DicomSeries> dicomSeriesList = JsonConvert.DeserializeObject<List<DicomSeries>>(reqSeries.downloadHandler.text);
            seriesContent.SetActive(true);
            foreach (DicomSeries seriesData in dicomSeriesList)
            {
                AddDicomSeriesRow(seriesData);
            }
            seriesText.gameObject.SetActive(false);
        }
    }

}