// scripts/check-ollama-server.ts
import { exec } from 'child_process';
import * as dotenv from 'dotenv';
import { OllamaTagsResponse } from './run-ollama-model';

// 환경 변수 로드
dotenv.config();

// Ollama 설정
const OLLAMA_HOST = process.env.OLLAMA_HOST || 'http://localhost:11434';
const OLLAMA_MODEL = process.env.OLLAMA_MODEL || 'gemma3:12b';

async function checkOllamaServer(): Promise<boolean> {
  try {
    const response = await fetch(`${OLLAMA_HOST}/api/tags`);
    return response.ok;
  } catch (error) {
    return false;
  }
}

async function main() {
  console.log(`Checking Ollama server at ${OLLAMA_HOST}...`);
  
  // 서버 상태 확인
  const isRunning = await checkOllamaServer();
  
  if (isRunning) {
    console.log('Ollama server is already running.');
  } else {
    console.log('Starting Ollama server...');
    
    // 백그라운드에서 Ollama 서버 시작
    exec('ollama serve > ollama-server.log 2>&1 &', async (error) => {
      if (error) {
        console.error('Failed to start Ollama server:', error);
        process.exit(1);
      }
      
      // 서버 시작 대기
      let serverStarted = false;
      for (let i = 0; i < 30; i++) {
        console.log(`Waiting for Ollama server to start (${i+1}/30)...`);
        await new Promise(resolve => setTimeout(resolve, 1000));
        
        if (await checkOllamaServer()) {
          serverStarted = true;
          break;
        }
      }
      
      if (serverStarted) {
        console.log('Ollama server started successfully.');
        
        // 모델 확인
        try {
          const response = await fetch(`${OLLAMA_HOST}/api/tags`);
          const data = await response.json() as OllamaTagsResponse;
          const models = data.models || [];
          
          if (!models.some((model: any) => model.name === OLLAMA_MODEL)) {
            console.log(`Model ${OLLAMA_MODEL} not found. Please pull it manually with: ollama pull ${OLLAMA_MODEL}`);
          } else {
            console.log(`Model ${OLLAMA_MODEL} is available.`);
          }
        } catch (error) {
          console.error('Error checking models:', error);
        }
      } else {
        console.error('Failed to start Ollama server after 30 seconds.');
        process.exit(1);
      }
    });
  }
}

main().catch(error => {
  console.error('Error:', error);
  process.exit(1);
});