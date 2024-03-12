### Normal Vector
직선/평면의 기울기나 경사각을 표현할 때, 해당 직선/평면에 수직인 벡터. 3D 영상 처리에서 직선/평면의 모든 꼭짓점에서의 normal vector를 구하면 물체를 부드럽게 표현할 수 있다.

### Transform Matrix(4x4)
3D 공간상의 물체에 이동, 회전, 크기 조절 등의 변환을 수행하기 위한 단일 행렬. 한 번의 행렬 곱셈으로 모든 변환을 적용할 수 있기 때문에 효율적인 계산이 가능하다. 좌표 위치를 나타내는 x,y,z에 이동 변환을 위한 차원이 추가되어 4X4 행렬을 사용한다.

<div align="center">
  <p>
  <img width="200" alt="image" src="https://github.com/hsskia/task/assets/162946806/66d0cd9e-a698-40d5-8474-2e4797f9f64b">
  <br>
    이동변환
  </p>
</div>

<br>

<div align="center">
  <p>
  <img width="200" alt="image" src="https://github.com/hsskia/task/assets/162946806/e7d39472-06e2-4bcc-8087-cb19ff0284ca">
  <br>
    회전변환(x축)
  </p>
  <br>
  <p>
  <img width="200" alt="image" src="https://github.com/hsskia/task/assets/162946806/1af25b03-c229-41ac-848f-046c77f08aba">
  <br>
    회전변환(y축)
  </p>
  <br>
  <p>
  <img width="200" alt="image" src="https://github.com/hsskia/task/assets/162946806/51769264-7bd8-4f9c-b9c7-ffd899549e76">
  <br>
    회전변환(z축)
  </p>
</div>

<br>

<div align="center">
  <p>
  <img width="200" alt="image" src="https://github.com/hsskia/task/assets/162946806/32845ed6-fd95-4a49-8832-e65002b6df3d">
  <br>
    크기변환
  </p>
</div>

<br>

<br><br>
    
### Image Segmentation
이미지에서 같은 의미를 가지고 있는 부분을 구분해내는 작업으로 픽셀 단위로 분류가 수행된다. 동일한 의미를 갖는 객체를 모두 동일하게 라벨링하면 semantic segmentation, 객체마다 다른 라벨링을 적용하면 instance segmentation으로 구분할 수 있다.

### Image Registration
크기, 위치, 각도가 다른 이미지들을 매칭 시켜주는 영상 정합 task. 매칭을 위해서 영상 간의 연관 관계를 나타내는 최적의 transformation function(matrix)을 찾는 것이 목표이고 두 영상의 차이를 최소화시키는 transformation function이 최적이라고 표현할 수 있다. 

### Point Cloud
3차원 공간상에 퍼져 있는 여러 point들의 집합. 다양한 좌표 정보를 모아둔 point cloud로 3차원 공간을 표현할 수 있고, 이를 기반으로 classification, detection, segmentation의 task를 수행할 수 있다.   

### Point Cloud Registration
다양한 시점이나 센서에서 수집한 point cloud 데이터를 정렬하여 하나의 공간상에 표현하는 기법. Point Cloud는 2D 데이터와는 달리 정형화 되어있지 않고 순서도 무작위로 표현되기때문에 point간의 관계를 파악하기가 어렵다. 따라서 데이터를 가장 잘 정렬하는 transformation matrix를 찾는 것이 중요한데 서로 대응되는 point를 찾기위해서 ICP, SURF 등과 같은 방법들을 사용할 수 있다.
- Global registration: point cloud 데이터 전체에 대해 변환 행렬을 추정하는데 초점을 맞춘 기법. Point cloud간의 변환이 클 때나 상대적으로 큰 공간을 다룰 때 유용하게 사용할 수 있다. 많은 데이터를 고려하기 때문에 위치 추정이 정확하지 않은 초기 단계에서도 안정적인 결과를 제공한다. 하지만, 많은 데이터를 사용해서 연산량이 많다는 단점 또한 존재한다.  
- Local registration: 작은 지역 영역에 대해서만 변환 행렬을 추정하는 기법. Point cloud가 서로 가까이 위치하고, 변환이 작을 때 유용하게 사용될 수 있다. Global registratino에 비해 변환 정확도가 떨어질 수 있으나 계산 비용이 상대적으로 낮고 필요한 데이터의 양이 적다는 장점이 있다.
- Iterative Closest Point (ICP): 두개의 point cloud가 주어졌을 때, corresponding point 사이의 거리를 계산하여 이 거리를 최소화하는 변환 행렬을 찾는 알고리즘. 쉽게 말해서, 데이터 포인트들을 가장 잘 매칭시키는 크기, 이동, 회전 등의 변환을 찾는 것이다. 좌표마다 다른 좌표와의 모든 거리를 계산하면 비효율적이기 때문에 nearest neighbors 등과 같은 방법을 활용하여 연산량을 줄일 수 있다. 점들 사이의 거리를 최소화하기 위해서는 최소 제곱법을 적용한다.

