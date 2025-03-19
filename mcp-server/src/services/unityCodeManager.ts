import fs from 'fs';
import path from 'path';
import { UnityCodeManager } from '../types';
import logger from '../logger';

class FileManager implements UnityCodeManager {
  private unityProjectPath: string;
  
  constructor(unityProjectPath: string) {
    this.unityProjectPath = unityProjectPath;
    logger.info(`Unity 프로젝트 경로 설정: ${unityProjectPath}`);
    
    // 경로 유효성 검사
    if (!fs.existsSync(unityProjectPath)) {
      logger.warn(`Unity 프로젝트 경로를 찾을 수 없습니다: ${unityProjectPath}`);
    }
  }
  
  // 파일 읽기
  readFile(relativePath: string): string {
    const fullPath = path.join(this.unityProjectPath, relativePath);
    if (!fs.existsSync(fullPath)) {
      throw new Error(`파일을 찾을 수 없습니다: ${fullPath}`);
    }
    return fs.readFileSync(fullPath, 'utf-8');
  }
  
  // 파일 쓰기
  writeFile(relativePath: string, content: string): void {
    const fullPath = path.join(this.unityProjectPath, relativePath);
    const dirPath = path.dirname(fullPath);
    
    // 디렉토리가 없으면 생성
    if (!fs.existsSync(dirPath)) {
      fs.mkdirSync(dirPath, { recursive: true });
    }
    
    fs.writeFileSync(fullPath, content, 'utf-8');
    logger.info(`파일이 수정되었습니다: ${fullPath}`);
  }
  
  // 파일 목록 가져오기
  listFiles(relativePath: string = ''): string[] {
    const fullPath = path.join(this.unityProjectPath, relativePath);
    if (!fs.existsSync(fullPath)) {
      return [];
    }
    
    return fs.readdirSync(fullPath);
  }
  
  getProjectPath(): string {
    return this.unityProjectPath;
  }
}

export default FileManager;