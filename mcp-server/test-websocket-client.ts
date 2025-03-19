// test-websocket-client.ts
import WebSocket from 'ws';
import { v4 as uuidv4 } from 'uuid';

const ws = new WebSocket('ws://localhost:8765');

ws.on('open', () => {
  console.log('WebSocket 연결 성공');
  
  // 테스트 메시지 전송
  const message = {
    command: 'generate',
    conversationId: uuidv4(),
    message: '안녕하세요, MCP 서버!',
    options: {
      model: 'gemma3:12b'
    }
  };
  
  console.log('전송 메시지:', message);
  ws.send(JSON.stringify(message));
});

ws.on('message', (data) => {
  try {
    const response = JSON.parse(data.toString());
    console.log('수신 메시지:', response);
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