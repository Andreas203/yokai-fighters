# Generator for merchant-shop.svg and merchant-shop-states.svg (YOK-159 rough mockup).
# Run: python gen_merchant_shop.py   then render with headless Chrome/Edge at 1920x1080.
from xml.sax.saxutils import escape
INK="#1D1B21"; PAPER="#F1E8D4"; PAPER2="#F8F1E0"; IND="#2D3A5E"; PERS="#D8632C"; PINE="#34483B"
RED="#B5332B"; FOX="#F2A23A"; BRASS="#B08A48"; WOOD="#5A3E2A"; WOOD2="#46301F"; WOODL="#8A6440"
GREY="#B9B4A6"; GREYD="#8C8778"; MUTE="#7A7054"
F="font-family=\"Georgia, 'Times New Roman', serif\""

class S:
    def __init__(s): s.o=[]
    def add(s,x): s.o.append(x)
    def r(s,x,y,w,h,fill="none",stroke=INK,sw=2,rx=0,dash=None):
        d=f' stroke-dasharray="{dash}"' if dash else ""
        s.add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{fill}" stroke="{stroke if stroke else "none"}" stroke-width="{sw}"{d}/>')
    def l(s,x1,y1,x2,y2,stroke=INK,sw=2,dash=None):
        d=f' stroke-dasharray="{dash}"' if dash else ""
        s.add(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{stroke}" stroke-width="{sw}"{d}/>')
    def c(s,x,y,r,fill,stroke=INK,sw=2):
        s.add(f'<circle cx="{x}" cy="{y}" r="{r}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}"/>')
    def p(s,d,fill="none",stroke=INK,sw=2):
        s.add(f'<path d="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}" stroke-linejoin="round" stroke-linecap="round"/>')
    def t(s,x,y,txt,size=18,fill=INK,w="normal",a="start",i=False):
        st=' font-style="italic"' if i else ""
        s.add(f'<text x="{x}" y="{y}" {F} font-size="{size}" fill="{fill}" font-weight="{w}" text-anchor="{a}"{st}>{escape(txt)}</text>')
    def lines(s,x,y,arr,size=18,fill=INK,lh=None,**k):
        lh=lh or size*1.3
        for n,a in enumerate(arr): s.t(x,y+n*lh,a,size,fill,**k)
    def svg(s,w=1920,h=1080,bg=IND):
        return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}"><rect width="{w}" height="{h}" fill="{bg}"/>'+"".join(s.o)+'</svg>'

def strip(s,x,w,label="ROUGH PROTOTYPE · AWAITING ASTRA PASS · YOK-159"):
    s.r(x,18,w,32,RED,None,0)
    s.t(x+w/2,40,label,16,PAPER,"bold","middle")

def scroll_frame(s):
    s.r(0,780,1920,300,"#26314F",None,0)
    s.t(960,76,"backdrop: night bamboo valley (reuse night-backdrop plate)",16,"#7C88AE",a="middle",i=True)
    s.r(132,100,1656,880,PINE,INK,2); s.r(132,122,1656,836,PAPER,INK,2)
    for x in (137,142,1778,1783): s.l(x,126,x,954,"#C9BE9F",2)
    for x in (94,1788):
        s.r(x,84,38,912,WOOD,INK,2,6)
        s.r(x-6,66,50,20,WOOD2,INK,2,10); s.r(x-6,994,50,20,WOOD2,INK,2,10)

def seal(s,cx,cy,r=18,txt="印"):
    s.r(cx-r,cy-r,2*r,2*r,RED,INK,2,4); s.t(cx,cy+r*0.4,txt,r*1.1,PAPER,"bold","middle")

def coin(s,x,y,r=11):
    s.c(x,y,r,"#D9B35E",BRASS,2); s.r(x-r/3,y-r/3,2*r/3,2*r/3,"none",BRASS,1.5)

def chip(s,x,y,key,label,kw):
    s.r(x,y,kw,34,PAPER2,INK,2,6); s.t(x+kw/2,y+24,key,18,INK,"bold","middle")
    s.t(x+kw+10,y+24,label,20,INK)
    return x+kw+10+len(label)*10+30

