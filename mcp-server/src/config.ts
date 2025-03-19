// src/config.ts
import dotenv from 'dotenv';
import { Config } from './types';

// 환경 변수 로드
dotenv.config();

// 기본 설정값
const config: Config = {
  // 서버 설정
  PORT: parseInt(process.env.PORT || '8765', 10),
  HOST: process.env.HOST || '0.0.0.0',
  
  // Ollama 설정
  OLLAMA_HOST: process.env.OLLAMA_HOST || 'http://localhost:11434',
  OLLAMA_MODEL: process.env.OLLAMA_MODEL || 'gemma:12b',
  
  // 레이트 리밋 설정
  RATE_LIMIT_WINDOW: parseInt(process.env.RATE_LIMIT_WINDOW || '60000', 10), // 1분
  RATE_LIMIT_MAX_REQUESTS: parseInt(process.env.RATE_LIMIT_MAX_REQUESTS || '20', 10), // 1분당 20요청
  
  // API 키 인증 (선택적)
  API_KEY_ENABLED: process.env.API_KEY_ENABLED === 'true',
  API_KEY: process.env.API_KEY || '',

  // 로깅 설정
  LOG_LEVEL: (process.env.LOG_LEVEL || 'info') as 'debug' | 'info' | 'warn' | 'error',
  
  // 컨텍스트 설정
  MAX_HISTORY_LENGTH: parseInt(process.env.MAX_HISTORY_LENGTH || '10', 10),
};

export default config;