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
using UnityEngine.UIElements;
using Unity.VisualScripting;

public class dicom_tableview : MonoBehaviour
{
    public ScrollRect study_scrollview;
    public ScrollRect series_scrollview; // series data 출력용 ScrollView

    public GameObject id_content;
    public GameObject patient_name_content;
    public GameObject study_description_content;
    public GameObject patient_id_content;
    public GameObject num_series_content;
    public GameObject scrollview_content;
    public GameObject series_content;
    
    // series 연동 버튼
    public UnityEngine.UI.Button button;

    // table data row
    public Text id_row;
    public Text patient_name_row;
    public Text study_description_row;
    public Text patient_id_row;
    public Text num_series_row;
    public Text series_text; // series data 출력용 Text

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
            StartCoroutine(GetSeriesData());
            SetStudyVisibility(false);
        }
    }

    // 스크롤뷰 초기화 -> 데이터의 크기만큼 화면이 출력되도록
    void ResetScrollView(){
        study_scrollview.gameObject.SetActive(false);
        study_scrollview.gameObject.SetActive(true);
    }

    // Series 데이터 항목 삭제
    void RemoveSeriesObject(){
        Transform[] child_object = series_content.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < child_object.Length; i++){

            // 원본 제외 복사한 항목 모두 삭제
            if(child_object[i].name.Contains("Clone")){
                Destroy(child_object[i].gameObject);
            }
        }
    }

    // Study data 활성화
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
            Transform[] child_object2 = child_object[i].GetComponentsInChildren<Transform>(true);
            for (int j = 1; j < child_object2.Length; j++){
                if(check == true){
                    child_object2[j].gameObject.SetActive(true);
                }
                else{
                    string child_name = child_object2[j].name;
                    string child_id = Regex.Replace(child_name, @"[^0-9]", "");
                    if (child_name.Contains("Clone") & 
                    (child_id != study_id)) // id가 일치하는 data 외에는 모두 비활성화
                    {
                        child_object2[j].gameObject.SetActive(false);
                    }
                }
            }
        }

        ResetScrollView();        
    }

    void Add_DicomSeries_Rows(JArray data)
    {
        series_content.SetActive(true);
        int id_val = (int) data[0]["dicomStudyId"];
        string result = $"Dicom Study ID {id_val} Dicom Series Data";

        // study id에 일치하는 series 데이터들
        foreach (JObject item in data) {
            DicomSeries series = item.ToObject<DicomSeries>(); // 받아온 data를 DicomStudy class에 입력 
            string result2 = "-------------------------------------------------------------------------------------------- \n";
            
            // series 데이터 출력
            foreach (var property in typeof(DicomSeries).GetProperties()){
                object val = property.GetValue(series);
                result2 += $"{property.Name}: {val} \n";
            }
            Text series_data = (Text)Instantiate(series_text, series_content.transform);
            series_data.text = result2;
            
        }
        series_text.transform.SetParent(series_content.transform);
        series_text.text = result;
        series_text.gameObject.SetActive(false);
    }

    void Add_DicomStudy_Rows(JArray data){
        foreach (JObject item in data) {
            // Button 생성 및 부모 지정
            UnityEngine.UI.Button id = Instantiate(button, id_content.transform);
            
            // Text 요소 생성 및 부모 지정
            Text patient_name = Instantiate(patient_name_row, patient_name_content.transform);
            Text study_description = Instantiate(study_description_row, study_description_content.transform);
            Text patient_id = Instantiate(patient_id_row, patient_id_content.transform);
            Text num_series = Instantiate(num_series_row, num_series_content.transform);

            // Text 요소에 데이터 설정
            DicomStudy study = item.ToObject<DicomStudy>();
            id.GetComponentInChildren<Text>().text = study.id.ToString();
            patient_name.text = study.patientName.ToString();
            study_description.text = study.studyDescription.ToString();
            patient_id.text = study.patientID.ToString();
            num_series.text = study.numberOfSeries.ToString();

            // 이름에 id 추가
            id.name = id.name + study.id.ToString();
            patient_name.name = patient_name.name + study.id.ToString();
            study_description.name = study_description.name + study.id.ToString();
            patient_id.name = patient_id.name + study.id.ToString();
            num_series.name = num_series.name + study.id.ToString();

        }
        ResetScrollView();
    }

    void Start(){
        StartCoroutine(GetStudyData());
    }

    IEnumerator GetStudyData()
    {
        // http 접근 후 data GET
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