def bubble(s,x,y,w,h,arr,tail,size=22,event=None):
    """Brush-ink speech bubble; tail=(tx,ty) tip. Body drawn twice (offset stroke) for a brush look."""
    tx,ty=tail
    if ty>y+h:
        bx=min(max(tx-23,x+30),x+w-80)
        body=f'M{x+16},{y} L{x+w-16},{y} Q{x+w},{y} {x+w},{y+16} L{x+w},{y+h-16} Q{x+w},{y+h} {x+w-16},{y+h} L{bx+46},{y+h} L{tx},{ty} L{bx},{y+h} L{x+16},{y+h} Q{x},{y+h} {x},{y+h-16} L{x},{y+16} Q{x},{y} {x+16},{y} Z'
    else:
        my=min(max(ty,y+30),y+h-30)
        body=f'M{x+16},{y} L{x+w-16},{y} Q{x+w},{y} {x+w},{y+16} L{x+w},{y+h-16} Q{x+w},{y+h} {x+w-16},{y+h} L{x+16},{y+h} Q{x},{y+h} {x},{y+h-16} L{x},{my+16} L{tx},{ty} L{x},{my-16} L{x},{y+16} Q{x},{y} {x+16},{y} Z'
    s.p(body,"none","#3a3640",7)
    s.p(body,PAPER2,INK,3)
    s.lines(x+w/2,y+h/2-(len(arr)-1)*size*0.65+size*0.35,arr,size,INK,a="middle",i=True)
    if event: s.t(x,y-8,event,13,MUTE,i=True)

def portrait(s,x,y,w,h,small=False):
    s.r(x,y,w,h,"#E3D8BD",MUTE,2,2,"8 6")
    cx=x+w/2
    s.c(cx,y+h*0.36,w*0.2,"#D3C7A8","#B7AB8B",2)
    s.p(f"M{x+w*0.12},{y+h} Q{x+w*0.14},{y+h*0.62} {cx},{y+h*0.58} Q{x+w*0.86},{y+h*0.62} {x+w*0.88},{y+h} Z","#D3C7A8","#B7AB8B",2)
    if not small:
        s.r(x+8,y+h-34,w-16,24,PAPER,None,0,2)
        s.t(cx,y+h-17,"portrait (human guise)",13,MUTE,a="middle",i=True)

# ---------- shelf pieces ----------
def tag(s,x,y,name,price,state="normal",price_txt=None,k=1.0):
    """Hanging price tag; (x,y) = left edge, top of body. k scales the whole tag."""
    g=state!="normal"
    s.add(f'<g transform="translate({x},{y-10*k}) scale({k})">')
    s.c(90,5,3.5,INK,INK,1)
    s.p("M4,10 L134,10 L134,54 L4,54 L-6,32 Z",PAPER2 if not g else "#D9D4C6",INK,2)
    s.c(8,32,3.5,"#E3D8BD",INK,1.5)
    s.t(74,29,name,14,INK if not g else GREYD,"bold","middle")
    if state=="bought":
        s.t(74,48,"SOLD · borrowed ink",11,GREYD,a="middle",i=True)
    else:
        coin(s,44,43,7)
        s.t(56,48,price_txt or str(price),15,(RED if not g else GREYD),"bold")
    s.add('</g>')

