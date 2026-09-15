import { render, screen, cleanup } from '@testing-library/react';
import { afterEach, expect, test, vi } from 'vitest';
import { BattleWorkspace } from './BattleWorkspace';
import type { MissionCombatant, MissionState, ViewerIdentity } from '../types';
afterEach(cleanup);
const identity:ViewerIdentity={token:'test',channelId:'42',userId:'9',displayName:'Viewer',roles:['viewer'],linked:true};
const hero=(id:string,name:string,hp:number)=>({id,name,hp,maxHp:100,state:'active',isPlayerSide:true,tournamentTeam:-1,cooldownSecondsRemaining:0,activePowerFractionRemaining:0,kills:3,retinue:0,eliteRetinue:0,goldEarned:10,xpEarned:20,ammoCurrent:14,ammoMaximum:30} as MissionCombatant);
const mission=(heroes:MissionCombatant[])=>({active:true,kind:'battle',revision:1,deploymentFinished:true,combatants:heroes,actionAvailability:{}} as MissionState);
function props(heroes:MissionCombatant[],heroId?:string){return {mission:mission(heroes),heroId,actions:[],identity,selectors:{cultures:[],heroes:[],clans:[],kingdoms:[],settlements:[],skills:[]},cooldowns:{},busy:false,onRequestIdentity:vi.fn(),onSubmit:vi.fn()};}
test('solo owner appears in personal HUD and roster with real name',()=>{
 render(<BattleWorkspace {...props([hero('a','Alice',73)],'a')}/>);
 expect(screen.getAllByText('Alice')).toHaveLength(2);
 expect(document.querySelectorAll('.battle-roster-tile')).toHaveLength(1);
 expect(document.querySelector('.hero-hud')?.textContent).toContain('14/30');
});
test('stable ID wins over duplicate display names; other participants remain',()=>{
 render(<BattleWorkspace {...props([hero('a','Same',73),hero('b','Same',42)],'b')}/>);
 expect(document.querySelector('.hero-hud')?.textContent).toContain('42 / 100 HP');
 expect(document.querySelectorAll('.battle-roster-tile')).toHaveLength(2);
});
test('no identity never guesses by name; arrival and death update personal state',()=>{
 const view=render(<BattleWorkspace {...props([hero('a','Viewer',73)])}/>);
 expect(document.querySelector('.hero-hud')).toBeNull();
 view.rerender(<BattleWorkspace {...props([hero('a','Viewer',73)],'a')}/>);
 expect(document.querySelector('.hero-hud')).not.toBeNull();
 view.rerender(<BattleWorkspace {...props([{...hero('a','Viewer',0),state:'killed'}],'a')}/>);
 expect(document.querySelector('.hero-hud')?.textContent).toContain('0 / 100 HP');
 view.rerender(<BattleWorkspace {...props([],'a')}/>);
 expect(document.querySelector('.hero-hud')).toBeNull();
});
