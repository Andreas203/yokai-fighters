# Generator for the YOK-71 font specimen sheets (ROUGH, docs only).
#   python gen_font_specimens.py            -> writes font-specimens*.svg that load fonts from Google Fonts (needs network to view)
#   python gen_font_specimens.py --local D  -> writes *.render.svg that load TTFs from folder D (used for the PNGs; never committed)
# Render PNGs with headless Chrome or Edge at 1920x1080 (sheets 1 and 2) and 1280x720 (sheet 3).
import sys, os
from xml.sax.saxutils import escape

INK="#1D1B21"; PAPER="#F1E8D4"; PAPER2="#F8F1E0"; IND="#2D3A5E"; PERS="#D8632C"; RED="#B5332B"; MUTE="#7A7054"; GREY="#8C8778"

# family key -> (css family, weight, google css2 name, local file)
FONTS={
 "syuku":("Yuji Syuku",400,"Yuji+Syuku","YujiSyuku-Regular.ttf"),
 "boku":("Yuji Boku",400,"Yuji+Boku","YujiBoku-Regular.ttf"),
 "decol":("Kaisei Decol",700,"Kaisei+Decol:wght@700","KaiseiDecol-Bold.ttf"),
 "shippori":("Shippori Mincho",400,"Shippori+Mincho:wght@400;700","ShipporiMincho-Regular.ttf"),
 "zkg":("Zen Kaku Gothic New",400,"Zen+Kaku+Gothic+New:wght@400;700","ZenKakuGothicNew-Regular.ttf"),
 "noto":("Noto Serif JP",400,"Noto+Serif+JP:wght@400;700","NotoSerifJP-VF.ttf"),
 "zom":("Zen Old Mincho",700,"Zen+Old+Mincho:wght@400;700","ZenOldMincho-Bold.ttf"),
 "sawa":("Sawarabi Mincho",400,"Sawarabi+Mincho","SawarabiMincho-Regular.ttf"),
}
SYS_HEAD="Georgia, 'Yu Mincho', 'Times New Roman', serif"
SYS_BODY="Georgia, 'Yu Mincho', 'Times New Roman', serif"

def fam(k):
    if k=="sys": return SYS_HEAD
    return f"'{FONTS[k][0]}', serif" if k not in("zkg",) else f"'{FONTS[k][0]}', sans-serif"

PAIRS=[
 dict(n="1",title="Brush mincho + calm mincho",head="syuku",body="shippori",hn="Yuji Syuku",bn="Shippori Mincho",
      lic="OFL 1.1, free, embed and ship in the game",
      cov="Latin: ASCII 95/95, Latin-1 96/96, dashes, quotes, dot, times; kana + ~6.7k kanji (Yuji), ~13.6k (Shippori)",
      size="TTF 8.4 MB + 8.7 MB full; Latin-only subset 237 KB + 107 KB",
      note="Soft brush-written heading, quiet body. Closest to a hand-lettered talisman at low risk."),
 dict(n="2",title="Rough brush + clean sans",head="boku",body="zkg",hn="Yuji Boku",bn="Zen Kaku Gothic New",
      lic="OFL 1.1, free, embed and ship in the game",
      cov="Latin: ASCII 95/95, Latin-1 96/96, dashes, quotes, dot, times, arrows; kana + ~6.7k kanji",
      size="TTF 8.5 MB + 2.4 MB full; Latin-only subset 274 KB + 28 KB",
      note="Boldest brush feel; sans body is the sharpest at small sizes and during hitstop."),
 dict(n="3",title="Decorative serif + wide-coverage serif",head="decol",body="noto",hn="Kaisei Decol Bold",bn="Noto Serif JP",
      lic="OFL 1.1, free, embed and ship in the game",
      cov="Latin: ASCII 95/95, Latin-1 96/96, dashes, quotes, dot, times, arrows; kana + 6.7k / 12.7k kanji",
      size="TTF 4.3 MB + 13.6 MB (variable) full; Latin-only subset 185 KB + 192 KB",
      note="Stately, print-like. Safest for long text and any kanji the story cards might need; least brush-like."),
 dict(n="4",title="Licensed brush (NOT rendered: stand-in shown)",head="boku",body="zom",hn="commercial Japanese brush face",bn="Zen Old Mincho (free body)",
      lic="Commercial. e.g. Fontworks or Morisawa brush families; game/app embedding licence, price on request, NOT BOUGHT, not verified",
      cov="Latin coverage varies by family and must be checked before purchase; many Japanese brush faces have weak or proportional-only Latin",
      size="unknown until a family is chosen (commercial JP fonts are often 5-20 MB)",
      note="Stand-in: heading drawn in Yuji Boku, body in Zen Old Mincho. Only worth buying if free brush faces fall short. The designer owns the purchase.",
      standin=True),
 dict(n="5",title="System fallback",head="sys",body="sys",hn="Georgia (then Yu Mincho / Times)",bn="Georgia (same)",
      lic="OS-bundled, no licence to ship (Godot SystemFont); not guaranteed on every PC",
      cov="Latin: full. Japanese falls to the OS face (Yu Mincho on Windows, Hiragino on macOS), so kana and kanji change look per machine",
      size="0 MB bundled",
      note="What the rough mockups use. Ships nothing, but looks different per machine and has no brush character; keep as the last fallback only.",),
]
ALSO=[("zom","sawa","Zen Old Mincho Bold + Sawarabi Mincho","OFL 1.1. Latin ASCII 95/95. Sawarabi lacks en/em dash and arrows (use hyphen). Full TTF 5.4 MB + 3.7 MB; Latin subset 39 KB + 35 KB.")]

