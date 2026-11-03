import struct, zlib, json
from pathlib import Path

p = Path('Assets/Models/Generated/Soldier/WarZoneSoldier.fbx')
b = p.read_bytes()
version = struct.unpack_from('<I', b, 23)[0]
wide = version >= 7500
def node(pos):
    end, count, plen, nlen = struct.unpack_from('<QQQB' if wide else '<IIIB', b, pos)
    pos += 25 if wide else 13
    if not end: return None, pos
    name = b[pos:pos+nlen].decode(); pos += nlen
    props=[]
    for _ in range(count):
        t=chr(b[pos]); pos+=1
        if t in 'YCLFDI':
            fmt={'Y':'h','C':'?','L':'q','F':'f','D':'d','I':'i'}[t]
            v=struct.unpack_from('<'+fmt,b,pos)[0]; pos+=struct.calcsize(fmt)
        elif t in 'SR':
            n=struct.unpack_from('<I',b,pos)[0];pos+=4
            v=b[pos:pos+n];pos+=n
            if t=='S':v=v.decode(errors='replace')
            else:
                if name=='Content' and v.startswith(b'\xff\xd8'):
                    out=Path('Assets/Models/Generated/Soldier/Textures/WarZoneSoldier_BaseColor.jpg')
                    out.parent.mkdir(exist_ok=True)
                    out.write_bytes(v)
                v={'bytes':n,'prefix':v[:16].hex()}
        elif t in 'fdlibc':
            n,enc,size=struct.unpack_from('<III',b,pos);pos+=12
            v={'array':t,'length':n};pos+=size
        else:raise ValueError(t)
        props.append(v)
    kids=[]
    while pos < end-(25 if wide else 13):
        k,pos=node(pos)
        if k:kids.append(k)
    return {'name':name,'props':props,'children':kids},end
nodes=[];pos=27
while pos<len(b):
    n,pos=node(pos)
    if not n:break
    nodes.append(n)
Path('AgentScripts/fbx_structure.json').write_text(json.dumps(nodes,indent=2))
for n in nodes:
    if n['name']=='Objects':
        for o in n['children']:
            if o['name'] in ['Model','Material','Texture','Video']:
                print(o['name'],o['props'])
                if o['name']!='Model':print(json.dumps(o['children'])[:4000])
