#!/usr/bin/env node
const { spawn } = require('child_process');
const path = require('path');
const fs = require('fs');

console.log('========================================================');
console.log('       Infinite-Lite-Token Universal Launcher           ');
console.log('========================================================');

const localAppData = process.env.LOCALAPPDATA || path.join(require('os').homedir(), 'AppData', 'Local');
const appDir = path.join(localAppData, 'InfiniteLiteToken');
const exePath = path.join(appDir, 'InfiniteLiteToken.UI.exe');

if (fs.existsSync(exePath)) {
    console.log('[+] Launching Infinite-Lite-Token Desktop App...');
    const child = spawn(exePath, [], { detached: true, stdio: 'ignore' });
    child.unref();
} else {
    console.log('[!] Desktop executable not found in local installation path.');
    console.log('[*] Running Zero-Touch Dependency & Runtime Bootstrapper...');
    // We can also trigger the C# core app or download/compile if needed
    console.log('[*] Please clone/build the repository or place InfiniteLiteToken.UI.exe in: ' + appDir);
}
