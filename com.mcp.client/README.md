# MCP Client for Unity

Model Context Protocol (MCP) Client는 AI 모델을 활용하여 Unity 에디터 내에서 코드 생성 및 수정을 도와주는 확장 도구입니다.

## 간단한 설치 방법

### 옵션 1: Unity Package Manager 사용 (권장)

1. Unity 프로젝트 열기
2. Window > Package Manager 메뉴 선택
3. 좌측 상단의 "+" 버튼 클릭
4. "Add package from git URL..." 선택
5. 다음 URL 입력: `https://github.com/사용자명/MCP-Client.git?path=com.mcp.client`
6. "Add" 버튼 클릭

### 옵션 2: 직접 다운로드

1. 이 저장소를 다운로드 또는 복제합니다
2. Unity 프로젝트의 `Packages` 폴더에 `com.mcp.client` 디렉토리를 복사합니다

## 시작하기

1. MCP 서버 시작:

```bash
cd mcp-server
npm install
npm run start:dev
```

2. Unity에서 MCP 클라이언트 열기:
   Window > AI > Improved MCP Client

3. 서버에 자동으로 연결됩니다. (기본 주소: ws://localhost:8765)

## 주요 기능

- 자연어로 Unity C# 스크립트 생성
- 기존 코드 수정 및 개선
- 다양한 코드 템플릿 제공
- 생성된 코드 미리보기 및 적용

## 필수 요구사항

- Unity 2020.3 이상
- npm이 설치된 Node.js 환경 (MCP 서버용)

모든 필요한 의존성이 패키지에 포함되어 있어 별도 설치가 필요 없습니다.
