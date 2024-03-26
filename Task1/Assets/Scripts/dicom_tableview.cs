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
using study_row;

public class dicom_tableview : MonoBehaviour
{
    public ScrollRect study_scrollview;
    public ScrollRect series_scrollview;

    public GameObject scrollview_content;
    public GameObject row_content;
    public GameObject series_content;

    public Text series_text;

    string dicom_url = "http://10.10.20.173:5080/v2/Dicom/";
    public string study_id;

    public void on_click()
    {
        GameObject btn = EventSystem.current.currentSelectedGameObject;
        study_id = btn.GetComponentInChildren<Text>().text;

        // ID 버튼을 클릭했을 때 처음으로 원상복구 되도록
        if (study_id == "ID"){ 
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
    void ResetScrollView(){
        study_scrollview.gameObject.SetActive(false);
        study_scrollview.gameObject.SetActive(true);
    }

    void RemoveSeriesObject(){
        Transform[] child_object = series_content.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < child_object.Length; i++){

            // 원본 제외 복사한 항목 모두 삭제
            if(child_object[i].name.Contains("Clone")){
                Destroy(child_object[i].gameObject);
            }
        }
    }

    void SetStudyVisibility(bool check){
        if(check == true){
            // study data가 활성화되면 series 데이터는 비활성화되어 화면이 겹치는 것 방지
            series_scrollview.gameObject.SetActive(false);             
        }
        else{     
            series_scrollview.gameObject.SetActive(true);
            series_text.gameObject.SetActive(true);
        }   
            
        Transform[] child_object = scrollview_content.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < child_object.Length; i++){
            if(check == true){
                child_object[i].gameObject.SetActive(true);
            }
            else{
                string child_name = child_object[i].name;
                string child_id = Regex.Replace(child_name, @"[^0-9]", "");
                if (child_name.Contains("Clone") & 
                (child_id != study_id)) // id가 일치하는 data 외에는 모두 비활성화
                {
                    child_object[i].gameObject.SetActive(false);
                }
            }
        }

        ResetScrollView();        
    }

    void Add_DicomSeries_Rows(JArray data)
    {
        series_content.SetActive(true);

        // study id에 일치하는 series 데이터들
        foreach (JObject item in data) {
            DicomSeries series = item.ToObject<DicomSeries>();
            string series_value = "-------------------------------------------------------------------------------------------- \n";
            
            // series 데이터 출력
            foreach (var property in typeof(DicomSeries).GetProperties()){
                object val = property.GetValue(series);
                series_value += $"{property.Name}: {val} \n";
            }
            Text series_data = (Text)Instantiate(series_text, series_content.transform);
            series_data.text = series_value;
            
        }
        series_text.gameObject.SetActive(false);
    }

    void Add_DicomStudy_Rows(JArray data){
        foreach (JObject item in data) {
            GameObject new_row = Instantiate(row_content, scrollview_content.transform);
            Dicom_Study_Row dicom_study_row = new_row.GetComponent<Dicom_Study_Row>();
            DicomStudy study = item.ToObject<DicomStudy>();
            dicom_study_row.SetStudyData(study);
            new_row.name = new_row.name + study.id.ToString();
        }
        ResetScrollView();
    }

    void Start(){
        StartCoroutine(GetStudyData());
    }

    IEnumerator GetStudyData()
    {
        UnityWebRequest req_study = UnityWebRequest.Get(dicom_url + "Study");
        yield return req_study.SendWebRequest();

        if (req_study.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(req_study.error);
        }
        else
        {
            JArray dicom_study = JArray.Parse(req_study.downloadHandler.text);
            Add_DicomStudy_Rows(dicom_study);
            
        }
    }

    IEnumerator GetSeriesData()
    {
        UnityWebRequest req_series = UnityWebRequest.Get(dicom_url + "Series?studyId=" + study_id);
        yield return req_series.SendWebRequest();

        if (req_series.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(req_series.error);
        }
        else
        {
            JArray dicom_series = JArray.Parse(req_series.downloadHandler.text);
            Add_DicomSeries_Rows(dicom_series);
        }
    }

}