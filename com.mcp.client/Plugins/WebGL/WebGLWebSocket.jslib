// WebGLWebSocket.jslib - WebGL 환경에서 웹소켓 통신을 위한 JavaScript 플러그인

mergeInto(LibraryManager.library, {
  // 웹소켓 생성
  WebSocketCreate: function(url) {
    var urlString = UTF8ToString(url);
    var id = webSocketInstances.length;
    webSocketInstances[id] = {
      socket: new WebSocket(urlString),
      messages: [],
      connected: false
    };
    
    var socket = webSocketInstances[id].socket;
    
    socket.binaryType = "arraybuffer";
    
    socket.onopen = function() {
      webSocketInstances[id].connected = true;
      if (webSocketInstances[id].onOpen) webSocketInstances[id].onOpen();
    };
    
    socket.onmessage = function(e) {
      var data = e.data;
      if (webSocketInstances[id].onMessage) {
        if (typeof data === "string") {
          webSocketInstances[id].onMessage(data);
        }
      }
    };
    
    socket.onerror = function(e) {
      if (webSocketInstances[id].onError) webSocketInstances[id].onError("Error: " + e.message);
    };
    
    socket.onclose = function(e) {
      webSocketInstances[id].connected = false;
      if (webSocketInstances[id].onClose) webSocketInstances[id].onClose();
    };
    
    return id;
  },
  
  // 웹소켓 상태 확인
  WebSocketState: function(instanceId) {
    var instance = webSocketInstances[instanceId];
    if (!instance) return 3; // 닫힘
    if (instance.connected) return 1; // 열림
    return 0; // 연결 중
  },
  
  // 메시지 전송
  WebSocketSend: function(instanceId, message) {
    var instance = webSocketInstances[instanceId];
    if (!instance) return;
    
    var messageString = UTF8ToString(message);
    instance.socket.send(messageString);
  },
  
  // 연결 시작
  WebSocketConnect: function(instanceId) {
    // 이미 생성 시 연결되므로 추가 액션 불필요
  },
  
  // 연결 종료
  WebSocketClose: function(instanceId) {
    var instance = webSocketInstances[instanceId];
    if (!instance) return;
    
    instance.socket.close();
  },
  
  // 콜백 등록 함수들
  WebSocketAddMessageCallback: function(instanceId, callback) {
    var instance = webSocketInstances[instanceId];
    if (!instance) return;
    
    instance.onMessage = function(message) {
      var bufferSize = lengthBytesUTF8(message) + 1;
      var buffer = _malloc(bufferSize);
      stringToUTF8(message, buffer, bufferSize);
      dynCall_vi(callback, buffer);
      _free(buffer);
    };
  },
  
  WebSocketAddErrorCallback: function(instanceId, callback) {
    var instance = webSocketInstances[instanceId];
    if (!instance) return;
    
    instance.onError = function(error) {
      var bufferSize = lengthBytesUTF8(error) + 1;
      var buffer = _malloc(bufferSize);
      stringToUTF8(error, buffer, bufferSize);
      dynCall_vi(callback, buffer);
      _free(buffer);
    };
  },
  
  WebSocketAddOpenCallback: function(instanceId, callback) {
    var instance = webSocketInstances[instanceId];
    if (!instance) return;
    
    instance.onOpen = function() {
      dynCall_v(callback);
    };
  },
  
  WebSocketAddCloseCallback: function(instanceId, callback) {
    var instance = webSocketInstances[instanceId];
    if (!instance) return;
    
    instance.onClose = function() {
      dynCall_v(callback);
    };
  }
});

// 웹소켓 인스턴스 관리를 위한 전역 배열
var webSocketInstances = [];