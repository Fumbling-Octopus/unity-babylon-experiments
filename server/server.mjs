import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { randomUUID } from 'node:crypto';
import { WebSocketServer, WebSocket } from 'ws';

// Class exercise: the server controls movement speed.
const SPEED = 4;
const HZ = 20;
const ROOT = fileURLToPath(new URL('./public/', import.meta.url));
const server = http.createServer((req, res) => {
  const routes = { '/':'index.html', '/index.html':'index.html', '/client.js':'client.js', '/babylon.js':'babylon.js' };
  const file = routes[new URL(req.url, 'http://localhost').pathname];
  if (!file) { res.writeHead(404); return res.end('Not found'); }
  res.setHeader('Content-Type', file.endsWith('.js') ? 'text/javascript' : 'text/html; charset=utf-8');
  fs.createReadStream(path.join(ROOT, file)).pipe(res);
});
const wss = new WebSocketServer({ server, maxPayload:1024 });
const rooms = new Map();
const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));
function snapshot(room) {
  return JSON.stringify({type:'state', players:[...room.values()].map(p => ({id:p.id,x:p.x,z:p.z,color:p.color}))});
}
wss.on('connection', (socket, req) => {
  const roomName = new URL(req.url, 'http://localhost').searchParams.get('room') || 'class';
  if (!/^[a-zA-Z0-9_-]{1,24}$/.test(roomName)) return socket.close(1008, 'Invalid room');
  if (!rooms.has(roomName)) rooms.set(roomName, new Map());
  const room = rooms.get(roomName);
  if (room.size >= 16) return socket.close(1008, 'Room full');
  const id = randomUUID();
  const palette = ['#4DE2C5','#FFB657','#8DA9FF','#F18AC2'];
  const p = {id, socket, x:room.size % 4 * 2-3, z:Math.floor(room.size/4)*2-3, dx:0,dz:0,lastInput:0,color:palette[room.size%4]};
  room.set(id,p);
  socket.send(JSON.stringify({type:'welcome',id,room:roomName}));
  socket.on('error', () => {});
  socket.on('message', raw => {
    let m; try {m=JSON.parse(raw.toString());} catch {return;}
    if (!m || m.type!=='input' || !Number.isFinite(m.dx) || !Number.isFinite(m.dz)) return;
    // Read direction from this connection only. Ignore client-provided ids and positions.
    const length = Math.hypot(m.dx,m.dz);
    p.dx=m.dx/Math.max(1,length); p.dz=m.dz/Math.max(1,length);
    p.lastInput=Date.now();
  });
  socket.on('close', () => {room.delete(id);if(!room.size) rooms.delete(roomName);});
});
setInterval(() => {
  for (const room of rooms.values()) {
    for (const p of room.values()) {
      if (Date.now()-p.lastInput>300) {p.dx=0;p.dz=0;}
      p.x=clamp(p.x+p.dx*SPEED/HZ,-8,8);
      p.z=clamp(p.z+p.dz*SPEED/HZ,-8,8);
    }
    const message=snapshot(room);
    for(const p of room.values()) if(p.socket.readyState===WebSocket.OPEN && p.socket.bufferedAmount<65536) p.socket.send(message);
  }
},1000/HZ).unref();
const port=Number(process.env.PORT || 2567);
server.listen(port,'0.0.0.0',()=>console.log(`Classroom server: http://localhost:${port}/?room=class`));
