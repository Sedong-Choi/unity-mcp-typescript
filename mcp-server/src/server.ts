import * as WebSocket from 'ws';
import * as http from 'http';
import * as express from 'express';
import axios from 'axios';
import dotenv from 'dotenv';
import * as fs from 'fs';
import * as path from 'path';
import { EventEmitter } from 'events';

// 환경 변수 로드
dotenv.config();

// 기본 설정값
const CONFIG = {
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
  LOG_LEVEL: process.env.LOG_LEVEL || 'info', // 'debug', 'info', 'warn', 'error'
  
  // 컨텍스트 설정
  MAX_HISTORY_LENGTH: parseInt(process.env.MAX_HISTORY_LENGTH || '10', 10),
};

// 타입 정의
interface ClientMessage {
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

interface ServerResponse {
  status: 'success' | 'error';
  result?: string | any;
  error?: string;
  sessionId?: string;
  conversationId?: string;
}

interface OllamaRequest {
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

interface OllamaResponse {
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

interface ClientSession {
  id: string;
  socket: WebSocket;
  lastActivity: number;
  conversations: Map<string, Conversation>;
  requestCount: number;
  requestResetTime: number;
}

interface Conversation {
  id: string;
  history: Array<{ role: 'user' | 'assistant'; content: string }>;
  context?: number[];
}

/**
 * 로깅 시스템
 */
class Logger {
  private static logLevels = {
    debug: 0,
    info: 1,
    warn: 2,
    error: 3
  };

  private static currentLevel = Logger.logLevels[CONFIG.LOG_LEVEL as keyof typeof Logger.logLevels] || 1;

  static debug(message: string, ...args: any[]): void {
    if (Logger.currentLevel <= Logger.logLevels.debug) {
      console.debug(`[DEBUG] ${new Date().toISOString()} - ${message}`, ...args);
    }
  }

  static info(message: string, ...args: any[]): void {
    if (Logger.currentLevel <= Logger.logLevels.info) {
      console.info(`[INFO] ${new Date().toISOString()} - ${message}`, ...args);
    }
  }

  static warn(message: string, ...args: any[]): void {
    if (Logger.currentLevel <= Logger.logLevels.warn) {
      console.warn(`[WARN] ${new Date().toISOString()} - ${message}`, ...args);
    }
  }

  static error(message: string, ...args: any[]): void {
    if (Logger.currentLevel <= Logger.logLevels.error) {
      console.error(`[ERROR] ${new Date().toISOString()} - ${message}`, ...args);
    }
  }
}

/**
 * 레이트 리미터 클래스 - 과도한 요청 제한
 */
class RateLimiter {
  private sessions = new Map<string, { count: number, resetTime: number }>();
  private readonly windowMs: number;
  private readonly maxRequests: number;

  constructor(windowMs: number, maxRequests: number) {
    this.windowMs = windowMs;
    this.maxRequests = maxRequests;
  }

  checkLimit(sessionId: string): boolean {
    const now = Date.now();
    const sessionData = this.sessions.get(sessionId);

    // 세션이 없거나 리셋 시간이 지났으면 새 세션 생성
    if (!sessionData || now > sessionData.resetTime) {
      this.sessions.set(sessionId, {
        count: 1,
        resetTime: now + this.windowMs
      });
      return true;
    }

    // 요청 수 증가
    sessionData.count++;

    // 한도 초과 확인
    if (sessionData.count > this.maxRequests) {
      Logger.warn(`Rate limit exceeded for session ${sessionId}`);
      return false;
    }

    return true;
  }
}

/**
 * 인증 관리자 클래스
 */
class AuthManager {
  private readonly enabled: boolean;
  private readonly apiKey: string;

  constructor(enabled: boolean, apiKey: string) {
    this.enabled = enabled;
    this.apiKey = apiKey;
  }

  authenticate(key: string | undefined): boolean {
    if (!this.enabled) return true;
    return key === this.apiKey;
  }
}

/**
 * 대화 컨텍스트 관리 클래스
 */
class ConversationManager {
  private conversations = new Map<string, Conversation>();
  private readonly maxHistoryLength: number;

