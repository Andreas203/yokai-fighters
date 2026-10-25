# Diagnosis helper (YOK-146): base-colour size, UV island count and a UV layout image. blender -b --python tools/blender/texture_diagnose.py
import os
import bpy,bmesh
S=os.path.abspath("assets/specs/texture/ryo-tripo/diagnosis")+"/"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath="game/assets/generated/characters/ryo/ryo-tripo-rigged.glb")
o=[x for x in bpy.data.objects if x.name=="ryo"][0]
img=o.data.materials[0].node_tree.nodes
for n in img:
    if n.type=="TEX_IMAGE":
        im=n.image;print("IMG",im.size[:],im.name);im.scale(1024,1024);im.filepath_raw=S+"basecolor-1024.png";im.file_format="PNG";im.save()
bm=bmesh.new();bm.from_mesh(o.data);uv=bm.loops.layers.uv.active
# islands by UV-connectivity
par={}
def f(a):
    while par.setdefault(a,a)!=a:
        par[a]=par[par[a]];a=par[a]
    return a
key=lambda l:(round(l[uv].uv.x,5),round(l[uv].uv.y,5))
for fc in bm.faces:
    ks=[(l.vert.index,)+key(l) for l in fc.loops]
    for k in ks[1:]: par[f(k)]=f(ks[0])
roots=set(f(k) for k in list(par))
area={}
tot=0
for fc in bm.faces:
    k=f((fc.loops[0].vert.index,)+key(fc.loops[0]))
    p=[l[uv].uv for l in fc.loops];a=0
    for i in range(1,len(p)-1):
        a+=abs((p[i].x-p[0].x)*(p[i+1].y-p[0].y)-(p[i+1].x-p[0].x)*(p[i].y-p[0].y))/2
    area[k]=area.get(k,0)+a;tot+=a
s=sorted(area.values(),reverse=True)
print("UVSTAT islands",len(s),"cover",round(tot,3),"top5",[round(x,3) for x in s[:5]],"small(<0.0005)",sum(1 for x in s if x<0.0005))
# uv layout render as image
W=1024
buf=[1.0]*(W*W*4)
for fc in bm.faces:
    p=[l[uv].uv for l in fc.loops]
    # draw edges
    for i in range(len(p)):
        a=p[i];b=p[(i+1)%len(p)]
        for t in range(0,40):
            x=int((a.x+(b.x-a.x)*t/40)*(W-1));y=int((a.y+(b.y-a.y)*t/40)*(W-1))
            if 0<=x<W and 0<=y<W:
                j=(y*W+x)*4;buf[j]=buf[j+1]=buf[j+2]=0
im2=bpy.data.images.new("uv",W,W);im2.pixels=buf;im2.filepath_raw=S+"uv-layout-1024.png";im2.file_format="PNG";im2.save()
