using UnityEngine;
using UnityEngine.UI;
using Mars.db;
namespace StudyRow
{
    public class DicomStudyRow : MonoBehaviour
    {

        [SerializeField] private Text idText;
        [SerializeField] private Text nameText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text patientIdText;
        [SerializeField] private Text numSeriesText;

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
