import { CodeModification, CodeModificationParser } from '../types';
import logger from '../logger';

class UnityCodeParser implements CodeModificationParser {
  // AI 응답 텍스트에서 코드 수정 명령 추출
  parseAIResponse(text: string): CodeModification[] {
    const modifications: CodeModification[] = [];
    
    try {
      // 패턴: [UNITY_CODE:파일경로]
      // 코드 블록
      // [/UNITY_CODE]
      const codeBlockRegex = /\[UNITY_CODE:([\w\/\.]+)\]([\s\S]*?)\[\/UNITY_CODE\]/g;
      
      let match;
      while ((match = codeBlockRegex.exec(text)) !== null) {
        const filePath = match[1].trim();
        const content = match[2].trim();
        
        logger.debug(`코드 생성 명령 감지: ${filePath}`);
        
        modifications.push({
          filePath,
          content,
          operation: 'create' // 기본값은 파일 생성/덮어쓰기
        });
      }
      
      // 패턴: [UNITY_MODIFY:파일경로:대상섹션]
      // 코드 블록
      // [/UNITY_MODIFY]
      const modifyBlockRegex = /\[UNITY_MODIFY:([\w\/\.]+):([\w]+)\]([\s\S]*?)\[\/UNITY_MODIFY\]/g;
      
      while ((match = modifyBlockRegex.exec(text)) !== null) {
        const filePath = match[1].trim();
        const targetSection = match[2].trim();
        const content = match[3].trim();
        
        logger.debug(`코드 수정 명령 감지: ${filePath} (섹션: ${targetSection})`);
        
        modifications.push({
          filePath,
          content,
          operation: 'modify',
          targetSection
        });
      }
      
      // 패턴: [UNITY_DELETE:파일경로]
      const deleteRegex = /\[UNITY_DELETE:([\w\/\.]+)\]/g;
      
      while ((match = deleteRegex.exec(text)) !== null) {
        const filePath = match[1].trim();
        
        logger.debug(`코드 삭제 명령 감지: ${filePath}`);
        
        modifications.push({
          filePath,
          content: '',
          operation: 'delete'
        });
      }
    } catch (error) {
      logger.error('코드 수정 명령 파싱 중 오류:', error);
    }
    
    return modifications;
  }
  
  // 파일 내용에 있는 특정 섹션을 새 내용으로 교체
  modifySection(originalContent: string, sectionName: string, newContent: string): string {
    // 섹션 시작과 끝 패턴
    const sectionStartPattern = `// BEGIN ${sectionName}`;
    const sectionEndPattern = `// END ${sectionName}`;
    
    // 섹션 시작과 끝 위치 찾기
    const startIndex = originalContent.indexOf(sectionStartPattern);
    const endIndex = originalContent.indexOf(sectionEndPattern);
    
    if (startIndex === -1 || endIndex === -1 || startIndex >= endIndex) {
      // 섹션을 찾을 수 없으면 원본 반환
      logger.warn(`섹션을 찾을 수 없습니다: ${sectionName}`);
      return originalContent;
    }
    
    // 섹션 끝부분 찾기 (줄 바꿈 포함)
    const sectionEndLine = originalContent.indexOf('\n', endIndex);
    const endPosition = sectionEndLine !== -1 ? sectionEndLine + 1 : originalContent.length;
    
    // 파일 내용 교체
    return (
      originalContent.substring(0, startIndex + sectionStartPattern.length) +
      '\n' + newContent + '\n' +
      originalContent.substring(endIndex)
    );
  }
}

export default UnityCodeParser;