def talisman(s,cx,base,letter,kind,name,state="normal",lift=0,price=50,price_txt=None,k=1.0):
    """Standing ofuda on a stand; base = shelf board top. Type mark is the band text (red = special, indigo = modifier).
    Returns (paper top, hole x, hole y)."""
    g=state!="normal"
    paper=PAPER2 if not g else "#D9D4C6"
    band=(RED if kind=="special" else IND) if not g else GREYD
    icon=({"special":PERS,"modifier":IND}[kind]) if not g else GREYD
    pw=80*k; ph=100 if k<=1 else 128; hw=pw/2
    s.r(cx-46*k,base-12,92*k,12,WOODL,INK,2,2); s.r(cx-30*k,base-18,60*k,8,WOOD,INK,2,2)
    top=base-12-lift-6-ph
    if lift:
        s.add(f'<ellipse cx="{cx}" cy="{base-1}" rx="{hw+22}" ry="6" fill="{FOX}" opacity="0.6"/>')
        s.r(cx-hw-12,top-8,pw+24,ph+20,"none",FOX,5,6); s.r(cx-hw-18,top-14,pw+36,ph+32,"none",FOX,2,8,"4 4")
    s.r(cx-hw,top,pw,ph,paper,INK,2.5,2)
    bh=18 if k<=1 else 24
    s.r(cx-hw,top,pw,bh,band,INK,2.5,0); s.t(cx,top+bh-6,kind.upper(),11 if k<=1 else 14,PAPER,"bold","middle")
    isz=40 if k<=1 else 50
    s.r(cx-isz/2,top+bh+6,isz,isz,icon,None,0,6); s.t(cx,top+bh+6+isz*0.72,letter,24 if k<=1 else 32,PAPER,"bold","middle")
    tg=top+bh+6+isz+4
    tk=0.9 if k<=1 else 1.5
    tag(s,cx-65*tk,tg,name,price,state,price_txt,tk)
    return top,cx+25*tk,tg

def shelf_unit(s,x,y,w,h,rows):
    s.r(x,y,w,h,WOOD2,INK,3,4)
    ix=x+24; iw=w-48
    s.r(ix,y+22,iw,h-34,"#CDBB94",INK,2)
    for k in range(0,int(iw),48): s.l(ix+k,y+22,ix+k,y+h-12,"#C0AE86",1)
    s.r(x-10,y-12,w+20,22,WOOD,INK,3,3); s.r(x-6,y+h-12,w+12,18,WOOD,INK,3,3)
    ch=(h-34)/rows; out=[]
    for i in range(rows):
        top=y+22+i*ch; bt=top+ch-14
        s.r(ix-2,bt,iw+4,14,WOODL,INK,2.5,1); s.l(ix,bt+4,ix+iw,bt+4,"#6b4a34",1)
        out.append((top,bt))
    return out,ix,iw


def slot_row(s,x,y,w,n,letter,name,lvl,focused=False,target=False,empty=False,h=84):
    if empty:
        s.r(x,y,w,h,"none",MUTE,2,4,"6 5"); s.t(x+12,y+22,f"SLOT {n}",13,MUTE,"bold")
        s.t(x+w/2,y+h/2+12,"empty: a bought special goes here",13,MUTE,a="middle",i=True); return
    s.r(x,y,w,h,PAPER2,PERS if focused else INK,4 if focused else 2,4)
    if target: s.r(x-5,y-5,w+10,h+10,"none",FOX,3,8)
    s.t(x+12,y+20,f"SLOT {n}",12,MUTE,"bold")
    s.r(x+12,y+28,44,44,PERS,None,0,6); s.t(x+34,y+58,letter,26,PAPER,"bold","middle")
    s.t(x+68,y+46,name,19,INK,"bold"); s.t(x+68,y+68,f"Lv {lvl}"+("  ·  Tanuki target" if target else ""),14,"#B5600E" if target else "#4A463C",i=target)

def slots_scroll(s,x,y,w,h,focus_slot=None):
    cx=x+w/2
    s.l(cx,y-26,x+44,y+4,WOOD,2); s.l(cx,y-26,x+w-44,y+4,WOOD,2); s.c(cx,y-28,6,WOOD)
    s.r(x,y,w,h,PAPER2,INK,2.5,2)
    s.r(x,y,w,20,IND,None,0); s.r(x,y+h-20,w,20,IND,None,0)
    s.r(x-8,y-10,w+16,12,WOOD,INK,1.5,3); s.r(x-8,y+h-2,w+16,12,WOOD,INK,1.5,3)
    s.r(x-18,y-12,12,16,WOOD2,INK,1.5,6); s.r(x+w+6,y-12,12,16,WOOD2,INK,1.5,6)
    s.t(cx,y+52,"YOUR SLOTS",24,INK,"bold","middle"); s.t(cx,y+74,"release a binding · 40",14,MUTE,a="middle",i=True)
    ry=y+92
    for n,(l,nm,lv) in enumerate([("S","Spirit Wave",1),("R","Rising Talisman",1),("F","Foxfire",2)],1):
        slot_row(s,x+16,ry,w-32,n,l,nm,lv,focused=(focus_slot==n),target=(nm=="Foxfire"))
        if focus_slot==n:
            s.r(x+w-16-120,ry+46,104,28,RED,INK,2,6); s.t(x+w-16-68,ry+66,"Release 40",15,PAPER,"bold","middle")
        ry+=100
    slot_row(s,x+16,ry,w-32,4,"","","",empty=True)
    s.t(x+16,y+h-52,"Releasing frees a slot; the move loses",13,"#4A463C"); s.t(x+16,y+h-34,"its levels (A6). Foxfire: Tanuki target (T2).",13,"#4A463C")

