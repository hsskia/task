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
    public ScrollRect study_scrollview;
    public ScrollRect series_scrollview; // series data 출력용 ScrollView

    public GameObject scrollview_content;
    public GameObject series_content;
    public GameObject row_content;
    
    // series 연동 버튼
    public Button button;
    public Text series_text; // series data 출력용 Text

    string dicom_url = "http://10.10.20.173:5080/v2/Dicom/";
    public string study_id;
    public JArray dicom_study;

    public GameObject search_content;
    public InputField input_field;
    public Text search_text;
    public Button search_button;
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
            RemoveSeriesObject();
            StartCoroutine(GetSeriesData());
            SetStudyVisibility(false);
        }
    }

    public void on_click_search()
    {
        keyword = search_text.text;
        search_text.transform.SetParent(input_field.transform);
        
        List<string> search_id_list = new List<string>();

        foreach (JObject item in dicom_study) {
            if (item.ToString().Contains(keyword)){
                search_id_list.Add(item["id"].ToString());
            }
        }

        Visibility_Dicom_Search_Keyword(search_id_list);
    }

    void Visibility_Dicom_Search_Keyword(List<string> data){
        Transform[] child_object = scrollview_content.GetComponentsInChildren<Transform>(true);
        for (int i = 1; i < child_object.Length; i++){
            string child_name = child_object[i].name;
            string child_id = Regex.Replace(child_name, @"[^0-9]", "");

            // keyword 가 포함된 데이터들의 ID 가 포함된 list 에
            // 해당되는 object 면 활성화, keyword 가 포함 안 되었으면 비활성화
            if (data.Count == dicom_study.Count){
                child_object[i].gameObject.SetActive(true);
            }
            else if (child_name.Contains("Clone") & 
            (!data.Contains(child_id))) // id가 일치하는 data 외에는 모두 비활성화
            {
                child_object[i].gameObject.SetActive(false);
            }
        }

        ResetScrollView();
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
            search_content.gameObject.SetActive(true);     
        }
        else{     
            series_scrollview.gameObject.SetActive(true);
            series_text.gameObject.SetActive(true);
            search_content.SetActive(false);
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
            GameObject new_row = (GameObject) Instantiate(row_content, scrollview_content.transform);

            // Text 요소에 데이터 설정
            DicomStudy study = item.ToObject<DicomStudy>();
            Text id_row = new_row.transform.Find("ID_Button").GetComponentInChildren<Text>();
            Text patient_name_row = new_row.transform.Find("name_text").GetComponent<Text>();
            Text description_row = new_row.transform.Find("des_text").GetComponent<Text>();
            Text patient_id_row = new_row.transform.Find("pid_text").GetComponent<Text>();
            Text num_series_row = new_row.transform.Find("num_series_text").GetComponent<Text>();
            
            id_row.text = study.id.ToString();
            patient_name_row.text = study.patientName.ToString();
            description_row.text = study.studyDescription.ToString();
            patient_id_row.text = study.patientID.ToString();
            num_series_row.text = study.numberOfSeries.ToString();

            // 이름에 id 추가
            new_row.name = new_row.name + study.id.ToString();

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