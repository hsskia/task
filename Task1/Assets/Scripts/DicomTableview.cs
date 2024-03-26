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
using Unity.VisualScripting;
using StudyRow;

public class DicomTableview : MonoBehaviour
{
    public ScrollRect studyScrollview;
    public ScrollRect seriesScrollview;

    public GameObject scrollviewContent;
    public GameObject rowContent;
    public GameObject seriesContent;

    public Text seriesText;

    string dicomURL = "http://10.10.20.173:5080/v2/Dicom/";
    public string studyId;

    public void OnClick()
    {
        GameObject btn = EventSystem.current.currentSelectedGameObject;
        studyId = btn.GetComponentInChildren<Text>().text;

        // ID 버튼을 클릭했을 때 처음으로 원상복구 되도록
        if (studyId == "ID"){ 
            SetStudyVisibility(true);
            RemoveSeriesObject();
        }
        else{
            RemoveSeriesObject();
            StartCoroutine(GetSeriesData());
            SetStudyVisibility(false);
        }
    }

    // 스크롤뷰 초기화 -> 데이터의 크기만큼 화면이 출력되도록
    void ResetScrollview(){
        studyScrollview.gameObject.SetActive(false);
        studyScrollview.gameObject.SetActive(true);
    }

    void RemoveSeriesObject(){
        Transform[] childObject = seriesContent.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < childObject.Length; i++){

            // 원본 제외 복사한 항목 모두 삭제
            if(childObject[i].name.Contains("Clone")){
                Destroy(childObject[i].gameObject);
            }
        }
    }

    void SetStudyVisibility(bool check){
        if(check == true){
            // study data가 활성화되면 series 데이터는 비활성화되어 화면이 겹치는 것 방지
            seriesScrollview.gameObject.SetActive(false);             
        }
        else{     
            seriesScrollview.gameObject.SetActive(true);
            seriesText.gameObject.SetActive(true);
        }   
            
        Transform[] childObject = scrollviewContent.GetComponentsInChildren<Transform>(true);

        /* index 0번은 모든 Dicom Study Data Row 를 포함하는 GameObject 이기 때문에 이를 비활성화 시킬 경우
           Series Data 와 일치하는 Study Data 는 남겨두고자 하는 원래 목적이 사라질 수 있기 때문에
           index 를 1번 부터 시작하여 Series Data 가 표현하는 studyID 와 일치하는 Study Data Row 는
           활성화된 상태로 남겨둠.
        */
        for (int i = 1; i < childObject.Length; i++){
            if(check == true){
                childObject[i].gameObject.SetActive(true);
            }
            else{
                string childName = childObject[i].name;
                string childId = Regex.Replace(childName, @"[^0-9]", "");
                if (childName.Contains("Clone") & 
                (childId != studyId)) // id가 일치하는 data 외에는 모두 비활성화
                {
                    childObject[i].gameObject.SetActive(false);
                }
            }
        }

        ResetScrollview();        
    }

    void AddDicomSeriesRows(JArray dicomSeries)
    {
        seriesContent.SetActive(true);

        // study id에 일치하는 series 데이터들
        foreach (JObject item in dicomSeries) {
            DicomSeries series = item.ToObject<DicomSeries>();
            string seriesValue = "-------------------------------------------------------------------------------------------- \n";
            
            // series 데이터 출력
            foreach (var property in typeof(DicomSeries).GetProperties()){
                object val = property.GetValue(series);
                seriesValue += $"{property.Name}: {val} \n";
            }
            Text seriesData = (Text)Instantiate(seriesText, seriesContent.transform);
            seriesData.text = seriesValue;
            
        }
        seriesText.gameObject.SetActive(false);
    }

    void AddDicomStudyRows(JArray dicomStudy){
        foreach (JObject item in dicomStudy) {
            GameObject newRow = Instantiate(rowContent, scrollviewContent.transform);
            DicomStudyRow dicomStudyRow = newRow.GetComponent<DicomStudyRow>();
            DicomStudy study = item.ToObject<DicomStudy>();
            dicomStudyRow.SetStudyData(study);
            newRow.name = newRow.name + study.id.ToString();
        }
        ResetScrollview();
    }

    void Start(){
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
            JArray dicomStudy = JArray.Parse(reqStudy.downloadHandler.text);
            AddDicomStudyRows(dicomStudy);
            
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
            JArray dicomSeries = JArray.Parse(reqSeries.downloadHandler.text);
            AddDicomSeriesRows(dicomSeries);
        }
    }

}