// src/types/index.ts
import WebSocket from 'ws';
import { EventEmitter } from 'events';

// 클라이언트 메시지 인터페이스
export interface ClientMessage {
  command: string;       // 'generate', 'chat', 'reset' 등
  conversationId?: string; // 대화 ID (선택적)
  message: string;       // 사용자 메시지
  options?: {
    stream?: boolean;    // 스트리밍 활성화 여부
    temperature?: number; // 생성 온도
    maxTokens?: number;  // 최대 토큰 수
    model?: string;      // 사용할 모델 (선택적)
  };
}

// 서버 응답 인터페이스
export interface ServerResponse {
  status: 'success' | 'error';
  result?: string | any;
  error?: string;
  sessionId?: string;
  conversationId?: string;
}

// Ollama 요청 인터페이스
export interface OllamaRequest {
  model: string;
  prompt: string;
  system: string,
  stream?: boolean;
  options?: {
    temperature?: number;
    num_predict?: number;
    [key: string]: any;
  };
  context?: number[];
}

// Ollama 응답 인터페이스
export interface OllamaResponse {
  model: string;
  created_at: string;
  response: string;
  done: boolean;
  context?: number[];
  total_duration?: number;
  load_duration?: number;
  sample_count?: number;
  sample_duration?: number;
  prompt_eval_count?: number;
  prompt_eval_duration?: number;
  eval_count?: number;
  eval_duration?: number;
}

// 클라이언트 세션 인터페이스
export interface ClientSession {
  id: string;
  socket: WebSocket;
  lastActivity: number;
  conversations: Map<string, Conversation>;
  requestCount: number;
  requestResetTime: number;
}

// 대화 인터페이스
export interface Conversation {
  id: string;
  history: Array<{ role: 'user' | 'assistant'; content: string }>;
  context?: number[];
}

// 설정 인터페이스
export interface Config {
  PORT: number;
  HOST: string;
  OLLAMA_HOST: string;
  OLLAMA_MODEL: string;
  RATE_LIMIT_WINDOW: number;
  RATE_LIMIT_MAX_REQUESTS: number;
  API_KEY_ENABLED: boolean;
  API_KEY: string;
  LOG_LEVEL: 'debug' | 'info' | 'warn' | 'error';
  MAX_HISTORY_LENGTH: number;
}

// 로거 인터페이스
export interface ILogger {
  debug(message: string, ...args: any[]): void;
  info(message: string, ...args: any[]): void;
  warn(message: string, ...args: any[]): void;
  error(message: string, ...args: any[]): void;
}

// 레이트 리미터 인터페이스
export interface IRateLimiter {
  checkLimit(sessionId: string): boolean;
}

// 인증 관리자 인터페이스
export interface IAuthManager {
  authenticate(key: string | undefined): boolean;
}

// 대화 관리자 인터페이스
export interface IConversationManager {
  getOrCreateConversation(conversationId: string): Conversation;
  addMessage(conversationId: string, role: 'user' | 'assistant', content: string): void;
  getContext(conversationId: string): number[] | undefined;
  setContext(conversationId: string, context: number[]): void;
}

// Ollama 클라이언트 인터페이스
export interface IOllamaClient {
  generateCompletion(
    prompt: string, 
    options?: {
      model?: string;
      stream?: boolean;
      temperature?: number;
      maxTokens?: number;
      context?: number[];
    }
  ): Promise<OllamaResponse | EventEmitter>;
}

// WebSocket 핸들러 인터페이스
export interface IWebSocketHandler {
  handleConnection(socket: WebSocket, request: any): void;
  handleMessage(sessionId: string, data: WebSocket.Data): Promise<void>;
  handleDisconnect(sessionId: string): void;
  getSessions(): Map<string, ClientSession>;
}
// Unity 코드 수정 관련 타입
export interface CodeModification {
  filePath: string;    // 수정할 파일 경로
  content: string;     // 새로운 파일 내용 또는 변경할 내용
  operation: 'create' | 'modify' | 'delete';  // 수행할 작업
  targetSection?: string;  // 수정할 경우 대상 섹션 (선택적)
}

export interface UnityCodeManager {
  readFile(relativePath: string): string;
  writeFile(relativePath: string, content: string): void;
  listFiles(relativePath?: string): string[];
  getProjectPath(): string;
}

export interface CodeModificationParser {
  parseAIResponse(text: string): CodeModification[];
  modifySection(originalContent: string, sectionName: string, newContent: string): string;
}