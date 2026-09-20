import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { verifyPackage } from './verify-package.mjs';
const helper='<script src="https://extension-files.twitch.tv/helper/v1/twitch-ext.min.js"></script>';
function fixture(html,run){const root=fs.mkdtempSync(path.join(os.tmpdir(),'blt-package-test-'));try{fs.writeFileSync(path.join(root,'index.html'),html);fs.writeFileSync(path.join(root,'app.js'),'void 0;');run(root)}finally{fs.rmSync(root,{recursive:true,force:true})}}
test('valid helper-first output passes',()=>fixture(helper+'<script src="./app.js"></script>',root=>assert.doesNotThrow(()=>verifyPackage(root))));
test('Vite head reordering is rejected',()=>fixture('<script src="./app.js"></script>'+helper,root=>assert.throws(()=>verifyPackage(root),/Helper/)));
test('missing helper and asynchronous helper are rejected',()=>{
 fixture('<script src="./app.js"></script>',root=>assert.throws(()=>verifyPackage(root),/Helper/));
 fixture(helper.replace('<script','<script async'),root=>assert.throws(()=>verifyPackage(root),/Helper/));
});
test('every extra HTML page is checked',()=>fixture(helper,root=>{fs.writeFileSync(path.join(root,'forgotten.html'),'<html></html>');assert.throws(()=>verifyPackage(root),/Helper/)}));
test('missing assets, old terminology, and development endpoints fail',()=>{
 fixture(helper+'<script src="./missing.js"></script>',root=>assert.throws(()=>verifyPackage(root),/asset/));
 fixture(helper+'<p>Bet</p>',root=>assert.throws(()=>verifyPackage(root),/terminology/));
 fixture(helper,root=>{fs.writeFileSync(path.join(root,'app.js'),'http://127.0.0.1:5188');assert.throws(()=>verifyPackage(root),/development endpoint/)});
});
