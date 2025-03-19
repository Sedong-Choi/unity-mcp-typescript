// src/logger.ts
import { ILogger } from './types';
import config from './config';

/**
 * 로깅 시스템
 * 다양한 로그 레벨을 지원하는 간단한 로거 구현
 */
class Logger implements ILogger {
  private static logLevels = {
    debug: 0,
    info: 1,
    warn: 2,
    error: 3
  };

  private currentLevel: number;

  constructor() {
    this.currentLevel = Logger.logLevels[config.LOG_LEVEL] || 1;
  }

  debug(message: string, ...args: any[]): void {
    if (this.currentLevel <= Logger.logLevels.debug) {
      console.debug(`[DEBUG] ${new Date().toISOString()} - ${message}`, ...args);
    }
  }

  info(message: string, ...args: any[]): void {
    if (this.currentLevel <= Logger.logLevels.info) {
      console.info(`[INFO] ${new Date().toISOString()} - ${message}`, ...args);
    }
  }

  warn(message: string, ...args: any[]): void {
    if (this.currentLevel <= Logger.logLevels.warn) {
      console.warn(`[WARN] ${new Date().toISOString()} - ${message}`, ...args);
    }
  }

  error(message: string, ...args: any[]): void {
    if (this.currentLevel <= Logger.logLevels.error) {
      console.error(`[ERROR] ${new Date().toISOString()} - ${message}`, ...args);
    }
  }
}

// 싱글톤 인스턴스 생성 및 내보내기
const logger = new Logger();
export default logger;