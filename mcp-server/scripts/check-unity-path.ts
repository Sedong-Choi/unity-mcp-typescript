// scripts/check-unity-path.ts
import * as fs from 'fs';
import * as dotenv from 'dotenv';
import * as path from 'path';

dotenv.config();

const unityPath = process.env.UNITY_PROJECT_PATH || './unity-project';
console.log('원본 경로:', unityPath);
console.log('정규화된 경로:', path.resolve(unityPath));
console.log('경로 존재 여부:', fs.existsSync(path.resolve(unityPath)));

// 공백 처리 테스트
if (unityPath.includes('\\')) {
  const cleanPath = unityPath.replace(/\\/g, '');
  console.log('이스케이프 제거 경로:', cleanPath);
  console.log('이스케이프 제거 경로 존재 여부:', fs.existsSync(path.resolve(cleanPath)));
}