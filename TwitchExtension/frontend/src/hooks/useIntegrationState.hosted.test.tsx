import { renderHook, cleanup } from '@testing-library/react';
import { afterEach, expect, test, vi } from 'vitest';
import { useIntegrationState } from './useIntegrationState';
vi.mock('../environment',()=>({isLocalHost:()=>false,isLiveLocalIntegration:()=>false}));
afterEach(cleanup);
test('hosted frontend starts disconnected without demo campaign or hero',()=>{
 const {result}=renderHook(()=>useIntegrationState(null));
 expect(result.current.connected).toBe(false);
 expect(result.current.gameStarted).toBe(false);
 expect(result.current.mission.active).toBe(false);
 expect(result.current.mission.combatants).toEqual([]);
 expect(result.current.viewer.adopted).toBe(false);
});