class S:
    def __init__(s,w,h,local=None):
        s.w=w; s.h=h; s.o=[]; s.local=local
    def add(s,x): s.o.append(x)
    def r(s,x,y,w,h,fill="none",stroke=INK,sw=2,rx=0,dash=None):
        d=f' stroke-dasharray="{dash}"' if dash else ""
        s.add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{fill}" stroke="{stroke if stroke else "none"}" stroke-width="{sw}"{d}/>')
    def t(s,x,y,txt,size,font,w=400,fill=INK,a="start",i=False):
        st=' font-style="italic"' if i else ""
        s.add(f'<text x="{x}" y="{y}" font-family="{font}" font-size="{size}" font-weight="{w}" fill="{fill}" text-anchor="{a}"{st}>{escape(txt)}</text>')
    def svg(s,keys):
        css=""
        if s.local:
            for k in keys:
                if k=="sys": continue
                f,w,g,file=FONTS[k]
                wr="200 900" if k=="noto" else str(w)
                css+=f"@font-face{{font-family:'{f}';src:url('file:///{s.local}/{file}');font-weight:{wr};}}\n"
                if k=="shippori": css+=f"@font-face{{font-family:'{f}';src:url('file:///{s.local}/ShipporiMincho-Bold.ttf');font-weight:700;}}\n"
                if k=="zkg": css+=f"@font-face{{font-family:'{f}';src:url('file:///{s.local}/ZenKakuGothicNew-Bold.ttf');font-weight:700;}}\n"
        else:
            fams="&amp;".join(f"family={FONTS[k][2]}" for k in keys if k!="sys")
            css=f"@import url('https://fonts.googleapis.com/css2?{fams}&amp;display=swap');\n"
        return f'<svg xmlns="http://www.w3.org/2000/svg" width="{s.w}" height="{s.h}" viewBox="0 0 {s.w} {s.h}"><style>{css}</style><rect width="{s.w}" height="{s.h}" fill="{IND}"/>'+"".join(s.o)+'</svg>'

def strip(s,label):
    s.r(560,18,800,32,RED,None,0); s.t(960,40,label,16,"Georgia, serif",700,PAPER,"middle")

def wrap(txt,n):
    out=[];cur=""
    for w in txt.split():
        if len(cur)+len(w)+1>n and cur: out.append(cur); cur=w
        else: cur=(cur+" "+w).strip()
    if cur: out.append(cur)
    return out

CARD_NAME="Foxfire"; CARD_PLAIN="Throw a slow fireball that lights the way and sets the copied move's level."
BUBBLE="Ah, Slippery Skin: Slick kappa skin makes you impossible to throw while the move starts up."
UIF="Georgia, serif"   # annotation font, not part of the specimen

