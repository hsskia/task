using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Mars.db;
using Unity.VisualScripting;
namespace study_row{
    public class Dicom_Study_Row : MonoBehaviour{

        public Text id_text;
        public Text name_text;
        public Text description_text;
        public Text patient_id_text;
        public Text num_series_text;

        public void SetStudyData(DicomStudy study)
        {
            id_text.text = study.id.ToString();
            name_text.text = study.patientName;
            description_text.text = study.studyDescription;
            patient_id_text.text = study.patientID;
            num_series_text.text = study.numberOfSeries.ToString();
        }
    }
}
