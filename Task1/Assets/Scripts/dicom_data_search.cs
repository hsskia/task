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

public class dicom_data_search : MonoBehaviour
{
    public GameObject id_content;
    public GameObject patient_name_content;
    public GameObject study_description_content;
    public GameObject patient_id_content;
    public GameObject num_series_content;
    public GameObject scrollview_content;
    public GameObject series_content;
    
    // series 연동 버튼
    public GameObject series_button;

    // table data row
    public GameObject id_row;
    public GameObject patient_name_row;
    public GameObject study_description_row;
    public GameObject patient_id_row;
    public GameObject num_series_row;

    string dicom_url = "http://10.10.20.173:5080/v2/Dicom/";
    public string study_id;
    public JArray dicom_study;

    public GameObject series_text; // series data 출력용 Text
    public GameObject series_scrollview; // series data 출력용 ScrollView

    /////////////
    public GameObject search_content;
    public GameObject input_field;
    public GameObject search_text;
    public GameObject search_button;
    public string keyword;

    public void on_click_id()
    {
        GameObject id_btn = EventSystem.current.currentSelectedGameObject;
        study_id = id_btn.GetComponentInChildren<Text>().text;
        
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

    public void on_click_search()
    {
        keyword = search_text.GetComponent<Text>().text;
        search_text.transform.SetParent(input_field.transform);
        
        List<string> search_id_list = new List<string>();

        foreach (JObject item in dicom_study) {
            if (item.ToString().Contains(keyword)){
                search_id_list.Add(item["id"].ToString());
            }
        }

        // keyword 에 해당하는 dicom data 의 id 출력
        foreach (string item in search_id_list){
            print(item);
        }
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
            series_scrollview.SetActive(false);      
            search_content.SetActive(true);       
        }
        else{     
            series_scrollview.SetActive(true);
            series_text.SetActive(true);
            search_content.SetActive(false);
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
            GameObject series_data = (GameObject)Instantiate(series_text);
            series_data.transform.SetParent(series_content.transform);
            series_data.GetComponent<Text>().text = result2;
            
        }
        series_text.transform.SetParent(series_content.transform);
        series_text.GetComponent<Text>().text = result;
        series_text.SetActive(false);
    }

    void Add_DicomStudy_Rows(JArray data){
        foreach (JObject item in data) {
            GameObject id = (GameObject)Instantiate(series_button);
            GameObject patient_name = (GameObject)Instantiate(patient_name_row);
            GameObject study_description = (GameObject)Instantiate(study_description_row);
            GameObject patient_id = (GameObject)Instantiate(patient_id_row);
            GameObject num_series = (GameObject)Instantiate(num_series_row);
            
            id_row.transform.SetParent(series_button.transform);
            id.transform.SetParent(id_content.transform);
            patient_name.transform.SetParent(patient_name_content.transform);
            study_description.transform.SetParent(study_description_content.transform);
            patient_id.transform.SetParent(patient_id_content.transform);
            num_series.transform.SetParent(num_series_content.transform);

            Text id_elements = id.GetComponentInChildren<Text>(); // id밑에 복사된 text를 가져와야함.
            Text patient_name_elements = patient_name.GetComponent<Text>();
            Text study_description_elements = study_description.GetComponent<Text>();
            Text patient_id_elements = patient_id.GetComponent<Text>();
            Text num_seriese_elements = num_series.GetComponent<Text>();

            DicomStudy study = item.ToObject<DicomStudy>();

            id_elements.text = study.id.ToString();
            patient_name_elements.text = study.patientName.ToString();
            study_description_elements.text = study.studyDescription.ToString();
            patient_id_elements.text = study.patientID.ToString();
            num_seriese_elements.text = study.numberOfSeries.ToString();

            // 이름에 id 추가
            // id_text -> id_text(Clone)18
            id.name = id.name + study.id.ToString();
            patient_name_elements.name = patient_name_elements.name + study.id.ToString();
            study_description.name = study_description.name + study.id.ToString();
            patient_id_elements.name = patient_id_elements.name + study.id.ToString();
            num_seriese_elements.name = num_seriese_elements.name + study.id.ToString();
        }
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
            dicom_study = JArray.Parse(req_study.downloadHandler.text);
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