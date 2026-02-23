import fs from 'fs';
import path from 'path';

/** Remove old screenshots and test results before each run. */
export default function globalSetup(): void {
  const dirs = [
    path.join(__dirname, 'test-results'),
    path.join(__dirname, 'report'),
  ];

  for (const dir of dirs) {
    if (fs.existsSync(dir)) {
      fs.rmSync(dir, { recursive: true, force: true });
      console.log(`Cleaned: ${dir}`);
    }
  }
}
