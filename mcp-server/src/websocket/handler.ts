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
  OllamaResponse,
  CodeModificationParser,
  UnityCodeManager,
  CodeModification
} from '../types';
import logger from '../logger';
import FileManager from '../services/unityCodeManager';
import UnityCodeParser from '../services/codeModificationParser';
import { unityConfig } from '../config';


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
  private codeManager: UnityCodeManager;
  private codeParser: CodeModificationParser;

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
    
    // Unity 코드 관리 기능 초기화
    this.codeManager = new FileManager(unityConfig.projectPath);
    this.codeParser = new UnityCodeParser();
    
    logger.info('WebSocket 핸들러 초기화');
    logger.info(`Unity 프로젝트 경로: ${this.codeManager.getProjectPath()}`);
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
    const prompt = message.message || ''; // 메시지 필드에서 프롬프트 얻기
    
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
    
    // Unity 코드 생성을 위한 시스템 프롬프트 추가
    const systemPrompt = `
      당신은 Unity 개발자를 위한 AI 어시스턴트입니다.
      사용자의 요청에 따라 Unity C# 스크립트를 생성하거나 수정할 수 있습니다.
      
      코드를 생성할 때는 다음 형식을 사용하세요:
      [UNITY_CODE:파일경로]
      // 코드 내용
      [/UNITY_CODE]
      
      기존 코드의 특정 섹션을 수정할 때는 다음 형식을 사용하세요:
      [UNITY_MODIFY:파일경로:섹션이름]
      // 새로운 코드 내용
      [/UNITY_MODIFY]
      
      코드를 생성할 때 다음 규칙을 따르세요:
      1. 모든 C# 스크립트는 유효한 Unity 코드여야 합니다.
      2. MonoBehaviour를 상속받는 클래스는 클래스 이름과 파일 이름이 일치해야 합니다.
      3. 표준 Unity 라이프사이클 메서드(Start, Update 등)를 적절히 사용하세요.
      4. 코드에 충분한 주석을 추가하여 작동 방식을 설명하세요.
    `;
    
    // Ollama 옵션 설정
    const ollamaOptions = {
      stream: message.options?.stream === true,
      temperature: message.options?.temperature,
      maxTokens: message.options?.maxTokens,
      context: this.conversationManager.getContext(conversationId),
      system: systemPrompt // 시스템 프롬프트 추가
    };
    
    try {
      const result = await this.ollamaClient.generateCompletion(prompt, ollamaOptions);
      
      if (result instanceof EventEmitter) {
        // 스트리밍 응답 처리
        let fullResponse = '';
        
        result.on('data', (data: OllamaResponse) => {
          fullResponse += data.response;
          
          session.socket.send(JSON.stringify({
            type: 'generation_chunk',
            chunk: data.response,
            done: false,
            conversationId,
            sessionId: session.id
          }));
        });
        
        result.on('end', (data: OllamaResponse) => {
          // 대화 기록에 AI 응답 추가
          this.conversationManager.addMessage(conversationId, 'assistant', fullResponse);
          
          // 컨텍스트 저장 (있는 경우)
          if (data.context) {
            this.conversationManager.setContext(conversationId, data.context);
          }
          
          // 코드 수정 명령 처리
          this.processCodeModifications(fullResponse, session, conversationId);
          
          session.socket.send(JSON.stringify({
            type: 'generation',
            content: '',
            done: true,
            total_duration: data.total_duration,
            model: data.model,
            conversationId,
            sessionId: session.id
          }));
        });
        
        result.on('error', (error: Error) => {
          logger.error(`스트리밍 응답 오류 (세션 ${session.id}):`, error);
          
          session.socket.send(JSON.stringify({
            type: 'error',
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
        
        // 코드 수정 명령 처리
        this.processCodeModifications(response.response, session, conversationId);
        
        session.socket.send(JSON.stringify({
          type: 'generation',
          content: response.response,
          done: true,
          total_duration: response.total_duration,
          model: response.model,
          conversationId,
          sessionId: session.id
        }));
      }
    } catch (error) {
      logger.error(`Ollama 응답 오류 (세션 ${session.id}):`, error);
      
      session.socket.send(JSON.stringify({
        type: 'error',
        error: `응답 생성 실패: ${error instanceof Error ? error.message : '알 수 없는 오류'}`,
        sessionId: session.id,
        conversationId
      }));
    }
  }

  /**
   * AI 응답에서 코드 수정 명령을 추출하고 처리
   * @param responseText AI 응답 텍스트
   * @param session 클라이언트 세션
   * @param conversationId 대화 ID
   */
  private processCodeModifications(
    responseText: string, 
    session: ClientSession, 
    conversationId: string
  ): void {
    try {
      // 코드 수정 명령 추출
      const modifications = this.codeParser.parseAIResponse(responseText);
      
      if (modifications.length === 0) {
        return; // 수정 명령 없음
      }
      
      logger.info(`${modifications.length}개의 코드 수정 명령 발견`);
      
      // 각 수정 명령 처리
      const results = modifications.map(mod => {
        try {
          // 파일 경로 정규화
          const normalizedPath = this.normalizeFilePath(mod.filePath);
          
          // 작업 유형에 따라 처리
          switch (mod.operation) {
            case 'create':
              this.codeManager.writeFile(normalizedPath, mod.content);
              return { 
                filePath: normalizedPath, 
                operation: 'create', 
                success: true 
              };
              
            case 'modify':
              if (mod.targetSection) {
                // 기존 파일 읽기
                const originalContent = this.codeManager.readFile(normalizedPath);
                // 섹션 수정
                const modifiedContent = this.codeParser.modifySection(
                  originalContent,
                  mod.targetSection,
                  mod.content
                );
                // 수정된 내용 저장
                this.codeManager.writeFile(normalizedPath, modifiedContent);
                return { 
                  filePath: normalizedPath, 
                  operation: 'modify', 
                  section: mod.targetSection, 
                  success: true 
                };
              }
              return { 
                filePath: normalizedPath, 
                operation: 'modify', 
                success: false, 
                error: '타겟 섹션이 지정되지 않음' 
              };
              
            case 'delete':
              // 안전을 위해 삭제 기능은 로깅만 함
              logger.warn(`파일 삭제 요청 무시됨: ${normalizedPath}`);
              return { 
                filePath: normalizedPath, 
                operation: 'delete', 
                success: false, 
                error: '보안상의 이유로 파일 삭제 작업은 비활성화되어 있습니다' 
              };
              
            default:
              return { 
                filePath: normalizedPath, 
                operation: mod.operation, 
                success: false, 
                error: '알 수 없는 작업 유형' 
              };
          }
        } catch (error) {
          logger.error(`코드 수정 오류 (파일: ${mod.filePath}):`, error);
          return { 
            filePath: mod.filePath, 
            operation: mod.operation, 
            success: false, 
            error: error instanceof Error ? error.message : '알 수 없는 오류' 
          };
        }
      });
      
      // 수정 결과 클라이언트에 알림
      session.socket.send(JSON.stringify({
        type: 'code_modification',
        conversationId,
        sessionId: session.id,
        modifications: results
      }));
      
    } catch (error) {
      logger.error('코드 수정 처리 중 오류:', error);
      // 오류를 클라이언트에 알림 (선택적)
      session.socket.send(JSON.stringify({
        type: 'error',
        conversationId,
        sessionId: session.id,
        error: `코드 수정 처리 중 오류: ${error instanceof Error ? error.message : '알 수 없는 오류'}`
      }));
    }
  }

  /**
   * 파일 경로 정규화
   * @param filePath 원본 파일 경로
   * @returns 정규화된 파일 경로
   */
  private normalizeFilePath(filePath: string): string {
    let normalizedPath = filePath;
    
    // Assets/Scripts/ 경로 접두사가 없으면 추가
    if (!normalizedPath.startsWith(unityConfig.assetsPath)) {
      normalizedPath = `${unityConfig.assetsPath}/${normalizedPath}`;
    }
    
    // .cs 확장자가 없으면 추가 (다른 파일 타입 제외)
    if (!normalizedPath.endsWith('.cs') && !normalizedPath.endsWith('.json') && 
        !normalizedPath.endsWith('.txt') && !normalizedPath.endsWith('.xml') && 
        !normalizedPath.endsWith('.shader')) {
      normalizedPath = `${normalizedPath}${unityConfig.defaultScriptExtension}`;
    }
    
    return normalizedPath;
  }
}

export default WebSocketHandler;