### ARKit
IOS를 위한 Apple의 AR 개발 플랫폼. IOS11 이상을 요구하고 개발을 위해 Mac에 IOS 앱 개발을 위한 개발환경인 Xcode를 설치해야한다. ARKit은 다른 IOS 프레임워크와 결합되어 애니메이션을 만들거나(SceneKit), ML 모델을 적용(Core ML) 하는 등 다양한 task를 수행할 수 있다.

### SLAM(Simultaneous Localization and Mapping)
동시적 위치추정 및 맵핑의 약자로 현재 객체가 존재하는 위치(localization)와 주변 환경 정보(mapping)를 파악하는 기법이다. 프로그램이 시작된 시점부터 지속적으로 객체의 위치를 추정하면서 위치와 맵핑 정보를 업데이트시킨다. 센서 데이터를 모두 얻은 뒤 Map을 만들면 Offline SLAM, 실시간으로 map과 localization을 수행하면 Online SLAM이다. Kalman Filter, Particle Filter, Graph-based와 같은 방법으로 SLAM의 문제를 풀 수 있다. Kalman Filter는 데이터가 선형/가우시안 분포를 따를 때, Particle Filter와 Graph-based는 데이터가 비선형/비가우시안 일 때 주로 사용한다.

### Anatomical positions(planes) and directions
Anatomical positions란 아래 그림과 같이 양발을 벌리고 똑바로 서서 눈은 정면을 응시한 채로 팔은 몸통 양옆에 두어 손바닥이 정면을 바라보도록 한 자세이다. Anatomical planes은 해부학적 평면을 나타내고 인체를 좌우로 나누는 Sagittal Plane, 앞뒤로 나누는 Coronal Plane, 상하로 나누는 Transverse Plane이 있다. Anatomical directions은 해부학적 방향을 나타내고 신체의 전면은 Anterior(or Ventral), 후면은 Posterior(or Dorsal), 최상위(Cranial, 두개골), 최하위(Caudal, 미골 꼬리뼈)로 나눈다. Superior은 어떠한 부분보다 위에 있을 경우(e.g. 머리는 다리보다 superior), Inferior은 어떠한 부분보다 아래 있는 경우, Medial은 신체를 좌우로 균등하게 나누는 정중앙선을 의미한다. 이외에도 Lateral, Proximal, Distal 등 다양한 용어 존재.

<div align="center">
  <p>
  <img width="200" alt="image" src="https://github.com/hsskia/task/assets/162946806/9e8ec82c-174d-44d0-8deb-2c3d101ad917">
  <br>
  </p>
</div>

### NRRD data format
Near Raw Raster Data의 약자로 이미지나 볼륨 데이터를 저장하기 위한 과학적 데이터 형식이다. 헤더에는 메타 정보(데이터 차원, 크기, 형식 등)가 포함되고 데이터는 바이너리나 텍스트 형식으로 헤더의 설명에 일치하게 구성된다. NRRD 형식은 의료, 컴퓨터 비전 도메인에서 자주 사용된다.

### DICOM, DICOM tags
DICOM은 의료 영상에서 정보를 처리, 저장, 통신할 때 지켜야할 표준 규약으로 DICOM 파일은 이 DICOM 규약을 따른다. 파일의 확장자는 .dcm이고 크게 메타 정보(환자 정보, 이미지 속성 등)와 Object 정보(이미지)로 나뉜다. X-ray, MRI, 초음파 영상이 아래 이미지와 같이 구성될 수 있다.

<div align="center">
  <p>
  <img width = "500" alt="image" src="https://github.com/hsskia/task/assets/162946806/8f7497b9-74c5-4868-8a6d-8eea20c85cf1">
  <br>
  </p>
</div>


DICOM tags는 파일 내의 정보를 식별하는 용도로 사용되고 아래 표와 같이 (그룹 번호, 요소 번호)의 쌍으로 표현한다.

<div align="center">
  <p>
  <img alt="image" src="https://github.com/hsskia/task/assets/162946806/3740b917-3718-46ba-8f8f-2dac7c8fcfc8">
  <br>
  </p>
</div>

