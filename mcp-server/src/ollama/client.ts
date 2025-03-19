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
      
      가능한 코드 예시:
      - 캐릭터 컨트롤러
      - 카메라 스크립트
      - 게임 매니저
      - UI 컨트롤러
      - 오브젝트 상호작용
    `;
    
    try {
      const requestData: OllamaRequest = {
        model,
        prompt,
        system: systemPrompt,
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