  constructor(maxHistoryLength: number) {
    this.maxHistoryLength = maxHistoryLength;
  }

  getOrCreateConversation(conversationId: string): Conversation {
    if (!this.conversations.has(conversationId)) {
      this.conversations.set(conversationId, {
        id: conversationId,
        history: [],
      });
    }
    return this.conversations.get(conversationId) as Conversation;
  }

  addMessage(conversationId: string, role: 'user' | 'assistant', content: string): void {
    const conversation = this.getOrCreateConversation(conversationId);
    
    conversation.history.push({ role, content });
    
    // 최대 기록 길이 제한
    if (conversation.history.length > this.maxHistoryLength) {
      conversation.history = conversation.history.slice(
        conversation.history.length - this.maxHistoryLength
      );
    }
  }

  getContext(conversationId: string): number[] | undefined {
    const conversation = this.conversations.get(conversationId);
    return conversation?.context;
  }

  setContext(conversationId: string, context: number[]): void {
    const conversation = this.getOrCreateConversation(conversationId);
    conversation.context = context;
  }
}

/**
 * Ollama API 클라이언트 클래스
 */
class OllamaClient {
  private readonly baseUrl: string;
  private readonly defaultModel: string;
  private readonly eventEmitter: EventEmitter;

  constructor(baseUrl: string, defaultModel: string) {
    this.baseUrl = baseUrl;
    this.defaultModel = defaultModel;
    this.eventEmitter = new EventEmitter();
  }

  async generateCompletion(
    prompt: string, 
    options: {
      model?: string, 
      stream?: boolean,
      temperature?: number,
      maxTokens?: number,
      context?: number[]
    } = {}
  ): Promise<OllamaResponse | EventEmitter> {
    const model = options.model || this.defaultModel;
    const isStream = options.stream === true;
    
    try {
      const requestData: OllamaRequest = {
        model,
        prompt,
        stream: isStream,
        options: {}
      };

      // 온도 설정 (있는 경우)
      if (options.temperature !== undefined) {
        requestData.options!.temperature = options.temperature;
      }

      // 최대 토큰 설정 (있는 경우)
      if (options.maxTokens !== undefined) {
        requestData.options!.num_predict = options.maxTokens;
      }

      // 컨텍스트 설정 (있는 경우)
      if (options.context && options.context.length > 0) {
        requestData.context = options.context;
      }

      Logger.debug(`Sending request to Ollama API: ${JSON.stringify(requestData)}`);

      if (isStream) {
        // 스트리밍 모드
        const response = await axios.post(`${this.baseUrl}/api/generate`, requestData, {
          responseType: 'stream'
        });

        // 청크 처리를 위한 설정
        response.data.on('data', (chunk: Buffer) => {
          try {
            const lines = chunk.toString().split('\n').filter(line => line.trim());
            
            for (const line of lines) {
              const data = JSON.parse(line);
              this.eventEmitter.emit('data', data);
              
              if (data.done) {
                this.eventEmitter.emit('end', data);
              }
            }
          } catch (error) {
            Logger.error('Error parsing streaming chunk:', error);
            this.eventEmitter.emit('error', error);
          }
        });

        response.data.on('error', (error: Error) => {
          Logger.error('Stream error:', error);
          this.eventEmitter.emit('error', error);
        });

        return this.eventEmitter;
      } else {
        // 비스트리밍 모드
        const response = await axios.post(`${this.baseUrl}/api/generate`, requestData);
        return response.data as OllamaResponse;
      }
    } catch (error) {
      Logger.error(`Ollama API 오류: ${error}`);
      
      if (axios.isAxiosError(error) && error.response) {
        throw new Error(`Ollama API 오류: ${error.response.status} - ${JSON.stringify(error.response.data)}`);
      } else {
        throw new Error(`Ollama API 요청 실패: ${error}`);
      }
    }
  }
}

/**
 * WebSocket 서버 클래스
 */
class MCPServer {
  private server: http.Server;
  private wss: WebSocket.Server;
  private ollamaClient: OllamaClient;
  private sessions: Map<string, ClientSession> = new Map();
  private rateLimiter: RateLimiter;
  private authManager: AuthManager;
  private conversationManager: ConversationManager;
  private app: express.Application;

