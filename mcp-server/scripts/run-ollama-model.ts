// scripts/run-ollama-model.ts
import dotenv from 'dotenv';
import { spawn } from 'child_process';

// .env 파일 로드
dotenv.config();

// 모델 이름 가져오기
const modelName = process.env.OLLAMA_MODEL;

if (!modelName) {
  console.error('오류: .env 파일에 OLLAMA_MODEL이 정의되지 않았습니다.');
  console.error('예: OLLAMA_MODEL=gemma3:12b');
  process.exit(1);
}

console.log(`Ollama 모델 실행: ${modelName}`);

// Ollama 프로세스 시작
const ollamaProcess = spawn('ollama', ['run', modelName], {
  stdio: 'inherit' // 표준 입출력을 현재 프로세스와 연결
});

// 프로세스 종료 시 처리
ollamaProcess.on('close', (code) => {
  console.log(`Ollama 프로세스가 종료되었습니다. 종료 코드: ${code}`);
});

// SIGINT (Ctrl+C) 처리
process.on('SIGINT', () => {
  console.log('Ollama 프로세스를 중단합니다...');
  ollamaProcess.kill('SIGINT');
});