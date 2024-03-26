using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Mars.db;
using Unity.VisualScripting;
namespace StudyRow{
    public class DicomStudyRow : MonoBehaviour{

        public Text idText;
        public Text nameText;
        public Text descriptionText;
        public Text patientIdText;
        public Text numSeriesText;

        public void SetStudyData(DicomStudy study)
        {
            idText.text = study.id.ToString();
            nameText.text = study.patientName;
            descriptionText.text = study.studyDescription;
            patientIdText.text = study.patientID;
            numSeriesText.text = study.numberOfSeries.ToString();
        }
    }
}