  constructor() {
    // Express 앱 초기화
    this.app = express();
    this.setupExpress();
    
    // HTTP 서버 생성
    this.server = http.createServer(this.app);
    
    // WebSocket 서버 초기화
    this.wss = new WebSocket.Server({ server: this.server });
    
    // Ollama 클라이언트 초기화
    this.ollamaClient = new OllamaClient(
      CONFIG.OLLAMA_HOST,
      CONFIG.OLLAMA_MODEL
    );
    
    // 레이트 리미터 초기화
    this.rateLimiter = new RateLimiter(
      CONFIG.RATE_LIMIT_WINDOW,
      CONFIG.RATE_LIMIT_MAX_REQUESTS
    );
    
    // 인증 관리자 초기화
    this.authManager = new AuthManager(
      CONFIG.API_KEY_ENABLED,
      CONFIG.API_KEY
    );
    
    // 대화 관리자 초기화
    this.conversationManager = new ConversationManager(
      CONFIG.MAX_HISTORY_LENGTH
    );
    
    // 연결 이벤트 처리
    this.setupWebSocketEvents();
    
    // 정기 세션 정리 설정
    this.setupSessionCleanup();
  }

  private setupExpress(): void {
    // 기본 미들웨어 설정
    this.app.use(express.json());
    
    // 헬스 체크 엔드포인트
    this.app.get('/health', (req, res) => {
      res.status(200).json({ status: 'ok' });
    });
    
    // 서버 상태 엔드포인트
    this.app.get('/status', (req, res) => {
      if (CONFIG.API_KEY_ENABLED) {
        const apiKey = req.headers['x-api-key'];
        if (!this.authManager.authenticate(apiKey as string)) {
          return res.status(401).json({ error: 'Unauthorized' });
        }
      }
      
      res.json({
        activeSessions: this.sessions.size,
        serverTime: new Date().toISOString(),
        uptime: process.uptime()
      });
    });
  }

  private setupWebSocketEvents(): void {
    this.wss.on('connection', (socket, req) => {
      // 새 세션 생성
      const sessionId = this.generateSessionId();
      
      // 인증 처리 (필요한 경우)
      if (CONFIG.API_KEY_ENABLED) {
        const url = new URL(req.url || '', `http://${req.headers.host}`);
        const apiKey = url.searchParams.get('api_key');
        
        if (!this.authManager.authenticate(apiKey || '')) {
          Logger.warn(`Unauthorized connection attempt from ${req.socket.remoteAddress}`);
          socket.close(1008, 'Unauthorized');
          return;
        }
      }
      
      // 새 세션 등록
      this.sessions.set(sessionId, {
        id: sessionId,
        socket,
        lastActivity: Date.now(),
        conversations: new Map(),
        requestCount: 0,
        requestResetTime: Date.now() + CONFIG.RATE_LIMIT_WINDOW
      });
      
      Logger.info(`새 WebSocket 연결: 세션 ID ${sessionId}, IP ${req.socket.remoteAddress}`);
      
      // 초기 세션 ID 전송
      socket.send(JSON.stringify({
        status: 'success',
        sessionId,
        message: 'Connected to MCP Server'
      }));
      
      // 메시지 이벤트 처리
      socket.on('message', (data) => this.handleMessage(sessionId, data));
      
      // 연결 종료 이벤트 처리
      socket.on('close', () => this.handleDisconnect(sessionId));
      
      // 오류 이벤트 처리
      socket.on('error', (error) => {
        Logger.error(`WebSocket 오류 (세션 ${sessionId}):`, error);
      });
    });
  }

