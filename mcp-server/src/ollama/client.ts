// src/ollama/client.ts
import axios from 'axios';
import { EventEmitter } from 'events';
import { IOllamaClient, OllamaRequest, OllamaResponse } from '../types';
import logger from '../logger';

/**
 * Ollama API 클라이언트 클래스
 * Ollama 서비스와의 통신 담당
 */
class OllamaClient implements IOllamaClient {
  private readonly baseUrl: string;
  private readonly defaultModel: string;

  constructor(baseUrl: string, defaultModel: string) {
    this.baseUrl = baseUrl;
    this.defaultModel = defaultModel;
    logger.info(`Ollama 클라이언트 초기화: ${baseUrl}, 기본 모델: ${defaultModel}`);
  }

  /**
   * Ollama API를 통해 텍스트 생성
   * @param prompt 입력 프롬프트
   * @param options 생성 옵션
   * @returns 스트리밍 모드의 경우 EventEmitter, 그렇지 않은 경우 응답 객체
   */
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
    const eventEmitter = new EventEmitter();
    
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

      logger.debug(`Ollama API 요청: ${JSON.stringify({
        ...requestData,
        prompt: prompt.length > 50 ? prompt.substring(0, 50) + '...' : prompt
      })}`);

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
              eventEmitter.emit('data', data);
              
              if (data.done) {
                eventEmitter.emit('end', data);
              }
            }
          } catch (error) {
            logger.error('스트리밍 청크 파싱 오류:', error);
            eventEmitter.emit('error', error);
          }
        });

        response.data.on('error', (error: Error) => {
          logger.error('스트림 오류:', error);
          eventEmitter.emit('error', error);
        });

        return eventEmitter;
      } else {
        // 비스트리밍 모드
        const response = await axios.post(`${this.baseUrl}/api/generate`, requestData);
        const data = response.data as OllamaResponse;
        logger.debug(`Ollama API 응답: 모델=${data.model}, 응답 길이=${data.response.length}자`);
        return data;
      }
    } catch (error) {
      logger.error(`Ollama API 오류: ${error}`);
      
      if (axios.isAxiosError(error) && error.response) {
        throw new Error(`Ollama API 오류: ${error.response.status} - ${JSON.stringify(error.response.data)}`);
      } else {
        throw new Error(`Ollama API 요청 실패: ${error}`);
      }
    }
  }
}

export default OllamaClient;