def column(s,x,y,w,p,scale=1.0):
    H=fam(p["head"]); B=fam(p["body"]); hw=700 if p["head"] in ("sys","decol","zom") else 400
    bw=400
    s.r(x,y,w,1000-y+0,PAPER,INK,2.5,2)
    s.r(x,y,w,64,IND,None,0)
    s.t(x+12,y+26,f'{p["n"]}  {p["title"]}',18,UIF,700,PAPER)
    s.t(x+12,y+50,f'heading: {p["hn"]}   body: {p["bn"]}',14,UIF,400,"#C9CFE6",i=True)
    yy=y+64
    if p.get("standin"):
        s.r(x+8,yy+6,w-16,22,"#E3D8BD",MUTE,1.5,2,"6 5"); s.t(x+w/2,yy+22,"STAND-IN PREVIEW: licensed font not owned, not bought",13,UIF,700,RED,"middle"); yy+=16
    s.t(x+16,yy+96,"SHOP",92,H,hw); s.r(x+16,yy+108,224,10,RED,None,0,3)
    s.t(x+16,yy+170,"REST STOP",52,H,hw)
    s.t(x+16,yy+212,"Foxfire Lv 2 · Tanuki target",26,H,hw,PERS)
    s.t(x+16,yy+238,"Foxfire Lv 2 · Tanuki target",16,B,bw,"#4A463C")
    # card
    cy=yy+256
    s.r(x+16,cy,w-32,138,PAPER2,INK,2.5,3); s.r(x+16,cy,w-32,22,RED,None,0)
    s.t(x+28,cy+16,"SPECIAL · NEW · Lv 1",12,B,700,PAPER)
    s.r(x+28,cy+32,52,52,PERS,None,0,6); s.t(x+54,cy+68,"F",34,H,hw,PAPER,"middle")
    s.t(x+94,cy+62,CARD_NAME,30,H,hw)
    s.t(x+94,cy+82,"Kitsune · special",13,B,bw,MUTE)
    for n,l in enumerate(wrap(CARD_PLAIN,50)): s.t(x+28,cy+106+n*20,l,16,B,bw,"#33302A")
    # bubble
    by=cy+154
    lines=wrap(BUBBLE,44)
    s.r(x+16,by,w-32,18+len(lines)*26,PAPER2,INK,3,14)
    for n,l in enumerate(lines): s.t(x+30,by+30+n*26,l,20,B,bw)
    ky=by+18+len(lines)*26+14
    # chips
    cx=x+16
    for key,lab in [("Tab / Y","Frame data"),("Enter / A","Choose")]:
        kw=len(key)*11+22
        s.r(cx,ky,kw,34,PAPER2,INK,2,6); s.t(cx+kw/2,ky+24,key,18,B,700,INK,"middle")
        s.t(cx+kw+8,ky+24,lab,20,B,bw); cx+=kw+len(lab)*12+34
    # notes
    ny=ky+58
    s.t(x+16,ny,"Licence",12,UIF,700,RED)
    for n,l in enumerate(wrap(p["lic"],int(w/6.6))): s.t(x+16,ny+16+n*15,l,12.5,UIF,400,INK)
    ny+=16+15*len(wrap(p["lic"],int(w/6.6)))+8
    s.t(x+16,ny,"Latin glyph coverage",12,UIF,700,RED)
    for n,l in enumerate(wrap(p["cov"],int(w/6.6))): s.t(x+16,ny+16+n*15,l,12.5,UIF,400,INK)
    ny+=16+15*len(wrap(p["cov"],int(w/6.6)))+8
    s.t(x+16,ny,"File size",12,UIF,700,RED)
    for n,l in enumerate(wrap(p["size"],int(w/6.6))): s.t(x+16,ny+16+n*15,l,12.5,UIF,400,INK)
    ny+=16+15*len(wrap(p["size"],int(w/6.6)))+8
    s.t(x+16,ny,"Read",12,UIF,700,RED)
    for n,l in enumerate(wrap(p["note"],int(w/6.6))): s.t(x+16,ny+16+n*15,l,12.5,UIF,400,INK,i=True)

def sheet1(local):
    s=S(1920,1080,local); strip(s,"ROUGH PROTOTYPE · FONT SPECIMENS 1 of 3 · YOK-71")
    s.t(960,86,"Pairings 1 to 3: free Google Fonts (OFL). Sizes are the 1080p HUD and card sizes.",20,UIF,700,PAPER,"middle")
    for i,p in enumerate(PAIRS[:3]): column(s,40+i*622,100,600,p)
    return s.svg(["syuku","shippori","boku","zkg","decol","noto"])

