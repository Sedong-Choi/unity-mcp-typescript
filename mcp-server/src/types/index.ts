// src/types/index.ts
import WebSocket from 'ws';
import { EventEmitter } from 'events';

// 클라이언트 메시지 인터페이스
export interface ClientMessage {
  command: string;
  sessionId?: string;
  conversationId?: string;
  options?: {
    stream?: boolean;
    temperature?: number;
    maxTokens?: number;
    [key: string]: any;
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