  private async handleMessage(sessionId: string, data: WebSocket.Data): Promise<void> {
    const session = this.sessions.get(sessionId);
    if (!session) {
      Logger.warn(`존재하지 않는 세션 ID로부터의 메시지: ${sessionId}`);
      return;
    }
    
    // 세션 활동 시간 업데이트
    session.lastActivity = Date.now();
    
    // 레이트 리밋 확인
    if (!this.rateLimiter.checkLimit(sessionId)) {
      session.socket.send(JSON.stringify({
        status: 'error',
        error: 'Rate limit exceeded. Please try again later.',
        sessionId
      }));
      return;
    }
    
    try {
      // 메시지 파싱
      const message = JSON.parse(data.toString()) as ClientMessage;
      Logger.debug(`수신된 메시지 (세션 ${sessionId}):`, message);
      
      // 대화 ID 확인 (없으면 생성)
      const conversationId = message.conversationId || this.generateConversationId();
      
      // 명령 처리
      switch (message.command.toLowerCase()) {
        case 'generate':
        case 'chat':
          await this.handleGenerateCommand(session, message, conversationId);
          break;
          
        case 'reset':
          this.handleResetCommand(session, conversationId);
          break;
          
        default:
          session.socket.send(JSON.stringify({
            status: 'error',
            error: `Unknown command: ${message.command}`,
            sessionId,
            conversationId
          }));
      }
    } catch (error) {
      Logger.error(`메시지 처리 오류 (세션 ${sessionId}):`, error);
      
      session.socket.send(JSON.stringify({
        status: 'error',
        error: `Failed to process message: ${error instanceof Error ? error.message : 'Unknown error'}`,
        sessionId
      }));
    }
  }

  private async handleGenerateCommand(
    session: ClientSession, 
    message: ClientMessage, 
    conversationId: string
  ): Promise<void> {
    const prompt = message.command.toLowerCase() === 'chat' 
      ? message.command 
      : message.command.substring('generate'.length).trim();
    
    if (!prompt) {
      session.socket.send(JSON.stringify({
        status: 'error',
        error: 'Empty prompt',
        sessionId: session.id,
        conversationId
      }));
      return;
    }
    
    // 대화 기록에 사용자 메시지 추가
    this.conversationManager.addMessage(conversationId, 'user', prompt);
    
    // Ollama 옵션 설정
    const ollamaOptions = {
      stream: message.options?.stream === true,
      temperature: message.options?.temperature,
      maxTokens: message.options?.maxTokens,
      context: this.conversationManager.getContext(conversationId)
    };
    
    try {
      const result = await this.ollamaClient.generateCompletion(prompt, ollamaOptions);
      
      if (result instanceof EventEmitter) {
        // 스트리밍 응답 처리
        let fullResponse = '';
        
        result.on('data', (data: OllamaResponse) => {
          fullResponse += data.response;
          
          session.socket.send(JSON.stringify({
            status: 'success',
            result: {
              text: data.response,
              done: false
            },
            sessionId: session.id,
            conversationId
          }));
        });
        
        result.on('end', (data: OllamaResponse) => {
          // 대화 기록에 AI 응답 추가
          this.conversationManager.addMessage(conversationId, 'assistant', fullResponse);
          
          // 컨텍스트 저장 (있는 경우)
          if (data.context) {
            this.conversationManager.setContext(conversationId, data.context);
          }
          
          session.socket.send(JSON.stringify({
            status: 'success',
            result: {
              text: '',
              done: true,
              total_duration: data.total_duration,
              model: data.model
            },
            sessionId: session.id,
            conversationId
          }));
        });
        
        result.on('error', (error: Error) => {
          Logger.error(`스트리밍 응답 오류 (세션 ${session.id}):`, error);
          
          session.socket.send(JSON.stringify({
            status: 'error',
            error: `Streaming error: ${error.message}`,
            sessionId: session.id,
            conversationId
          }));
        });
      } else {
        // 비스트리밍 응답 처리
        const response = result as OllamaResponse;
        
        // 대화 기록에 AI 응답 추가
        this.conversationManager.addMessage(conversationId, 'assistant', response.response);
        
        // 컨텍스트 저장 (있는 경우)
        if (response.context) {
          this.conversationManager.setContext(conversationId, response.context);
        }
        
        session.socket.send(JSON.stringify({
          status: 'success',
          result: {
            text: response.response,
            done: true,
            total_duration: response.total_duration,
            model: response.model
          },
          sessionId: session.id,
          conversationId
        }));
      }
    } catch (error) {
      Logger.error(`Ollama 응답 오류 (세션 ${session.id}):`, error);
      
      session.socket.send(JSON.stringify({
        status: 'error',
        error: `Failed to generate response: ${error instanceof Error ? error.message : 'Unknown error'}`,
        sessionId: session.id,
        conversationId
      }));
    }
  }

