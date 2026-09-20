import os, json, socket, struct, base64, hashlib, hmac, time, uuid, subprocess, urllib.request
channel='blt-review-test-'+uuid.uuid4().hex
user='review-user'; credential=uuid.uuid4().hex; sockets=[]
def run(*args,input=None): return subprocess.check_output(args,input=input,text=True).strip()
info=json.loads(run('docker','inspect','backend-service-1'))[0]
env=dict(x.split('=',1) for x in info['Config']['Env']);secret=base64.b64decode(env['TWITCH_EXTENSION_SECRET'])
ip=next(iter(info['NetworkSettings']['Networks'].values()))['IPAddress']
def sql(q): return run('docker','exec','-i','backend-postgres-1','psql','-U','blt','-d','blt','-At','-v','ON_ERROR_STOP=1',input=q)
def b64(b):return base64.urlsafe_b64encode(b).decode().rstrip('=')
def token():
 s=b64(b'{"alg":"HS256","typ":"JWT"}')+'.'+b64(json.dumps(dict(channel_id=channel,user_id=user,opaque_user_id='U'+user,role='broadcaster',exp=int(time.time())+600)).encode())
 return s+'.'+b64(hmac.new(secret,s.encode(),hashlib.sha256).digest())
tok=token()
class WS:
 def __init__(self,path,protocol,auth=None):
  self.s=socket.create_connection((ip,8080),timeout=8);sockets.append(self.s)
  request=f'GET {path} HTTP/1.1\r\nHost: {ip}:8080\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: {base64.b64encode(os.urandom(16)).decode()}\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Protocol: {protocol}\r\n'
  if auth:request+='Authorization: Bearer '+auth+'\r\n'
  self.s.sendall((request+'\r\n').encode());header=b''
  while not header.endswith(b'\r\n\r\n'):header+=self.s.recv(1)
  assert b'101 Switching Protocols' in header,header.split(b'\r\n')[0]
 def read(self,n):
  b=b''
  while len(b)<n:
   part=self.s.recv(n-len(b))
   if not part:raise RuntimeError('socket closed')
   b+=part
  return b
 def recv(self):
  a,b=self.read(2);n=b&127
  if n==126:n=struct.unpack('!H',self.read(2))[0]
  elif n==127:n=struct.unpack('!Q',self.read(8))[0]
  payload=self.read(n)
  if a&15!=1:return self.recv()
  return json.loads(payload)
 def until(self,kind,predicate=lambda x:True):
  for i in range(30):
   msg=self.recv()
   if msg.get('kind')==kind and predicate(msg):return msg
  raise AssertionError('No '+kind)
 def send(self,kind,data):
  p=json.dumps(dict(v=1,id=data.get('requestId',str(uuid.uuid4())),kind=kind,channelId=channel,timestamp='2026-09-15T20:00:00Z',data=data)).encode();n=len(p);mask=os.urandom(4)
  header=bytes([129,128+n]) if n<126 else bytes([129,254])+struct.pack('!H',n)
  self.s.sendall(header+mask+bytes(v^mask[i%4] for i,v in enumerate(p)))
def http(path,method='GET',body=None):
 req=urllib.request.Request('http://'+ip+':8080/api/channels/'+channel+path,data=None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+tok,'Content-Type':'application/json'},method=method)
 with urllib.request.urlopen(req,timeout=8) as r:return json.load(r)