def hang(s,ceiling,res):
    pt,hx,hy=res
    s.l(hx,ceiling,hx,hy,"#6b4a34",1.8)

def slip(s,x,y,w,h,title,kind_line,blocks,foot=None):
    """Paper ledger slip pinned with a red pin. blocks = list of (heading, [lines])."""
    s.r(x+4,y+5,w,h,"#1D1B2133",None,0,2)
    s.r(x,y,w,h,PAPER2,INK,2.5,2)
    s.r(x,y,w,26,IND,None,0)
    s.t(x+12,y+19,"LEDGER SLIP · frame data",14,PAPER,"bold")
    s.c(x+w-18,y+13,6,RED,INK,1.5)
    s.t(x+12,y+56,title,20,INK,"bold"); s.t(x+12,y+78,kind_line,14,"#4A463C")
    yy=y+104
    for hd,lines in blocks:
        s.t(x+12,yy,hd,12,RED,"bold"); yy+=20
        for ln in lines:
            s.t(x+12,yy,ln,15,INK); yy+=21
        yy+=8
    if foot:
        yy=y+h-18-(len(foot)-1)*16
        for ln in foot:
            s.t(x+12,yy,ln,12,MUTE,i=True); yy+=16

def key_footer(s,x,y,frame_on=False):
    nx=chip(s,x,y,"Arrows / D-pad","Move",150)
    nx=chip(s,nx,y,"Enter / A","Buy / Release",100)
    nx=chip(s,nx,y,"Tab / Y","Frame data: ON" if frame_on else "Frame data",78)
    chip(s,nx,y,"Esc / B","Back to rest stop",90)

# ================= MAIN SHEET =================
def main():
    s=S()
    strip(s,560,800)
    scroll_frame(s)
    s.t(150,200,"SHOP",92,INK,"bold",i=True); s.r(150,214,224,10,RED,None,0,3)
    s.t(150,256,"Merchant's wares · pay in coins",26)
    s.t(1330,172,"RYO  ·  640 / 1000",26,INK,"bold")
    s.r(1330,182,440,20,"#D8CDB0",INK,2,3); s.r(1332,184,279,16,RED,None,0,2)
    s.t(1330,236,"COINS",20,INK,"bold"); s.r(1410,212,120,36,PAPER2,BRASS,3,18); s.t(1470,238,"75",24,INK,"bold","middle")
    # merchant column
    s.t(160,296,"MERCHANT",18,INK,"bold")
    bubble(s,160,336,460,140,["Ah, Slippery Skin: Slick kappa skin makes","you impossible to throw while the","move starts up."],(250,576),22,
           event="bubble: focus line = lead-in + card.plain, at most 2 sentences, no numbers (A12)")
    portrait(s,160,570,180,310)
    s.t(160,896,"never the Tanuki here (S3)",13,MUTE,i=True)
    slip(s,360,520,260,360,"Slippery Skin","Kappa modifier · common",
         [("CHANGE",["Throw-invulnerable","during startup"]),
          ("FRAME NUMBERS",["unchanged"]),
          ("RIDES ON",["the special you pick"])],
         ["modifier: shows the change, not a table","stays open while focus moves (Tab / Y)"])
    # shelf
    s.t(670,290,"ABILITIES · 50 coins · common · from yokai you have not bound (Kappa)",19,INK,"bold")
    s.t(670,312,"Bought abilities are borrowed ink: grey, this run only.",16,GREYD,i=True)
    rows,ix,iw=shelf_unit(s,670,330,640,556,3)
    cxs=[ix+iw*0.25,ix+iw*0.75]
    items=[[("R","special","River Grab"),("S","special","Shell Spin")],
           [("P","modifier","River Pull"),("Sk","modifier","Slippery Skin")],
           [("C","modifier","Cucumber Gift")]]
    focus=(1,1)
    for ri,(top,bt) in enumerate(rows):
        for ci,(l,k,n) in enumerate(items[ri]):
            cx=cxs[ci]; lift=12 if (ri,ci)==focus else 0
            res=talisman(s,cx,bt,l,k,n,lift=lift,k=1.5)
            hang(s,top,res)
    s.t(1336,360,"SPECIALS first,",14,MUTE,i=True); s.t(1336,378,"then MODIFIERS",14,MUTE,i=True)
    slots_scroll(s,1472,336,290,546)
    key_footer(s,150,902,True)
    return s.svg()

