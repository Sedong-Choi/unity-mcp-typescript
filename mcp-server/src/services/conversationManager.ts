// src/services/conversationManager.ts
import { IConversationManager, Conversation } from '../types';
import logger from '../logger';

/**
 * 대화 컨텍스트 관리 클래스
 * 대화 기록 및 컨텍스트 관리
 */
class ConversationManager implements IConversationManager {
  private conversations = new Map<string, Conversation>();
  private readonly maxHistoryLength: number;

  constructor(maxHistoryLength: number) {
    this.maxHistoryLength = maxHistoryLength;
    logger.info(`대화 관리자 초기화: 최대 기록 길이 ${maxHistoryLength}`);
  }

  /**
   * 대화 가져오기 (없으면 생성)
   * @param conversationId 대화 ID
   * @returns Conversation 객체
   */
  getOrCreateConversation(conversationId: string): Conversation {
    if (!this.conversations.has(conversationId)) {
      logger.debug(`새 대화 생성: ${conversationId}`);
      this.conversations.set(conversationId, {
        id: conversationId,
        history: [],
      });
    }
    return this.conversations.get(conversationId) as Conversation;
  }

  /**
   * 대화에 메시지 추가
   * @param conversationId 대화 ID
   * @param role 메시지 발신자 역할 ('user' 또는 'assistant')
   * @param content 메시지 내용
   */
  addMessage(conversationId: string, role: 'user' | 'assistant', content: string): void {
    const conversation = this.getOrCreateConversation(conversationId);
    
    conversation.history.push({ role, content });
    logger.debug(`메시지 추가: ${conversationId}, 역할: ${role}, 길이: ${content.length}자`);
    
    // 최대 기록 길이 제한
    if (conversation.history.length > this.maxHistoryLength) {
      conversation.history = conversation.history.slice(
        conversation.history.length - this.maxHistoryLength
      );
      logger.debug(`대화 기록 자르기: ${conversationId}, 새 길이: ${conversation.history.length}`);
    }
  }

  /**
   * 특정 대화의 컨텍스트 가져오기
   * @param conversationId 대화 ID
   * @returns 컨텍스트 배열 또는 undefined
   */
  getContext(conversationId: string): number[] | undefined {
    const conversation = this.conversations.get(conversationId);
    return conversation?.context;
  }

  /**
   * 특정 대화의 컨텍스트 설정
   * @param conversationId 대화 ID
   * @param context 컨텍스트 배열
   */
  setContext(conversationId: string, context: number[]): void {
    const conversation = this.getOrCreateConversation(conversationId);
    conversation.context = context;
    logger.debug(`컨텍스트 업데이트: ${conversationId}, 길이: ${context.length}`);
  }
}

export default ConversationManager;