try:
 sql("INSERT INTO installations(installation_id,channel_id,credential_hash,created_at) VALUES('"+str(uuid.uuid4())+"','"+channel+"','"+hashlib.sha256(credential.encode()).hexdigest().upper()+"',now());")
 game=WS('/ws/game/'+channel,'blt.integration.v1',credential);game.until('configuration.updated')
 old=WS('/ws/viewer/'+channel+'?token='+tok,'blt.viewer.v1')
 new=WS('/ws/viewer/'+channel+'?token='+tok+'&hud=hero-id-v1','blt.viewer.v1')
 def snapshot(rev,rows):game.send('state.snapshot',dict(connected=True,gameStarted=True,commands=[dict(name='bltbet',handler='TournamentBet',help='bet on a team')],mission=dict(active=True,kind='battle',revision=rev,combatants=rows)))
 rows=[dict(id='hero-a',name='Alice',hp=73,maxHp=100,ammoCurrent=14,ammoMaximum=30,state='active'),dict(id='hero-b',name='Bob',hp=42,maxHp=100,state='active')]
 snapshot(100,rows)
 old.until('state.snapshot');new.until('state.snapshot')
 game.send('viewer.state',dict(userId=user,adopted=True,heroId='hero-a',heroName='Alice',gold=100))
 a=old.until('state.snapshot');b=new.until('state.snapshot')
 assert [x['name'] for x in a['data']['mission']['combatants']]==['Viewer','Alice','Bob']
 assert [x['name'] for x in b['data']['mission']['combatants']]==['Alice','Bob']
 assert b['data']['commands'][0]['name']=='predict'
 last=b['data']['mission']['revision']
 snapshot(101,rows[:1]);a=old.until('state.snapshot');b=new.until('state.snapshot')
 assert len(a['data']['mission']['combatants'])==2 and len(b['data']['mission']['combatants'])==1
 game.send('viewer.state',dict(userId=user,adopted=True,heroName='Alice'))
 a=old.until('state.snapshot');assert len(a['data']['mission']['combatants'])==1
 cfg=http('/configuration');cfg['commands']=[dict(actionId='command.bltbet',enabled=False,settings={'Amount':1234,'Help':'Bet on a team','Name':'bltbet'})]
 cfg['profiles']=[]
 saved=http('/configuration','PUT',cfg)
 commands=saved['commands'];assert commands[0]['actionId']=='command.predict' and commands[0]['enabled']==False and commands[0]['settings']['Amount']==1234
 assert 'bltbet' not in json.dumps(saved) and 'Bet on' not in json.dumps(saved)
 context=http('/configuration/context');assert context['runtimeCommands'][0]['name']=='predict'
 print('PASS live authenticated old/new sockets simultaneously; full and solo rosters; native ownership; missing ownership; canonical runtime/config metadata; disabled setting and custom amount preservation.')
 from datetime import datetime,timezone
 from urllib.error import HTTPError
 def submission(**values):return dict(requestId=str(uuid.uuid4()),timestamp=datetime.now(timezone.utc).isoformat(),**values)
 for name in ['predict','bltbet']:
  try:http('/commands','POST',submission(commandLine='!'+name+' red 1234'));raise AssertionError('Disabled command accepted')
  except HTTPError as e:assert e.code==403
 saved['commands'][0]['enabled']=True
 for profile in saved.get('profiles',[]):
  for cmd in profile['commands']:cmd['enabled']=True
 stale=json.loads(json.dumps(saved))
 saved=http('/configuration','PUT',saved)
 try:http('/configuration','PUT',stale);raise AssertionError('Stale revision accepted')
 except HTTPError as e:assert e.code==409
 request=submission(commandLine='!predict red 1234');http('/commands','POST',request)
 forwarded=game.until('command.request');assert forwarded['data']['commandLine']=='predict red 1234'
 game.send('action.result',dict(requestId=request['requestId'],messages=['Prediction recorded for red. Gold committed: 1234']))
 old.until('action.result');new.until('action.result')
 print('PASS Predict discovery, disabled legacy/canonical commands, authenticated execution routing and private result delivery.')

 game.s.close();time.sleep(.5)
 game=WS('/ws/game/'+channel,'blt.integration.v1',credential);game.until('configuration.updated');game.until('viewer.subscribe');snapshot(1,rows)
 b=new.until('state.snapshot',lambda m:m['data']['mission']['revision']>last)
 assert b['data']['gameStarted'] is True
 print('PASS game reconnect restores authenticated viewer subscription, accepts reset source revision and sends increasing client revision.')
finally:
 for s in sockets:s.close()
 sql("DELETE FROM action_audit WHERE channel_id='"+channel+"';DELETE FROM channel_configurations WHERE channel_id='"+channel+"';DELETE FROM installations WHERE channel_id='"+channel+"';")
 print('Synthetic test installation/configuration removed.')
