const origin = process.argv[2];
if (!origin) throw new Error('Usage: node scripts/ws-smoke.mjs https://project.ryanl.in');
const url = new URL('/ws', origin); url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
const socket = new WebSocket(url);
const deadline = setTimeout(() => { socket.close(); console.error('WebSocket timeout'); process.exitCode = 1; }, 10000);
socket.addEventListener('open', () => socket.send('personal-paas-websocket-proof'));
socket.addEventListener('message', event => { if (event.data !== 'personal-paas-websocket-proof') throw new Error('Echo differs'); clearTimeout(deadline); console.log('WebSocket echo OK'); socket.close(); });
socket.addEventListener('error', () => { clearTimeout(deadline); console.error('WebSocket connection failed'); process.exitCode = 1; });