- Coordinate system for medical imaging (LPS, RAS, ..): 의료 영상에서 사용되는 좌표 시스템은 크게 world coordinate system, anatomical coordinate system, image coordinate system 세가지가 있다. World coordinate system은 주로 MRI 스캐너나 환자에 적용되는 좌표계로 일반적으로 사용되는 데카르트 좌표계(x,y,z)가 이에 해당한다. Anatomical coordinate system은 해부학적 좌표계를 의미하고 머리와 발(Superior and Inferior), 전면과 후면(Anterior and Posterior), 좌우(Left and Right)를 분리하는 plane에 따라 표현된다. DICOM에서 주로 사용되는 LPS(Left, Posterior, Superior)는 오른쪽으로부터 왼쪽면, 전방으로부터 후방면, 하부로부터 상부면의 방향을 갖는 좌표계로 LPS를 P축 방향으로 90도 눕히면 World coordinate system과 같은 좌표로 표현할 수 있다. RAS(Right, Anterior, Sperior)는 좌우/전후면이 LPS와 반대인 방법으로 좌표를 표현한다. 마지막으로, image coordinate system은 ijk로 좌표를 표현하는 방법으로 인덱스 i는 좌에서 우, j는 위에서 아래, k는 전방에서 후방으로 표현될 수 있습니다.

(왼쪽부터 World/Anatomical/Image coordinate system)

<div align="center">
  <p>
  <img width = "500" alt="image" src="https://github.com/hsskia/task/assets/162946806/ca85ee53-1130-4dff-8c34-2bc06437415a">
  <br>
  </p>
</div>

- Pixel spacing, Slice thickness: Pixel spacing은 CT와 같은 의료데이터에서 한 픽셀의 크기를 의미하고, slice thickness는 CT 데이터 각 절편의 두께를 의미하고 두께가 클수록 인체의 구조를 넓게 보여주지만 슬라이스 간격 간의 정보가 부족할 수 있어 적절한 두께를 선택하는 것이 좋다. Pixel spacing과 Slice thickness 모두 mm 단위로 표현한다.
- Image Orientation (Patient): DICOM에서 의료 이미지가 환자의 실제 물리적인 공간에서 어떻게 위치하는지를 나타내는 메타 데이터다. DICOM tag로는 (0020, 0037)이고 "Ax, Ay, Az, Bx, By, Bz" 6개의 elements로 표현가능하다. 의료 이미지를 실제 환자 위치에 일치시키기 위해서는 어떠한 변환이 필요하기 때문에 주어진 의료 영상 좌표에 이 Image Orientation (Patient)를 적용한다.
- Patient Position: Patient position은 의료 영상에서 환자가 어떤 위치에 있는지를 나타내고 9가지 위치로 나타낼 수 있다. Or DICOM에서 환자의 실제 위치를 나타내는 좌표. 
  - Supine: 환자가 등을 눕힌 상태로 촬영
  - Prone: 복부를 눕힌 상태 촬영
  - Erect: 환자가 서있는 상태
  - Decubitus: 좌 혹은 우 측면으로 누워있는 상태
  - Head First: 머리 먼저 촬영
  - Feet First: 발 먼저 촬영
  - Right Side: 오른쪽 먼저 촬영
  - Left Side: 왼쪽 먼저 촬영

### Restful API(Web API)
API는 어플리케이션 단계에서 사용자의 입력에 따른 정확한 출력을 만들기 위해 필요한 프로그램들간의 소통 규칙이다. 특히 Web API는 컴퓨터들 간의 통신 규약을 의미(강제가 아닌 권고사항)하는데 개발자들이 이 규약을 잘 지키지않아 중요한 규칙을 뽑아서 6개의 가이드라인을 만들었고 이 가이드라인이 Rest API이다. Restful API는 이 Rest API를 모두 충족하는 API이다.

보통 Rest API를 작성했다고 하면 HTTP Request를 다음과 같이 사용한다.

- GET(READ): 데이터 조회(가져옴)
- POST(CREATE): 데이터 생성
- PATCH(UPDATE): 데이터 일부 업데이트
- PUT(UPDATE): 데이터 전체 업데이트
- DELETE(DELETE): 데이터 삭제

### ASP .NET
마이크로소프트에서 개발한 웹 응용 프로그램 개발 프레임워크로 C#과 같은 .NET 언어로 작성된다. 웹 페이지 프레임워크, MVC(Model View Controller)와 같은 기능을 사용할 수 있다. MVC는 Client가 Controller에 task를 요청하면 Controller가 Model에 처리 요청을 하고 Model이 데이터를 처리한 후 다시 Controller가 결과를 받아 View에 전달하고 View에서 결과값을 사용자에게 보여주는 방식으로 동작하는 아키텍처이다. 

<div align="center">
  <p>
  <img width = "500" alt="image" src="https://github.com/hsskia/task/assets/162946806/8fe0e53d-b4b4-45a7-a709-e5c30d7060f4">
  <br>
  </p>
</div>

윈도우 서버와 완벽하게 통합되었고, Visual Studio를 비롯한 다른 Microsoft의 어플리케이션과 결합하여 웹 개발과 배포를 쉽게 할 수 있다.

