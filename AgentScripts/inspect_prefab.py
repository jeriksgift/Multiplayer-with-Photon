import re
from pathlib import Path
s=Path('Assets/Models/Generated/Soldier/WarZoneSoldier.prefab').read_text()
parts=re.split(r'--- !u!',s)[1:]
names={};trans={}
def vec(text,key):
    m=re.search(key+r': \{([^}]+)\}',text)
    return [float(v) for v in re.findall(r': ([^,}]+)',m[1])]
for p in parts:
    typ,ident=map(int,re.match(r'(\d+) &(\d+)',p).groups())
    if typ==1:names[ident]=re.search(r'm_Name: (.*)',p)[1]
    if typ==4:trans[ident]=(int(re.search(r'm_GameObject: \{fileID: (\d+)',p)[1]),int(re.search(r'm_Father: \{fileID: (\d+)',p)[1]),vec(p,'m_LocalPosition'),vec(p,'m_LocalRotation'),vec(p,'m_LocalScale'))
def mul(a,b):
    x,y,z,w=a;X,Y,Z,W=b
    return [w*X+x*W+y*Z-z*Y,w*Y-x*Z+y*W+z*X,w*Z+x*Y-y*X+z*W,w*W-x*X-y*Y-z*Z]
def rot(q,v):return mul(mul(q,v+[0]),[-q[0],-q[1],-q[2],q[3]])[:3]
cache={}
def world(i):
    if i in cache:return cache[i]
    g,parent,p,q,sc=trans[i]
    if parent:
        P,Q,S=world(parent);v=rot(Q,[a*b for a,b in zip(p,S)])
        p=[a+b for a,b in zip(P,v)];q=mul(Q,q);sc=[a*b for a,b in zip(S,sc)]
    cache[i]=(p,q,sc);return cache[i]
for i,(g,parent,*_) in trans.items():
    print(names[g], 'parent='+names[trans[parent][0]] if parent else '', 'position='+str([round(v,4) for v in world(i)[0]]))
