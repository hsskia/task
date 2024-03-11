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
DICOM은 의료 영상에서 정보를 처리, 저장, 통신할 때 지켜야할 표준 규약으로 DICOM 파일은 이 DICOM 규약을 따른다. 파일의 확장자는 .dcm이고 크게 메타 정보(환자 정보, 이미지 속성 등)와 Object 정보로 나뉜다. DICOM tags는 파일 내의 정보를 식별하는 용도로 사용되고 아래 표와 같이 (그룹 번호, 요소 번호)의 쌍으로 표현한다.

<div align="center">
  <p>
  <img alt="image" src="https://github.com/hsskia/task/assets/162946806/3740b917-3718-46ba-8f8f-2dac7c8fcfc8">
  <br>
  </p>
</div>

- Coordinate system for medical imaging (LPS, RAS, ..): 
- Pixel spacing, Slice thickness
- Image Orientation (Patient)
- Patient Position

### Restful API(Web API)

### ASP .NET

### Docker

### ORM(Object Relational Mapping), code first schema

### AR / VR / XR

## Reference
- [transform matrix 이미지](https://inyongs.tistory.com/132)
- [해부학 자세 이미지](https://m.blog.naver.com/ghippieyogi/220715767904)
