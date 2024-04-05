

using SQLite;
using System;
using System.Collections.Generic;

#pragma warning disable IDE1006 // 명명 스타일 - lowerCamelCase 허용

namespace Mars.db
{
    /// <summary>
    ///     reprensts database table for Object Relational Mapper (DDL source)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SqliteTableAttribute : Attribute { }

    #region DICOM

    //
    //   * index(정렬 및 검색이 빈번)가 필요한 경우가 아니라면 DICOM tag 데이터들은
    //     편의상 string (array 의 경우 separator '\') 형식 사용을 선호
    //   * 서비스에서 사용할만한 tag들만 추려서, DicomStudy, DicomSeries 등에 분산해 저장.
    //     다른 tag 가 추후에 필요하게 된다면 원본 dicom 에서 꺼내와 설정하면 될 것.(https://bitbucket.org/alkee_skia/mars3/issues/407)
    //   * tag(indexed) 와 memo(non-indexed) 는 custom reserved field
    //

    [SqliteTable]
    public class DicomFile
    {
        #region Keys

        [PrimaryKey, AutoIncrement]
        public long id { get; set; } // same as ROW_ID

        [Indexed]
        public long dicomStudyId { get; set; }

        [Indexed]
        public long dicomSeriesId { get; set; }

        #endregion Keys

        public string filePath { get; set; }

        public DateTime transferedDateTime { get; set; } = DateTime.Now;

        #region DICOM tags

        [Unique]
        public string sopInstanceUID { get; set; } // https://dicom.innolitics.com/ciods/rt-plan/sop-common/00080018

        public string sopClassUID { get; set; } // https://dicom.innolitics.com/ciods/cr-image/sop-common/00080016

        public string windowCenter { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0028,1050)
        public string windowWidth { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0028,1051)
        public string imagePositionPatient { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0020,0032)

        // https://stackoverflow.com/a/6598664 이유로 아래 값은 존재하지 않거나 부정확할 수 있으므로, image 위치 정렬을 위한 유일한 방법으로 사용하지 말 것.
        public int instanceNumber { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0020,0013)

        public string sliceLocation { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0020,0012)

        #region shared field to DicomSeries

        public string rows { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0028,0010)
        public string columns { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0028,0011)
        public string pixelSpacing { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0028,0030)
        public string sliceThickness { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0018,0050)
        public string imageType { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0008,0008)
        public string imageOrientationPatient { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0020,0037)

        #endregion shared field to DicomSeries

        #endregion DICOM tags

        [Indexed]
        public string tag { get; set; }

        public string memo { get; set; }
    }

    /// <summary>
    ///   Dicom series 를 나타냄. DicomFile 과 중복 정의된 rows, columns 는 공통된 값을 나타내지만 일부 다른 값을 갖는
    ///   DicomFile 이 포함되어있는 경우 null
    /// </summary>
    [SqliteTable]
    public class DicomSeries
    {
        #region Keys

        [PrimaryKey, AutoIncrement]
        public long id { get; set; } // same as ROW_ID

        [Indexed]
        public long dicomStudyId { get; set; }

        #endregion Keys

        #region DICOM tags

        [Unique]
        public string seriesInstanceUID { get; set; } // https://dicom.innolitics.com/ciods/cr-image/general-series/0020000e

        public string seriesNumber { get; set; } // https://dicom.innolitics.com/ciods/cr-image/general-series/00200011
        public string modality { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0008,0060)
        public DateTime? seriesDateTime { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0008,0021) + http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0008,0031)
        public string patientPosition { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0018,5100)
        public string acquisitionNumber { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0020,0012)
        public string scanningSequence { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0018,0020)
        public string bodyPartExamined { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0018,0015)
        public string seriesDescription { get; set; } // https://dicom.innolitics.com/ciods/rt-plan/rt-series/0008103e

        #region DicomFile 에도 존재하는 field.(#412) ; null 인 경우 series 내 모든 값이 동일하지 않음을 의미

