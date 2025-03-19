// src/server.ts
import * as http from 'http';
import express from 'express';
import WebSocket from 'ws';

// 설정 및 로거 가져오기
import config from './config';
import logger from './logger';

// 서비스 클래스 가져오기
import RateLimiter from './services/rateLimiter';
import AuthManager from './services/authManager';
import ConversationManager from './services/conversationManager';
import OllamaClient from './ollama/client';
import WebSocketHandler from './websocket/handler';

/**
 * MCP 서버 클래스
 * 모든 구성 요소를 통합하고 서버 생명주기 관리
 */
class MCPServer {
  private server: http.Server;
  private wss: WebSocket.Server;
  private app: express.Application;
  private webSocketHandler: WebSocketHandler;
  
  // 세션 정리 설정
  private readonly CLEANUP_INTERVAL = 30 * 60 * 1000; // 30분
  private readonly INACTIVE_THRESHOLD = 60 * 60 * 1000; // 1시간

  constructor() {
    // Express 앱 초기화
    this.app = express();
    this.setupExpress();
    
    // HTTP 서버 생성
    this.server = http.createServer(this.app);
    
    // 서비스 초기화
    const rateLimiter = new RateLimiter(
      config.RATE_LIMIT_WINDOW,
      config.RATE_LIMIT_MAX_REQUESTS
    );
    
    const authManager = new AuthManager(
      config.API_KEY_ENABLED,
      config.API_KEY
    );
    
    const conversationManager = new ConversationManager(
      config.MAX_HISTORY_LENGTH
    );
    
    const ollamaClient = new OllamaClient(
      config.OLLAMA_HOST,
      config.OLLAMA_MODEL
    );
    
    // WebSocket 서버 및 핸들러 초기화
    this.wss = new WebSocket.Server({ server: this.server });
    this.webSocketHandler = new WebSocketHandler(
      authManager,
      rateLimiter,
      conversationManager,
      ollamaClient
    );
    
    // WebSocket 연결 이벤트 설정
    this.setupWebSocketEvents();
    
    // 정기 세션 정리 설정
    this.setupSessionCleanup();
  }

  /**
   * Express 앱 설정
   * 미들웨어 및 라우트 등록
   */
  private setupExpress(): void {
    // 기본 미들웨어 설정
    this.app.use(express.json());
    
    // CORS 헤더 설정 (필요한 경우)
    this.app.use((req, res, next) => {
      res.header('Access-Control-Allow-Origin', '*');
      res.header('Access-Control-Allow-Headers', 'Origin, X-Requested-With, Content-Type, Accept, x-api-key');
      res.header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
      if (req.method === 'OPTIONS') {
        return res.sendStatus(200);
      }
      next();
    });
    
    // 헬스 체크 엔드포인트
    this.app.get('/health', (req, res) => {
      res.status(200).json({ status: 'ok' });
    });
    
    // 서버 상태 엔드포인트
    this.app.get('/status', (req, res) => {
      res.json({
        activeSessions: this.webSocketHandler.getSessions().size,
        serverTime: new Date().toISOString(),
        uptime: process.uptime()
      });
    });
  }

  /**
   * WebSocket 서버 이벤트 설정
   */
  private setupWebSocketEvents(): void {
    this.wss.on('connection', (socket, request) => {
      this.webSocketHandler.handleConnection(socket, request);
    });
  }

  /**
   * 정기적인 세션 정리 설정
   */
  private setupSessionCleanup(): void {
    setInterval(() => {
      this.webSocketHandler.cleanupInactiveSessions(this.INACTIVE_THRESHOLD);
    }, this.CLEANUP_INTERVAL);
  }

  /**
   * 서버 시작
   */
  public start(): void {
    this.server.listen(config.PORT, config.HOST, () => {
      logger.info(`MCP 서버가 http://${config.HOST}:${config.PORT}에서 실행 중입니다`);
      logger.info(`WebSocket 서버가 ws://${config.HOST}:${config.PORT}에서 실행 중입니다`);
      logger.info(`Ollama API: ${config.OLLAMA_HOST}, 모델: ${config.OLLAMA_MODEL}`);
    });
  }

  /**
   * 서버 종료
   */
  public stop(): void {
    logger.info('MCP 서버 종료 중...');
    
    // 서버 종료
    this.server.close(() => {
      logger.info('서버가 정상적으로 종료되었습니다');
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
  logger.error('처리되지 않은 예외:', error);
  // 심각한 오류의 경우 서버 재시작을 고려할 수 있음
});

// 처리되지 않은 프로미스 거부 로깅
process.on('unhandledRejection', (reason, promise) => {
  logger.error('처리되지 않은 프로미스 거부:', reason);
});

// 서버 시작
mcpServer.start();