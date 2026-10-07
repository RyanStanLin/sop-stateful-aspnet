#!/usr/bin/env python3
"""Create a private local development environment, separate from VPS secrets."""
import os,secrets
settings={"DATABASE_HOST":"127.0.0.1","DATABASE_PORT":"15432","DATABASE_NAME":"app","DATABASE_USER":"app","DATABASE_PASSWORD":secrets.token_hex(32),"REDIS_HOST":"127.0.0.1","REDIS_PORT":"16379","REDIS_PASSWORD":secrets.token_hex(32),"API_TOKEN":secrets.token_hex(32),"CONFIG_PATH":os.path.abspath("local-data/config.json"),"ASPNETCORE_URLS":"http://localhost:8080"}
with os.fdopen(os.open('.env.local',os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600),'w') as file:
 for key,value in settings.items():file.write(key+'='+value+'\n')
print('Created private .env.local with independent development credentials.')
