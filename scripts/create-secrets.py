#!/usr/bin/env python3
"""Create ignored local development/deployment demo credentials without printing them."""
import json,os,pathlib,secrets
file=pathlib.Path('paas.secrets.json')
flags=os.O_WRONLY|os.O_CREAT|os.O_EXCL
with os.fdopen(os.open(file,flags,0o600),'w') as stream:
    json.dump({key:secrets.token_urlsafe(32) for key in ['POSTGRES_PASSWORD','REDIS_PASSWORD','API_TOKEN']},stream,indent=2)
    stream.write('\n')
print('Created paas.secrets.json (mode 600). Save its JSON as the repository Actions secret PAAS_SECRETS. Never commit it.')
