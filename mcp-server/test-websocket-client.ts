// test-websocket-client.ts
import WebSocket from 'ws';
import { v4 as uuidv4 } from 'uuid';

const ws = new WebSocket('ws://localhost:8765');

ws.on('open', () => {
  console.log('WebSocket 연결 성공');
  
  // Unity 코드 생성 요청 메시지
  const message = {
    command: 'generate',
    conversationId: uuidv4(),
    message: '간단한 Unity 플레이어 이동 컨트롤러 스크립트를 작성해주세요. WASD로 이동하고 스페이스바로 점프하는 기능이 있어야 합니다.',
    options: {
      model: 'gemma3:12b',
      stream: false  // 응답 전체를 한 번에 받기 위해 스트리밍 비활성화
    }
  };
  
  console.log('전송 메시지:', message);
  ws.send(JSON.stringify(message));
});

ws.on('message', (data) => {
  try {
    const response = JSON.parse(data.toString());
    console.log('수신 메시지 타입:', response.type);
    
    // 코드 수정 명령 확인
    if (response.type === 'code_modification') {
      console.log('코드 수정 결과:', JSON.stringify(response.modifications, null, 2));
    }
    // 생성된 내용에서 코드 블록 확인
    else if (response.type === 'generation' && response.content) {
      console.log('응답 내용:', response.content.substring(0, 100) + '...');
      
      // Unity 코드 블록 확인
      if (response.content.includes('[UNITY_CODE:')) {
        console.log('Unity 코드 블록 감지됨!');
      }
    }
  } catch (e) {
    console.log('원시 메시지 수신:', data.toString());
  }
});

ws.on('error', (error) => {
  console.error('WebSocket 오류:', error);
});

ws.on('close', () => {
  console.log('WebSocket 연결 종료');
});