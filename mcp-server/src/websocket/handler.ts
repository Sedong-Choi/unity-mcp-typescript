// src/websocket/handler.ts
import WebSocket from 'ws';
import { EventEmitter } from 'events';
import { 
  IWebSocketHandler, 
  ClientSession, 
  ClientMessage, 
  IAuthManager, 
  IRateLimiter, 
  IConversationManager, 
  IOllamaClient,
  OllamaResponse
} from '../types';
import logger from '../logger';

/**
 * WebSocket 핸들러 클래스
 * WebSocket 연결 및 메시지 처리 담당
 */
class WebSocketHandler implements IWebSocketHandler {
  private sessions: Map<string, ClientSession> = new Map();
  private authManager: IAuthManager;
  private rateLimiter: IRateLimiter;
  private conversationManager: IConversationManager;
  private ollamaClient: IOllamaClient;

  constructor(
    authManager: IAuthManager,
    rateLimiter: IRateLimiter,
    conversationManager: IConversationManager,
    ollamaClient: IOllamaClient
  ) {
    this.authManager = authManager;
    this.rateLimiter = rateLimiter;
    this.conversationManager = conversationManager;
    this.ollamaClient = ollamaClient;
    
    logger.info('WebSocket 핸들러 초기화');
  }

  /**
   * 세션 ID 생성
   * @returns 고유한 세션 ID
   */
  private generateSessionId(): string {
    return 'session_' + Math.random().toString(36).substring(2, 15);
  }

  /**
   * 대화 ID 생성
   * @returns 고유한 대화 ID
   */
  private generateConversationId(): string {
    return 'conv_' + Math.random().toString(36).substring(2, 15);
  }

  /**
   * 새로운 WebSocket 연결 처리
   * @param socket WebSocket 객체
   * @param request HTTP 요청 객체
   */
  handleConnection(socket: WebSocket, request: any): void {
    // 새 세션 생성
    const sessionId = this.generateSessionId();
    
    // 인증 처리 (필요한 경우)
    if (request.url) {
      const url = new URL(request.url, `http://${request.headers.host}`);
      const apiKey = url.searchParams.get('api_key');
      
      if (!this.authManager.authenticate(apiKey || '')) {
        logger.warn(`인증되지 않은 연결 시도: ${request.socket.remoteAddress}`);
        socket.close(1008, '인증되지 않음');
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
      requestResetTime: Date.now() + 60000 // 1분
    });
    
    logger.info(`새 WebSocket 연결: 세션 ID ${sessionId}, IP ${request.socket.remoteAddress}`);
    
    // 초기 세션 ID 전송
    socket.send(JSON.stringify({
      status: 'success',
      sessionId,
      message: 'MCP 서버에 연결됨'
    }));
    
    // 메시지 이벤트 처리
    socket.on('message', (data) => this.handleMessage(sessionId, data));
    
    // 연결 종료 이벤트 처리
    socket.on('close', () => this.handleDisconnect(sessionId));
    
    // 오류 이벤트 처리
    socket.on('error', (error) => {
      logger.error(`WebSocket 오류 (세션 ${sessionId}):`, error);
    });
  }

  /**
   * WebSocket 메시지 처리
   * @param sessionId 세션 ID
   * @param data 수신된 메시지 데이터
   */
  async handleMessage(sessionId: string, data: WebSocket.Data): Promise<void> {
    const session = this.sessions.get(sessionId);
    if (!session) {
      logger.warn(`존재하지 않는 세션 ID로부터의 메시지: ${sessionId}`);
      return;
    }
    
    // 세션 활동 시간 업데이트
    session.lastActivity = Date.now();
    
    // 레이트 리밋 확인
    if (!this.rateLimiter.checkLimit(sessionId)) {
      session.socket.send(JSON.stringify({
        status: 'error',
        error: '요청 한도 초과. 잠시 후 다시 시도하세요.',
        sessionId
      }));
      return;
    }
    
    try {
      // 메시지 파싱
      const message = JSON.parse(data.toString()) as ClientMessage;
      logger.debug(`수신된 메시지 (세션 ${sessionId}):`, message);
      
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
            error: `알 수 없는 명령: ${message.command}`,
            sessionId,
            conversationId
          }));
      }
    } catch (error) {
      logger.error(`메시지 처리 오류 (세션 ${sessionId}):`, error);
      
      session.socket.send(JSON.stringify({
        status: 'error',
        error: `메시지 처리 실패: ${error instanceof Error ? error.message : '알 수 없는 오류'}`,
        sessionId
      }));
    }
  }

  /**
   * generate/chat 명령 처리
   * @param session 클라이언트 세션
   * @param message 클라이언트 메시지
   * @param conversationId 대화 ID
   */
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
        error: '빈 프롬프트',
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
          logger.error(`스트리밍 응답 오류 (세션 ${session.id}):`, error);
          
          session.socket.send(JSON.stringify({
            status: 'error',
            error: `스트리밍 오류: ${error.message}`,
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
      logger.error(`Ollama 응답 오류 (세션 ${session.id}):`, error);
      
      session.socket.send(JSON.stringify({
        status: 'error',
        error: `응답 생성 실패: ${error instanceof Error ? error.message : '알 수 없는 오류'}`,
        sessionId: session.id,
        conversationId
      }));
    }
  }

  /**
   * reset 명령 처리
   * @param session 클라이언트 세션
   * @param conversationId 대화 ID
   */
  private handleResetCommand(session: ClientSession, conversationId: string): void {
    // 대화 초기화
    this.conversationManager.getOrCreateConversation(conversationId).history = [];
    this.conversationManager.setContext(conversationId, []);
    
    session.socket.send(JSON.stringify({
      status: 'success',
      result: '대화가 성공적으로 초기화되었습니다',
      sessionId: session.id,
      conversationId
    }));
  }

  /**
   * WebSocket 연결 종료 처리
   * @param sessionId 세션 ID
   */
  handleDisconnect(sessionId: string): void {
    const session = this.sessions.get(sessionId);
    if (session) {
      logger.info(`WebSocket 연결 종료: 세션 ID ${sessionId}`);
      this.sessions.delete(sessionId);
    }
  }

  /**
   * 세션 맵 가져오기
   * @returns 현재 활성 세션 맵
   */
  getSessions(): Map<string, ClientSession> {
    return this.sessions;
  }

  /**
   * 비활성 세션 정리
   * @param inactiveThreshold 비활성으로 간주할 시간(ms)
   */
  cleanupInactiveSessions(inactiveThreshold: number): number {
    const now = Date.now();
    let removedCount = 0;
    
    this.sessions.forEach((session, id) => {
      if (now - session.lastActivity > inactiveThreshold) {
        // 오래된 세션 종료
        try {
          session.socket.close(1000, '세션 시간 초과');
        } catch (e) {
          // 이미 닫힌 소켓에 대한 예외 무시
        }
        
        this.sessions.delete(id);
        removedCount++;
      }
    });
    
    if (removedCount > 0) {
      logger.info(`세션 정리: ${removedCount}개의 비활성 세션 제거됨`);
    }
    
    return removedCount;
  }
}

export default WebSocketHandler;