# ================= STATES SHEET =================
def panel(s,x,y,w,h,title,n):
    s.r(x,y,w,h,PAPER,INK,2.5,2); s.r(x,y,w,34,IND,None,0)
    s.t(x+12,y+24,f"{n}  {title}",18,PAPER,"bold")

def mini_shelf(s,x,y,w,h):
    s.r(x,y,w,h,"#CDBB94",INK,2.5,2)
    for k in range(0,int(w),40): s.l(x+k,y,x+k,y+h,"#C0AE86",1)
    s.r(x-4,y+h-12,w+8,14,WOODL,INK,2.5,1)
    return y+h-12

def mini_speaker(s,x,y,arr,ev,w=290,h=70):
    portrait(s,x,y+4,64,86,small=True)
    bubble(s,x+86,y,w,h,arr,(x+64,y+34),17)
    s.t(x+86,y+h+18,ev,12,MUTE,i=True)

def states():
    s=S()
    strip(s,560,800)
    s.t(960,86,"Merchant shop · states (labels are annotation, not UI)",24,PAPER,"bold","middle")
    W=435; H=420; xs=[60+i*(W+20) for i in range(4)]; ys=[110,560,1010]
    # 1 bought
    x,y=xs[0],ys[0]; panel(s,x,y,W,H,"Bought",1)
    mini_speaker(s,x+14,y+46,["Sold! Treat it kindly."],"event: bought_special (replaces the focus line)",290,66)
    bt=mini_shelf(s,x+14,y+160,W-28,230)
    cx=x+110
    s.r(cx-46,bt-12,92,12,GREY,GREYD,2,2)
    s.r(cx-40,bt-60,80,46,"#D9D4C6",GREYD,2.5,23); s.r(cx-46,bt-62,10,50,GREYD,INK,1.5,5); s.r(cx+36,bt-62,10,50,GREYD,INK,1.5,5)
    s.l(cx-8,bt-60,cx-8,bt-14,RED,3); seal(s,cx+14,bt-36,10)
    tag(s,cx-50,bt-130,"River Grab",50,"bought")
    s.l(cx+40,y+170,cx+40,bt-120,"#6b4a34",1.8)
    hang(s,y+170,talisman(s,x+300,bt,"S","special","Shell Spin"))
    s.t(x+14,y+H-10,"Rolled in place, tied, grey, not selectable again.",12,MUTE,i=True)
    # 2 cannot afford
    x,y=xs[1],ys[0]; panel(s,x,y,W,H,"Cannot afford",2)
    mini_speaker(s,x+14,y+46,["Ah, not quite enough coins.","Win a few more, friend."],"event: cannot_afford",290,66)
    s.t(x+W-14,y+130,"COINS",14,INK,"bold","end"); s.r(x+W-110,y+136,96,22,PAPER2,RED,3,11); s.t(x+W-62,y+153,"25",16,INK,"bold","middle")
    bt=mini_shelf(s,x+14,y+176,W-28,214)
    hang(s,y+186,talisman(s,x+110,bt,"R","special","River Grab"))
    hang(s,y+186,talisman(s,x+300,bt,"S","special","Shell Spin","dim",price_txt="Need 50"))
    s.t(x+14,y+H-10,"Dimmed, Enter does nothing, coin chip shakes, nothing changes.",12,MUTE,i=True)
    # 3 slot full
    x,y=xs[2],ys[0]; panel(s,x,y,W,H,"Slot full (buying a special)",3)
    mini_speaker(s,x+14,y+46,["Hands full! A swapped move","forgets its levels, mind."],"event: slot_full",290,66)
    s.t(x+14,y+168,"All four slots are full. Replace which?",18,INK,"bold")
    for n,(l,nm,lv) in enumerate([("S","Spirit Wave",1),("R","Rising Talisman",1),("F","Foxfire",2),("B","Rope Toss",1)]):
        yy=y+184+n*52
        s.r(x+14,yy,W-28,44,PAPER2,PERS if n==1 else INK,4 if n==1 else 2,4)
        s.r(x+22,yy+6,32,32,PERS,None,0,5); s.t(x+38,yy+30,l,20,PAPER,"bold","middle")
        s.t(x+64,yy+28,f"{nm}  Lv {lv}",16,INK,"bold")
        if n==1: s.t(x+W-24,yy+28,"replace (no refund)",13,RED,"bold","end")
    s.t(x+14,y+H-10,"Esc cancels, nothing changes. Foxfire row warns: Tanuki target.",12,MUTE,i=True)
    # 4 modifier target
    x,y=xs[3],ys[0]; panel(s,x,y,W,H,"Modifier target step",4)
    mini_speaker(s,x+14,y+46,["A little polish on your move.","Lovely."],"event: bought_modifier (the target step itself has no line yet)",290,66)
    s.t(x+14,y+168,"Slippery Skin: pick the special that carries it",16,INK,"bold")
    for n,(l,nm,lv) in enumerate([("S","Spirit Wave",1),("R","Rising Talisman",1),("F","Foxfire",2)]):
        yy=y+186+n*66
        s.r(x+14,yy,W-28,56,PAPER2,PERS if n==0 else INK,4 if n==0 else 2,4)
        s.r(x+24,yy+8,40,40,PERS,None,0,5); s.t(x+44,yy+37,l,24,PAPER,"bold","middle")
        s.t(x+76,yy+34,f"{nm}  Lv {lv}",18,INK,"bold")
        s.t(x+W-24,yy+34,"attach rule A2",12,MUTE,a="end",i=True)
    s.t(x+14,y+H-10,"Same step as the reward screen's modifier card. Esc backs out.",12,MUTE,i=True)
    # 5 release confirmation
    x,y=xs[0],ys[1]; panel(s,x,y,W,H,"Release confirmation",5)
    mini_speaker(s,x+14,y+46,["Letting one go? Kind of you,","forty please."],"event: release_binding (confirm step has no line yet)",290,66)
    slot_row(s,x+14,y+160,W-28,2,"R","Rising Talisman",1,focused=True)
    s.r(x+60,y+270,W-120,100,PAPER2,INK,3,6)
    s.t(x+W/2,y+300,"Release Rising Talisman for 40?",17,INK,"bold","middle")
    s.t(x+W/2,y+322,"It loses its levels; the slot frees up.",14,"#4A463C",a="middle")
    s.r(x+90,y+334,110,30,RED,INK,2,6); s.t(x+145,y+355,"Release",16,PAPER,"bold","middle")
    s.r(x+W-200,y+334,110,30,PAPER,INK,3,6); s.t(x+W-145,y+355,"Keep",16,INK,"bold","middle")
    s.t(x+14,y+H-10,"Default focus on Keep. After: 'It is gone. Do not look back.'",12,MUTE,i=True)
    # 6 T5 lock
    x,y=xs[1],ys[1]; panel(s,x,y,W,H,"T5 lock (last special)",6)
    mini_speaker(s,x+14,y+46,["Not that one!","It's all you have left."],"event: release_last_special",290,66)
    slot_row(s,x+14,y+170,W-28,1,"S","Spirit Wave",1,focused=True)
    s.p(f"M{x+14},{y+240} Q{x+W/2},{y+256} {x+W-14},{y+242}","none",RED,5)
    seal(s,x+W/2,y+246,20,"鎖")
    s.t(x+W/2,y+292,"LOCKED · last special",20,RED,"bold","middle")
    s.t(x+W/2,y+316,"Enter does nothing; no Release chip on this slot (T5).",14,"#4A463C",a="middle")
    s.t(x+14,y+H-10,"A tied cord and a seal, not a grey button.",12,MUTE,i=True)
    # 7 shelf empty
    x,y=xs[2],ys[1]; panel(s,x,y,W,H,"Shelf empty",7)
    mini_speaker(s,x+14,y+46,["All gone! You've cleaned me out."],"event: shelf_empty",290,66)
    s.r(x+14,y+160,W-28,226,"#CDBB94",INK,2.5,2)
    for k in range(2): s.r(x+10,y+160+(k+1)*75,W-20,12,WOODL,INK,2.5,1)
    s.r(x+10,y+160+225-12,W-20,14,WOODL,INK,2.5,1)
    s.t(x+W/2,y+215,"bare shelf",18,MUTE,a="middle",i=True)
    s.t(x+14,y+H-10,"All yokai bound or everything bought. Footer keeps only Back.",12,MUTE,i=True)
    # 8 overflow
    x,y=xs[3],ys[1]; panel(s,x,y,W,H,"Overflow (more than 6, about 10)",8)
    s.r(x+14,y+46,W-70,330,"#CDBB94",INK,2.5,2)
    for k in range(3):
        by=y+46+(k+1)*110-14
        s.r(x+10,by,W-62,12,WOODL,INK,2.5,1)
        for j in range(2):
            cx=x+14+(W-70)*(0.27 if j==0 else 0.75)
            s.r(cx-26,by-62,52,62,PAPER2,INK,2,2); s.r(cx-14,by-52,28,28,PERS if k<2 else IND,None,0,4)
    s.add(f'<rect x="{x+15}" y="{y+46+330-60}" width="{W-72}" height="58" fill="{PAPER}" opacity="0.6"/>')
    s.p(f"M{x+W/2-95},{y+372} l15,-12 l15,12","none",INK,3)
    s.t(x+W/2-60,y+368,"more below",14,INK,"bold")
    s.r(x+W-34,y+46,14,330,"#D8CDB0",INK,2,6); s.r(x+W-34,y+46,14,150,WOOD,INK,2,6)
    s.t(x+W-27,y+394,"1-3 / 5",12,INK,"bold","middle")
    s.lines(x+14,y+H-30,["Proposal: 3 shelves visible; focus past the edge scrolls one shelf;","rod shows position; PgUp / PgDn or LB / RB jump a page."],12,MUTE,lh=15,i=True)
    # 9 frame data slip open (special)
    x,y=xs[0],ys[2]; panel(s,x,y,W,H,"Frame data slip open (special)",9)
    mini_speaker(s,x+14,y+46,["Command grab. Hard to block,","slow to start."],"bubble: lead-in would make 3 sentences, so card text alone",290,66)
    slip(s,x+14,y+158,W-28,230,"River Grab","Kappa special · throw · common",
         [("FRAME DATA (placeholder numbers)",["Startup 20 / Active 3 / Recovery 40","On block -12","Damage 120 / Throw"])],
         ["same format as the reward screen and move list","Tab / Y toggles; stays on while focus moves"])
    s.t(x+14,y+H-10,"The merchant never speaks these numbers (A12).",12,MUTE,i=True)
    # 10 reference
    x,y=xs[1],ys[2]; panel(s,x,y,W,H,"Slip format (matches existing screens)",10)
    s.lines(x+14,y+62,["Special (reward_screen CardView.frames):","  Startup 15 / Active 4 / Recovery 30","  Damage 70 / Slow projectile","","Modifier (card.frames, the change only):","  Throw-invulnerable during startup","  Pulls opponent one step closer on hit","  Heals 2% of max health on hit","","Open: data/moves has no Kappa specials yet, so the","special numbers above are placeholders; a modifier","shows 'Startup +N' only if its data carries one","(none of the three does today)."],15,INK,lh=22)
    return s.svg(1920,1480)

if __name__=="__main__":
    import os
    d=os.path.dirname(os.path.abspath(__file__))
    open(os.path.join(d,"merchant-shop.svg"),"w",encoding="utf8").write(main())
    open(os.path.join(d,"merchant-shop-states.svg"),"w",encoding="utf8").write(states())
