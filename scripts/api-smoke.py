#!/usr/bin/env python3
"""Exercise enabled PostgreSQL/Redis/config endpoints; credentials never appear in output."""
import argparse,json,pathlib,urllib.request,uuid
p=argparse.ArgumentParser();p.add_argument('origin');p.add_argument('--secrets',default='paas.secrets.json');a=p.parse_args()
token=json.loads(pathlib.Path(a.secrets).read_text())['API_TOKEN']
origin=a.origin.rstrip('/');key='smoke-'+uuid.uuid4().hex;value='persistent-'+uuid.uuid4().hex
def call(path,method='GET',body=None):
    request=urllib.request.Request(origin+path,data=None if body is None else json.dumps(body).encode(),method=method,headers={'Content-Type':'application/json','X-Demo-Token':token})
    with urllib.request.urlopen(request,timeout=15) as response:return json.load(response)
for service in ['postgres','redis']:
    path='/api/data/'+service+'/'+key
    call(path,'PUT',{'value':value})
    assert call(path)['value']==value
    print(service+' write/read OK')
call('/api/data/config','PUT',{'revision':1,'theme':'dark','proof':value})
assert call('/api/data/config')['value']['proof']==value
print('JSON config write/read OK')