### Docker
도커(Docker)는 개별환경 중 하나로 애플리케이션 실행 환경을 코드로 작성할 수 있고, 리눅스에서 돌아가는 프로그램을 사용자의 PC에서도 쉽고(docker file을 통해) 빠르게(Container를 통해) 동작할 수 있도록 툴을 제공한다. 개발자들의 운영체제가 달라서 발생할 수 있는 협업의 어려움을 해결할 수 있기 때문에 매우 유용한 개발환경이다. Docker는 크게 Image, Registry, Repository로 구성되어 있다. 

Docker image는 레지스트리에 저장되어 있는 파일이나 프로그램이다. Repository는 Registry내의 도커 이미지가 저장되는 공간이다. 이미지 이름이 사용되기도 하고, Github 레포지토리와 유사한 개념으로 생각하면 쉽다. Registry는 도커 이미지가 관리되는 공간으로 특별히 다른 것을 지정하지 안으면 도커 허브라는 원격 저장소를 기본 레지스트리로 설정한다. 도커 허브 외에도 회사 내부용 레지스트리나 Private Docker Hub등을 사용할 수 있다.

쉽게 말하면 Registry는 Github, Repository는 Github Repository, Image는 Registry 내부에 있는 파일이나 프로그램이다.

- image 가져오기
  
```
docker image pull docker/whalesay:latest
```

- image 실행

```
docker container run --name 컨테이너_이름 docker/whalesay:latest cowsay boo
```

- image 삭제

```
docker image rm docker/whalesay
```

- 컨테이너 삭제
  
```
docker container rm 컨테이너_이름
```

### ORM(Object Relational Mapping), code first schema
OOP(Object Oriented Programming)의 클래스와 관계형 DB의 테이블을 자동으로 연결하는 것을 의미한다. 클래스와 테이블이 기존부터 호환가능성을 두고 만들어지지 않았기때문에 ORM을 통해 SQL문을 자동으로 생성하여 불일치를 해결한다. 따로 SQL문을 짤 필요없이 객체를 통해 간적접으로 DB를 조작할 수 있게 된다. SQL문을 사용해야 하는 번거로움이 사라지고 오직 객체지향만 고려하면 되기때문에 생산성이 증가한다. 하지만, 프로젝트의 복잡성이 커질수록 신중한 설계가 필요하고 설계가 잘못된다면 효율성이 떨어질 수 있다. 클래스와 테이블간 관계가 일치하도록 유의해야한다.

GraphQL은 페이스북에서 개발한 데이터 쿼리 언어로 웹에서 데이터를 효율적으로 가져올 수 있다. 클라이언트가 필요한 데이터를 요청할때 원하는 구조를 명시할 수 있기 때문에 RESTful API보다 유연하고 복잡한 데이터 구조와 관계를 지원하여 효율적으로 데이터를 가져올 수 있다. GraphQL의 resolver는 쿼리의 각 필드에 맞는 데이터를 제공하는 함수로 서버에서 데이터를 가져와서 클라이언트가 요청한 필드의 값을 리턴한다. 이 GraphQL은 사용할 때 code first schema 방법은 스키마를 프로그래밍 방식으로 정의하는 접근법으로 resolver 함수를 먼저 작성하면 스키마가 자동으로 생성된다. Resolver와 스키마를 따로 정의해야하는 번거로움이 사라지고 API를 수정해도 스키마가 동적으로 생성되기 때문에 유지보수에 용이하다. 하지만, 스키마 정의와 resolver가 같이 있어 가독성이 떨어진다는 단점이 존재하기도 하다.

### AR / VR / XR
- AR(Augmented Reality): 기기를 통해 현실에 가상 객체를 매칭시키는 기술(포켓몬 GO, 자동차 유리에 비춰지는 네비게이션)
- VR(Virtual Reality): 완전한 가상 환경을 구현한 기술(실제와 비슷하거나 완전히 다를 수 있음)
- XR(eXtended Reality): AR, VR, MR을 모두 포함하고 현실 공간에 배치된 가상의 물체를 조작하는 기술 


## Image Reference
- [transform matrix 이미지](https://inyongs.tistory.com/132)
- [해부학 자세 이미지](https://m.blog.naver.com/ghippieyogi/220715767904)
- [DICOM 이미지](https://artiiicy.tistory.com/63)
- [DICOM Tag 이미지1](https://89douner.tistory.com/294)
- [DICOM Tag 이미지2](https://www.dicomlibrary.com/dicom/dicom-tags/)
- [Coordinate system 이미지](https://slicer.readthedocs.io/en/latest/user_guide/coordinate_systems.html)
- [MVC 이미지](https://devkingdom.tistory.com/337)

