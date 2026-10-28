const canvas=document.getElementById('renderCanvas');
const statusText=document.getElementById('status');
const engine=new BABYLON.Engine(canvas,true);
const scene=new BABYLON.Scene(engine);
scene.clearColor=new BABYLON.Color4(0.063,0.094,0.153,1);
const camera=new BABYLON.FreeCamera('camera',new BABYLON.Vector3(0,18,-18),scene);
camera.setTarget(BABYLON.Vector3.Zero());
new BABYLON.HemisphericLight('light',new BABYLON.Vector3(0,1,0),scene);
const ground=BABYLON.MeshBuilder.CreateGround('ground',{width:18,height:18},scene);
const gm=new BABYLON.StandardMaterial('groundMaterial',scene);
gm.diffuseColor=BABYLON.Color3.FromHexString('#27374D');ground.material=gm;
const room=new URLSearchParams(location.search).get('room')||'class';
const ws=new WebSocket(`${location.protocol==='https:'?'wss':'ws'}://${location.host}/?room=${encodeURIComponent(room)}`);
const players=new Map(); let myId=''; const keys=new Set();
ws.onmessage=e=>{
  const m=JSON.parse(e.data);
  if(m.type==='welcome'){myId=m.id;return;}
  if(m.type!=='state')return;
  const live=new Set(m.players.map(p=>p.id));
  for(const [id,entry] of players)if(!live.has(id)){entry.mesh.dispose();entry.material.dispose();players.delete(id);}
  for(const p of m.players){
    if(!players.has(p.id)){
      const mesh=BABYLON.MeshBuilder.CreateBox(p.id,{size:1},scene);
      const material=new BABYLON.StandardMaterial('mat-'+p.id,scene);
      material.diffuseColor=BABYLON.Color3.FromHexString(p.color);mesh.material=material;
      mesh.position.set(p.x,0.5,p.z);players.set(p.id,{mesh,material,target:mesh.position.clone()});
    }
    players.get(p.id).target.set(p.x,0.5,p.z);
  }
  const me=m.players.find(p=>p.id===myId);
  statusText.textContent=`Room ${room}  Online ${players.size}  You ${myId.slice(0,4)}${me?`　x ${me.x.toFixed(1)} z ${me.z.toFixed(1)}`:''}`;
};
ws.onclose=()=>{keys.clear();statusText.textContent='Disconnected. Refresh to reconnect.';};
ws.onerror=()=>{statusText.textContent='Connection failed. Check the server and room address.';};
const allowed=['w','a','s','d','arrowup','arrowdown','arrowleft','arrowright'];
window.addEventListener('keydown',e=>{const k=e.key.toLowerCase();if(allowed.includes(k)){e.preventDefault();keys.add(k);}});
window.addEventListener('keyup',e=>keys.delete(e.key.toLowerCase()));
window.addEventListener('blur',()=>keys.clear());
document.addEventListener('visibilitychange',()=>{if(document.hidden) keys.clear();});
setInterval(()=>{
  const dx=Number(keys.has('d')||keys.has('arrowright'))-Number(keys.has('a')||keys.has('arrowleft'));
  const dz=Number(keys.has('w')||keys.has('arrowup'))-Number(keys.has('s')||keys.has('arrowdown'));
  if(ws.readyState===WebSocket.OPEN)ws.send(JSON.stringify({type:'input',dx,dz}));
},50);
engine.runRenderLoop(()=>{
  const alpha=1-Math.exp(-12*engine.getDeltaTime()/1000);
  for(const {mesh,target} of players.values()) mesh.position=BABYLON.Vector3.Lerp(mesh.position,target,alpha);
  scene.render();
});
window.addEventListener('resize',()=>engine.resize());
