// test-ollama-connection.ts 수정
import axios from 'axios';

async function testOllamaConnection() {
  const OLLAMA_BASE_URL = 'http://localhost:11434';
  const MODEL_NAME = 'gemma3:12b'; // 모델명 확인

  try {
    // 버전 확인
    const versionResponse = await axios.get(`${OLLAMA_BASE_URL}/api/version`);
    console.log('Ollama 버전:', versionResponse.data);

    // 모델 목록 확인
    const modelsResponse = await axios.get(`${OLLAMA_BASE_URL}/api/tags`);
    console.log('사용 가능한 모델:', modelsResponse.data);

    // 생성 요청 테스트
    console.log('생성 요청 시도 중...');
    const generateResponse = await axios.post(`${OLLAMA_BASE_URL}/api/generate`, {
      model: MODEL_NAME,
      prompt: '안녕하세요, 테스트 메시지입니다.',
      stream: false
    });

    console.log('생성 응답:', generateResponse.data);
    console.log('Ollama API 연결 테스트 성공!');
  } catch (error:any) {
    console.error('Ollama API 연결 실패:', error.message);
    if (error.response) {
      console.error('응답 상태:', error.response.status);
      console.error('응답 데이터:', error.response.data);
    }
  }
}

testOllamaConnection();