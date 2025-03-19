// test-ollama-connection.ts
import axios from 'axios';
import config from './src/config';

const {
  OLLAMA_HOST, OLLAMA_MODEL
} = config;

async function testOllamaConnection() {
  try {
    // Ollama 버전 확인
    const versionResponse = await axios.get(`${OLLAMA_HOST}/api/version`);
    console.log('Ollama 버전:', versionResponse.data);

    // 간단한 생성 요청 테스트
    const generateResponse = await axios.post(`${OLLAMA_HOST}/api/generate`, {
      model: OLLAMA_MODEL,
      prompt: '안녕하세요, 테스트 메시지입니다.',
      stream: false
    });

    console.log('생성 응답:', generateResponse.data);
    console.log('Ollama API 연결 테스트 성공!');
  } catch (error) {
    console.error('Ollama API 연결 실패:', error);
  }
}

testOllamaConnection();