  private handleResetCommand(session: ClientSession, conversationId: string): void {
    // 대화 초기화
    this.conversationManager.getOrCreateConversation(conversationId).history = [];
    this.conversationManager.setContext(conversationId, []);
    
    session.socket.send(JSON.stringify({
      status: 'success',
      result: 'Conversation reset successfully',
      sessionId: session.id,
      conversationId
    }));
  }

  private handleDisconnect(sessionId: string): void {
    const session = this.sessions.get(sessionId);
    if (session) {
      Logger.info(`WebSocket 연결 종료: 세션 ID ${sessionId}`);
      this.sessions.delete(sessionId);
    }
  }

  private setupSessionCleanup(): void {
    // 비활성 세션 정리 (30분마다)
    const CLEANUP_INTERVAL = 30 * 60 * 1000; // 30분
    const INACTIVE_THRESHOLD = 60 * 60 * 1000; // 1시간
    
    setInterval(() => {
      const now = Date.now();
      let removedCount = 0;
      
      this.sessions.forEach((session, id) => {
        if (now - session.lastActivity > INACTIVE_THRESHOLD) {
          // 오래된 세션 종료
          try {
            session.socket.close(1000, 'Session timeout');
          } catch (e) {
            // 이미 닫힌 소켓에 대한 예외 무시
          }
          
          this.sessions.delete(id);
          removedCount++;
        }
      });
      
      if (removedCount > 0) {
        Logger.info(`세션 정리: ${removedCount}개의 비활성 세션 제거됨`);
      }
    }, CLEANUP_INTERVAL);
  }

  private generateSessionId(): string {
    return 'session_' + Math.random().toString(36).substring(2, 15);
  }

  private generateConversationId(): string {
    return 'conv_' + Math.random().toString(36).substring(2, 15);
  }

  public start(): void {
    this.server.listen(CONFIG.PORT, CONFIG.HOST, () => {
      Logger.info(`MCP 서버가 http://${CONFIG.HOST}:${CONFIG.PORT}에서 실행 중입니다`);
      Logger.info(`WebSocket 서버가 ws://${CONFIG.HOST}:${CONFIG.PORT}에서 실행 중입니다`);
      Logger.info(`Ollama API: ${CONFIG.OLLAMA_HOST}, 모델: ${CONFIG.OLLAMA_MODEL}`);
    });
  }

  public stop(): void {
    Logger.info('MCP 서버 종료 중...');
    
    // 모든 세션 종료
    this.sessions.forEach(session => {
      try {
        session.socket.close(1000, 'Server shutdown');
      } catch (e) {
        // 이미 닫힌 소켓에 대한 예외 무시
      }
    });
    
    // 서버 종료
    this.server.close(() => {
      Logger.info('서버가 정상적으로 종료되었습니다');
    });
  }
}

// 서버 인스턴스 생성 및 시작
const mcpServer = new MCPServer();

// 종료 시그널 처리
process.on('SIGINT', () => {
  mcpServer.stop();
  process.exit(0);
});

process.on('SIGTERM', () => {
  mcpServer.stop();
  process.exit(0);
});

// 처리되지 않은 예외 로깅
process.on('uncaughtException', (error) => {
  Logger.error('처리되지 않은 예외:', error);
  // 심각한 오류의 경우 서버 재시작을 고려할 수 있음
});

// 처리되지 않은 프로미스 거부 로깅
process.on('unhandledRejection', (reason, promise) => {
  Logger.error('처리되지 않은 프로미스 거부:', reason);
});

// 서버 시작
mcpServer.start();