def sheet2(local):
    s=S(1920,1080,local); strip(s,"ROUGH PROTOTYPE · FONT SPECIMENS 2 of 3 · YOK-71")
    s.t(960,86,"Pairings 4 and 5: licensed brush option (stand-in) and system fallback; plus one more free pair.",20,UIF,700,PAPER,"middle")
    for i,p in enumerate(PAIRS[3:]): column(s,40+i*622,100,600,p)
    x=40+2*622; y=100; w=600
    s.r(x,y,w,900,PAPER,INK,2.5,2); s.r(x,y,w,64,IND,None,0)
    s.t(x+12,y+26,"Also tried: Zen Old Mincho + Sawarabi Mincho",18,UIF,700,PAPER)
    s.t(x+12,y+50,"quiet all-mincho pair, little brush character",14,UIF,400,"#C9CFE6",i=True)
    H=fam("zom"); B=fam("sawa")
    s.t(x+16,y+160,"SHOP",92,H,700); s.t(x+16,y+230,"REST STOP",52,H,700)
    s.t(x+16,y+272,"Foxfire Lv 2 · Tanuki target",26,H,700,PERS)
    s.t(x+16,y+300,"Foxfire Lv 2 · Tanuki target",16,B,400,"#4A463C")
    s.t(x+16,y+346,CARD_NAME,30,H,700)
    for n,l in enumerate(wrap(CARD_PLAIN,50)): s.t(x+16,y+372+n*20,l,16,B,400)
    for n,l in enumerate(wrap(BUBBLE,44)): s.t(x+16,y+430+n*26,l,20,B,400)
    s.t(x+16,y+540,"Licence",12,UIF,700,RED)
    for n,l in enumerate(wrap(ALSO[0][3],int(w/6.6))): s.t(x+16,y+556+n*15,l,12.5,UIF,400,INK)
    s.t(x+16,y+640,"How the samples are drawn",12,UIF,700,RED)
    for n,l in enumerate(wrap("Specimen text is drawn upright. These families ship no italic, so the game would show merchant lines upright (a browser would fake an oblique, which is why the mockups look slanted). Japanese glyphs are not shown here; all listed families cover kana and at least 6.7k kanji except Sawarabi (3.3k).",int(w/6.6))): s.t(x+16,y+656+n*15,l,12.5,UIF,400,INK)
    return s.svg(["boku","zom","sawa"])

def sheet3(local):
    s=S(1280,720,local)
    s.r(373,6,534,22,RED,None,0); s.t(640,22,"ROUGH PROTOTYPE · 1280x720 LEGIBILITY · YOK-71",13,UIF,700,PAPER,"middle")
    k=2/3
    rowh=136
    for i,p in enumerate(PAIRS):
        y=34+i*rowh
        H=fam(p["head"]); B=fam(p["body"]); hw=700 if p["head"] in ("sys","decol","zom") else 400
        s.r(6,y,1268,rowh-6,PAPER,INK,2,2)
        s.t(14,y+16,f'{p["n"]} {p["hn"]} + {p["bn"]}'+("  [stand-in]" if p.get("standin") else ""),11,UIF,700,IND)
        s.t(14,y+86,"SHOP",92*k,H,hw); s.t(14,y+122,"REST STOP",52*k,H,hw)
        s.t(268,y+44,"Foxfire Lv 2 · Tanuki target",26*k,H,hw,PERS)
        s.t(268,y+62,"Foxfire Lv 2 · Tanuki target",16*k,B,400,"#4A463C")
        s.r(268,y+72,330,54,PAPER2,INK,1.5,2); s.r(268,y+72,330,10,RED,None,0)
        s.t(276,y+104,CARD_NAME,30*k,H,hw)
        for n,l in enumerate(wrap(CARD_PLAIN,42)): s.t(366,y+100+n*13,l,16*k,B,400,"#33302A")
        bl=wrap(BUBBLE,50)
        s.r(618,y+22,370,12+len(bl)*18,PAPER2,INK,2,9)
        for n,l in enumerate(bl): s.t(628,y+40+n*18,l,20*k,B,400)
        cx=618
        for key,lab in [("Tab / Y","Frame data"),("Enter / A","Choose")]:
            kw=len(key)*8+14
            s.r(cx,y+100,kw,23,PAPER2,INK,1.5,4); s.t(cx+kw/2,y+116,key,18*k,B,700,INK,"middle")
            s.t(cx+kw+6,y+116,lab,20*k,B,400); cx+=kw+len(lab)*8+22
        # smallest sizes in use: slot sub-line 14 px and frame-data slip 12 px at 1080p
        s.t(1010,y+40,"smallest 1080p sizes at 720p:",11,UIF,700,RED)
        s.t(1010,y+58,"Lv 2 · Tanuki target (14)",14*k,B,400); s.t(1010,y+76,"Startup 15 / Active 4 / Recovery 30 (15)",15*k,B,400)
        s.t(1010,y+94,"On block -12 · Damage 70 (13)",13*k,B,400)
    return s.svg(["syuku","shippori","boku","zkg","decol","noto","zom"])

if __name__=="__main__":
    d=os.path.dirname(os.path.abspath(__file__))
    local=None
    if "--local" in sys.argv: local=sys.argv[sys.argv.index("--local")+1].replace("\\","/")
    suf=".render.svg" if local else ".svg"
    for name,fn in (("font-specimens",sheet1),("font-specimens-2",sheet2),("font-specimens-720p",sheet3)):
        out=os.path.join(local and os.environ.get("SPEC_OUT",d) or d,name+suf)
        open(out,"w",encoding="utf8").write(fn(local))
