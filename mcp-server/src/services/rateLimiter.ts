// src/services/rateLimiter.ts
import { IRateLimiter } from '../types';
import logger from '../logger';

/**
 * 레이트 리미터 클래스 - 과도한 요청 제한
 * 특정 시간 내에 특정 수의 요청만 허용
 */
class RateLimiter implements IRateLimiter {
  private sessions = new Map<string, { count: number, resetTime: number }>();
  private readonly windowMs: number;
  private readonly maxRequests: number;

  constructor(windowMs: number, maxRequests: number) {
    this.windowMs = windowMs;
    this.maxRequests = maxRequests;
  }

  /**
   * 특정 세션의 레이트 리밋 확인
   * @param sessionId 세션 ID
   * @returns 요청이 허용되면 true, 제한된 경우 false
   */
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
      logger.warn(`Rate limit exceeded for session ${sessionId}`);
      return false;
    }

    return true;
  }
}

export default RateLimiter;