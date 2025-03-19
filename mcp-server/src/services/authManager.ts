// src/services/authManager.ts
import { IAuthManager } from '../types';
import logger from '../logger';

/**
 * 인증 관리자 클래스
 * API 키 기반 인증 처리
 */
class AuthManager implements IAuthManager {
  private readonly enabled: boolean;
  private readonly apiKey: string;

  constructor(enabled: boolean, apiKey: string) {
    this.enabled = enabled;
    this.apiKey = apiKey;

    if (enabled && !apiKey) {
      logger.warn('API 키 인증이 활성화되었지만 API 키가 설정되지 않았습니다.');
    }
  }

  /**
   * 인증 확인
   * @param key 제공된 API 키
   * @returns 인증이 성공하면 true, 실패하면 false
   */
  authenticate(key: string | undefined): boolean {
    if (!this.enabled) {
      return true;
    }

    const isValid = key === this.apiKey;
    
    if (!isValid) {
      logger.warn('잘못된 API 키로 인증 시도');
    }
    
    return isValid;
  }
}

export default AuthManager;