        public string rows { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0028,0010)
        public string columns { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0028,0011)
        public string pixelSpacing { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0028,0030)
        public string sliceThickness { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0018,0050)
        public string imageType { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0008,0008)
        public string imageOrientationPatient { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0020,0037)

        #endregion DicomFile 에도 존재하는 field.(#412) ; null 인 경우 series 내 모든 값이 동일하지 않음을 의미

        #endregion DICOM tags

        #region denormalized

        public int numberOfDicomFiles { get; set; } = 0; // numberOfImages
        public DateTime createdDateTime { get; set; } = DateTime.Now; // 첫 dicom file
        public DateTime lastModifiedDateTime { get; set; } = DateTime.Now; // 마지막 dicom file

        #endregion denormalized

        // number of frames ?? XELIS 에서는 DB 에 포함되어있는데..(imagecount 와 값이 같은 경우만 경험)

        public string volumeFilePath { get; set; } // 미리 생성된 dicom 의 HU-volume 파일

        [Indexed]
        public string tag { get; set; }

        public string memo { get; set; }
    }

    [SqliteTable]
    public class DicomStudy
    {
        #region Keys

        [PrimaryKey, AutoIncrement]
        public long id { get; set; } // same as ROW_ID

        #endregion Keys

        #region DICOM tags

        [Unique]
        public string studyInstanceUID { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0020,000D)

        public string studyID { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0020,0010)

        [Indexed]
        public DateTime? studyDateTime { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0008,0020) + http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0008,0030)

        public string studyDescription { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0008,1030)

        [Indexed]
        public string patientID { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0010,0020)

        [Indexed]
        [Collation("NOCASE")]
        public string patientName { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0010,0010)

        public DateTime? patientBirthDate { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0010,0030)
        public string patientSex { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0010,0040)

        [Collation("NOCASE")]
        public string referringPhysicianName { get; set; } // http://dicomlookup.com/lookup.asp?sw=Tnumber&q=(0008,0090)

        #endregion DICOM tags

        #region denormalized

        public int numberOfSeries { get; set; } = 0;
        public DateTime createdDateTime { get; set; } = DateTime.Now; // 첫 dicom file
        [Indexed] // 정렬에 사용 - https://bitbucket.org/alkee_skia/mars3/issues/711
        public DateTime lastModifiedDateTime { get; set; } = DateTime.Now; // 마지막 dicom file

        #endregion denormalized

        [Indexed]
        public string tag { get; set; }

        public string memo { get; set; }
    }

    #endregion DICOM

    #region AR items

    /// <summary>
    ///     AR Series 를 구성하는 ArFile 들의 종류
    /// </summary>
    [StoreAsText]
    public enum ArFileType // 정확히는 ArSeriesType 이 맞지만, ArFileType 이 더 직관적이라 사용
    {
        UNKNOWN = 0,
        CT_IMAGES,
        MR_IMAGES,
        SKIN_MESH,
        MARKER_MESH,
        LESION_MESH,
    }

    [SqliteTable]
    public class ArFile
    {
        #region Keys

        [PrimaryKey, AutoIncrement]
        public long id { get; set; } // same as ROW_ID

        [Indexed]
        public long arStudyId { get; set; }

        [Indexed]
        public long arSeriesId { get; set; }

        [Indexed]
        public long? refDicomFileId { get; set; }

        #endregion Keys

        public int index { get; set; } // 0 부터 시작

        public string filePath { get; set; }

        public DateTime createdDateTime { get; set; } = DateTime.Now;
        public DateTime lastModifiedDateTime { get; set; } = DateTime.Now;

        public string metaJson { get; set; }
    }

    [SqliteTable]
    public class ArSeries // dicom series 처럼 study 의 하위개념및 file을 묶는 단위이지, dicom series 와 직접적인 관련이 없음
    {
        #region Keys

        [PrimaryKey, AutoIncrement]
        public long id { get; set; } // same as ROW_ID

        [Indexed]
        public long arStudyId { get; set; }

        #endregion Keys

        public ArFileType dataType { get; set; }
        public int requiredFileCount { get; set; }

        // optional
        public long? dicomSeriesId { get; set; }
        public string transformMatrix { get; set; }
        public string metaJson { get; set; }
    }

    [SqliteTable]
    public class ArStudy
    {
        // 주의 : raw value(int)가 native plugin 의 CSkinSeg::BodyPart enum 과 값이 같아야 한다.(initSkinSeg 함수의 parameter)
        public enum RegistrationTarget
        {
            DEFAULT, // app 의 기본 설정값에 따름( skia-breast 라면 CHEST, skia-cmf 라면 HEAD 와 같은 식)
            CHEST,
            HEAD,
            ABDOMEN,
        }

        #region Keys

        [PrimaryKey, AutoIncrement]
        public long id { get; set; } // same as ROW_ID

        [Indexed]
        public long dicomStudyId { get; set; }

        [Indexed]
        public long mainDicomSeriesId { get; set; }

        #endregion Keys

        // states
        public bool deleted { get; set; } = false;

        public DateTime createdDateTime { get; set; } = DateTime.Now;
        [Indexed]
        public DateTime lastModifiedDateTime { get; set; } = DateTime.Now;

        [Indexed]
        public DateTime? uploadCompletedDateTime { get; set; } = null;

        public float? ctVolumeResolutionZ { get; set; } = null; // DICOM 의 sliceThickness 와 다른 경우 존재(#603)
        
        public RegistrationTarget registrationTarget { get; set; } = RegistrationTarget.DEFAULT;


        // 미지원
        //// 사용자에 의해 작성 가능
        //[Collation("NOCASE")]
        //public string referringPhysician { get; set; } // 진료의사. DICOM 의 정보 누락으로 인해, 별도(사용자에 의해) 작성되어야할 수 있음
        //public DateTime? operationDateTime { get; set; }

        [Indexed]
        public string tag { get; set; }
        public string memo { get; set; }

        public string metaJson { get; set; }
    }

    #endregion AR items

    #region UserData

    /// <summary>
    ///     DicomSeries 에 종속적인 UserFile
    /// </summary>
    [SqliteTable]
    public class DicomSeriesUserFile
    {
        public enum Type
        {
            SKIN_VOLUME = 11,
            LESION_VOLUME = 12,
        }

        #region Keys

        [PrimaryKey, AutoIncrement]
        public long id { get; set; } // same as ROW_ID

        [Indexed]
        public long dicomSeriesId { get; set; }

        #endregion Keys

        public Type type { get; set; }
        public string creator { get; set; } // null 인경우 서버생성
        public DateTime createDateTime { get; set; } = DateTime.Now; // volume 은 수정되는경우 제거하고 새로 만들기(id 변경)

        public string filePath { get; set; }
    }

    [SqliteTable]
    public class RecordFile
    {
        [StoreAsText]
        public enum RecordFileType
        {
            UNKNOWN = 0,
            COMPRESSED_DIRECTORY, // 결과파일 전체(결과 directory) 압축(.zip) 파일
                                  //LOG,
                                  //SOURCE_SCENE_MESH, // surface matching 에 사용된 scene.obj
                                  //SCREENSHOT,
                                  //DEPTH_MAP,
                                  //SCANNED_MESH, // color scanning 된 mesh.zip
        }

        #region Keys

        [PrimaryKey, AutoIncrement]
        public long id { get; set; } // same as ROW_ID

        [Indexed]
        public long arStudyId { get; set; }

        [Indexed]
        public string accountId { get; set; } // uploader

        #endregion Keys

        public string recordName { get; set; } // 하나의 arStudy 에서도 여러 matching 결과를 구분하기 위해 (YYYYMMDDHHmmSS 형식)

        public RecordFileType fileType { get; set; }

        public string filePath { get; set; }

        public DateTime createdDateTime { get; set; } = DateTime.Now;
        public DateTime lastModifiedDateTime { get; set; } = DateTime.Now;
    }

    #endregion UserData

    // DICOM 이나 AR data 의 정보 이외에, DICOM processor 나 iPad 에서 필요로하는 추가 정보들
    #region ETC

    [SqliteTable]
    public class ReviewStatus
    {
        #region Keys

        [PrimaryKey, AutoIncrement]
        public long id { get; set; } // same as ROW_ID

        [Indexed]
        public long dicomSeriesId { get; set; }

        [Indexed]
        public string accountId { get; set; }

        #endregion Keys

        public DateTime lastReviewedDateTime { get; set; }
        public string comment { get; set; }
        public string assignee { get; set; } // who's in charge ?
    }

    [SqliteTable]
    public class Roi
    {
        #region Keys

        [PrimaryKey, AutoIncrement]
        public long id { get; set; } // same as ROW_ID

        [Indexed]
        public long dicomSeriesId { get; set; } // dicom series 기반

        [Indexed]
        public string accountId { get; set; } // creator/owner

        #endregion Keys

        public string type { get; set; } = "default"; // shape or class(struct) name

        // 주로 숫자값이 포함되겠지만, db 에서 숫자로 저장한다고 얻을 수 있는 이득(검색, 통계 등)이 없으므로
        // string 으로 자유로운 형식으로 저장하도록 하여 확장성을 높임. json 데이터를 사용할 것으로 기대
        public string data { get; set; } // center, transform, color 등의 정보가 포함된 json 사용 추천

        public DateTime createdDateTime { get; set; } = DateTime.Now;
        public DateTime lastModifiedDateTime { get; set; } = DateTime.Now;
        public string comment { get; set; }
    }

    [SqliteTable]
    public class Feature
    {
        [StoreAsText]
        public enum DataType
        {
            UNKNOWN = 0,
            VECTOR3,
        }

        #region Keys

        [PrimaryKey, AutoIncrement]
        public long id { get; set; } // same as ROW_ID

        [Indexed]
        public long dicomSeriesId { get; set; } // dicom series 기반

        #endregion Keys

        public string name { get; set; } = "default";

        public DataType dataType { get; set; } = DataType.VECTOR3;
        public string data { get; set; } // json 형태의 feature 데이터(type 에 따라 달라질 수 있을 것)
    }

    #endregion ETC

    #region Server-Client shared data types

    // Controller 에 종속적인 class 이지만 mars-processor 와의 공유(orm.cs 파일 그대로 사용)를 위해 편의상 Mars.v2.db namespace 로 사용

    /// <summary>
    ///     Dicom prosessor 에서 사용할 study-series 묶음. (query 에 대한 응답)
    /// </summary>
    public class WorkItem // AR 정보가 빠져 rename 이 필요할 듯 한데...
    {
        public class Subitem
        {
            public DicomSeries DicomSeries { get; set; }
            /// <summary>
            ///     유저에 의해 변경된 상태 정보(Review state, AR data uploaded state 등)
            /// </summary>
            public string StatusMessage { get; set; } // 임시. #460 ; 클라이언트의 status 표시 logic 부담을 덜기 위해
            /// <summary>
            ///     서버의 processing 상태 정보
            /// </summary>
            /// <remarks>
            /// null 인 경우 모든 process 가 끝나 이용가능 https://bitbucket.org/alkee_skia/mars3/issues/641/dicom-series-processing-status#comment-60906233
            /// </remarks>
            public string WorkingStatus { get; set; }
            /// <summary>
            ///     이 series를 점유하고있는 사용자 계정. null 인 경우 점유하고있는 사용자가 없음.
            /// </summary>
            public string ReviewingAccountId { get; set; }
        }

        public DicomStudy DicomStudy { get; set; }
        /// <summary>
        ///     이 DicomStudy 에 속한 DicomSeries 의 정보들
        /// </summary>
        public List<Subitem> Subitems { get; set; }
    }

    #endregion Non database table
}

#pragma warning restore IDE1006
//#endif