import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
export function verifyPackage(root) {
  const files=[];
  const walk=dir=>{for(const entry of fs.readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,entry.name);entry.isDirectory()?walk(p):files.push(p)}};
  walk(root);
  const html=files.filter(p=>p.endsWith('.html'));
  if(!html.length) throw Error('No HTML entry pages');
  for(const file of html){
    const text=fs.readFileSync(file,'utf8');
    const scripts=[...text.matchAll(/<script\b([^>]*)>/gi)];
    const first=scripts[0]?.[1]??'';
    if(!/src=["']https:\/\/extension-files\.twitch\.tv\/helper\/v1\/twitch-ext\.min\.js["']/.test(first)||/\b(?:async|defer)\b/i.test(first)) throw Error(`${file}: Helper must load first and synchronously`);
    if(scripts.filter(s=>s[1].includes('twitch-ext.min.js')).length!==1) throw Error(`${file}: expected one Helper`);
    for(const match of text.matchAll(/(?:src|href)=["']([^"']+)["']/g)){
      if(/^(https?:|data:|#)/.test(match[1]))continue;
      const target=path.resolve(path.dirname(file),match[1].split('?')[0]);
      if(!target.startsWith(path.resolve(root)+path.sep)||!fs.existsSync(target))throw Error(`${file}: missing or invalid asset ${match[1]}`);
    }
  }
  for(const file of files.filter(p=>/\.(html|js|json|css)$/.test(p))){
    const text=fs.readFileSync(file,'utf8');
    if(/\b(?:bet|bets|betting)\b|bltbet|TournamentBet/i.test(text))throw Error(`${file}: old prediction terminology`);
    if(/https?:\/\/(?:localhost|127\.0\.0\.1):\d+/.test(text))throw Error(`${file}: development endpoint`);
  }
  console.log(`Package checks passed: ${html.length} HTML pages, ${files.length} files`);
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) verifyPackage(path.resolve(process.argv[2]??'dist'));
