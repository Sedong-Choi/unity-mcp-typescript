import { spawn } from 'child_process';
import * as readline from 'readline';
import * as dotenv from 'dotenv';
import * as path from 'path';
import * as fs from 'fs';

// 환경 변수 로드
dotenv.config();

// Ollama 모델 설정
const OLLAMA_MODEL = process.env.OLLAMA_MODEL || 'gemma3:12b';
const OLLAMA_HOST = process.env.OLLAMA_HOST || 'http://localhost:11434';

// Ollama 서버 상태 확인 함수
async function checkOllamaServer(): Promise<boolean> {
  try {
    const response = await fetch(`${OLLAMA_HOST}/api/tags`);
    return response.ok;
  } catch (error) {
    return false;
  }
}

// Ollama 모델 실행 함수
async function runOllamaModel() {
  console.log(`Checking if Ollama server is running at ${OLLAMA_HOST}...`);
  
  // Ollama 서버가 실행 중인지 확인
  const isServerRunning = await checkOllamaServer();
  
  if (!isServerRunning) {
    console.log('Ollama server is not running. Starting Ollama server...');
    
    // Ollama 서버 시작 (플랫폼에 맞게 경로 조정 필요)
    const ollamaProcess = spawn('ollama', ['serve'], {
      detached: true,
      stdio: 'pipe'
    });
    
    // 로그 처리
    ollamaProcess.stdout.on('data', (data) => {
      console.log(`Ollama server: ${data.toString().trim()}`);
    });
    
    ollamaProcess.stderr.on('data', (data) => {
      console.error(`Ollama server error: ${data.toString().trim()}`);
    });
    
    // 서버가 시작될 때까지 기다림
    console.log('Waiting for Ollama server to start...');
    let serverStarted = false;
    for (let i = 0; i < 30; i++) {
      await new Promise(resolve => setTimeout(resolve, 1000));
      const check = await checkOllamaServer();
      if (check) {
        serverStarted = true;
        break;
      }
    }
    
    if (!serverStarted) {
      console.error('Failed to start Ollama server after 30 seconds.');
      process.exit(1);
    }
  }
  
  console.log(`Ollama server is running. Checking if model ${OLLAMA_MODEL} is available...`);
  
  // 모델 가용성 확인
  try {
    const response = await fetch(`${OLLAMA_HOST}/api/tags`);
    const data = await response.json();
    const models = data.models || [];
    const modelExists = models.some((model: any) => model.name === OLLAMA_MODEL);
    
    if (!modelExists) {
      console.log(`Model ${OLLAMA_MODEL} is not available. Pulling the model...`);
      
      // 모델 풀
      const pullProcess = spawn('ollama', ['pull', OLLAMA_MODEL], {
        stdio: 'inherit'
      });
      
      await new Promise((resolve, reject) => {
        pullProcess.on('close', (code) => {
          if (code === 0) {
            console.log(`Successfully pulled model ${OLLAMA_MODEL}`);
            resolve(code);
          } else {
            console.error(`Failed to pull model ${OLLAMA_MODEL}`);
            reject(code);
          }
        });
      });
    } else {
      console.log(`Model ${OLLAMA_MODEL} is already available.`);
    }
  } catch (error) {
    console.error('Error checking model availability:', error);
    process.exit(1);
  }
  
  // CLI 상호작용 설정
  console.log(`\n=== ${OLLAMA_MODEL} CLI Mode ===`);
  console.log('Type your prompts and press Enter. Type "exit" to quit.\n');
  
  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    prompt: '> '
  });
  
  rl.prompt();
  
  rl.on('line', async (line) => {
    const input = line.trim();
    
    if (input.toLowerCase() === 'exit') {
      rl.close();
      return;
    }
    
    if (input) {
      try {
        // API를 통해 Ollama에 직접 요청
        const response = await fetch(`${OLLAMA_HOST}/api/generate`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify({
            model: OLLAMA_MODEL,
            prompt: input,
            stream: false
          })
        });
        
        const data = await response.json();
        
        if (data.response) {
          console.log(`\n${data.response}\n`);
        } else {
          console.log('\nNo response from model\n');
        }
      } catch (error) {
        console.error('Error communicating with Ollama:', error);
      }
    }
    
    rl.prompt();
  });
  
  rl.on('close', () => {
    console.log('CLI session ended. Server continues running.');
    process.exit(0);
  });
}

// 스크립트 실행
runOllamaModel().catch(error => {
  console.error('Error in runOllamaModel:', error);
  process.exit(1);
});