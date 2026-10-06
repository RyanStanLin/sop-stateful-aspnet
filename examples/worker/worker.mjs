import net from 'node:net';
const command=(...args)=>'*'+args.length+'\r\n'+args.map(value=>'$'+Buffer.byteLength(value)+'\r\n'+value+'\r\n').join('');
setInterval(()=>{const s=net.connect(6379,process.env.REDIS_HOST||'redis',()=>{if(process.env.REDIS_PASSWORD)s.write(command('AUTH',process.env.REDIS_PASSWORD));s.write(command('INCR','worker-counter'));});let reply='';s.on('data',b=>{reply+=b.toString();if(reply.includes(':')){console.log('worker increment succeeded');s.end();}});s.setTimeout(3000,()=>s.destroy());s.on('error',()=>console.error('Redis unavailable'));},5000);
