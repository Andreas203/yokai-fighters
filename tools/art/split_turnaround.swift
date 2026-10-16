import Foundation
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers
let a = CommandLine.arguments; let p = a[1]; let prefix = a[2]; let cuts: [(Character, Int, Int, Int)] = a.count > 3 ? a[3].split(separator: ",").map { let f = $0.split(separator: ":"); return (f[0].first!, Int(f[1])!, Int(f[2])!, Int(f[3])!) } : []; let names = ["front","side","back"]
let im = CGImageSourceCreateImageAtIndex(CGImageSourceCreateWithURL(URL(fileURLWithPath: p) as CFURL, nil)!, 0, nil)!
let w = im.width, h = im.height
var buf = [UInt8](repeating: 0, count: w*h*4)
let ctx = CGContext(data: &buf, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w*4, space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
ctx.draw(im, in: CGRect(x: 0, y: 0, width: w, height: h))
func ink(_ i: Int) -> Bool { Int(buf[i*4]) + Int(buf[i*4+1]) + Int(buf[i*4+2]) < 720 }
var label = [Int32](repeating: -1, count: w*h)
var comps: [(n: Int, minx: Int, maxx: Int, miny: Int, maxy: Int, sx: Int)] = []
var stack: [Int] = []
for start in 0..<(w*h) where label[start] < 0 && ink(start) {
  let id = Int32(comps.count); var c = (n: 0, minx: w, maxx: 0, miny: h, maxy: 0, sx: 0)
  label[start] = id; stack.append(start)
  while let q = stack.popLast() {
    let x = q % w, y = q / w; c.n += 1; c.sx += x
    c.minx = min(c.minx, x); c.maxx = max(c.maxx, x); c.miny = min(c.miny, y); c.maxy = max(c.maxy, y)
    for (dx, dy) in [(1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,-1),(1,-1),(-1,1)] {
      let nx = x+dx, ny = y+dy; if nx < 0 || ny < 0 || nx >= w || ny >= h { continue }
      if cuts.contains(where: { c in c.0 == "v" ? ((x < c.1) != (nx < c.1) && y >= c.2 && y <= c.3) : ((y < c.1) != (ny < c.1) && x >= c.2 && x <= c.3) }) { continue }
      let j = ny*w+nx; if label[j] < 0 && ink(j) { label[j] = id; stack.append(j) }
    }
  }
  comps.append(c)
}
let majors = comps.indices.sorted { comps[$0].n > comps[$1].n }.prefix(3).sorted { comps[$0].sx/comps[$0].n < comps[$1].sx/comps[$1].n }
print(p, "components:", comps.count, "majors:", majors.map { (comps[$0].n, comps[$0].minx, comps[$0].maxx) })
// assign every component to a major: one whose bbox contains its centre, else nearest centre
var owner = [Int](repeating: -1, count: comps.count)
for (ci, c) in comps.enumerated() {
  let cx = c.sx / c.n, cy = (c.miny + c.maxy) / 2
  var best = -1; var bd = Int.max
  for (k, m) in majors.enumerated() { let d = abs(cx - comps[m].sx/comps[m].n); if d < bd { bd = d; best = k } }
  owner[ci] = best
}
for (k, _) in majors.enumerated() {
  var minx = w, maxx = 0, miny = h, maxy = 0
  for (ci, c) in comps.enumerated() where owner[ci] == k { minx = min(minx, c.minx); maxx = max(maxx, c.maxx); miny = min(miny, c.miny); maxy = max(maxy, c.maxy) }
  let bw = maxx - minx + 1, bh = maxy - miny + 1, side = max(bw, bh) + 80
  var ob = [UInt8](repeating: 255, count: side*side*4)
  let ox = (side - bw)/2, oy = (side - bh)/2
  for y in miny...maxy { for x in minx...maxx {
    let i = y*w+x; let l = label[i]
    if l >= 0 && owner[Int(l)] != k { continue }   // pixel belongs to another view: leave white
    let o = ((y-miny+oy)*side + (x-minx+ox))*4
    ob[o] = buf[i*4]; ob[o+1] = buf[i*4+1]; ob[o+2] = buf[i*4+2]; ob[o+3] = 255
  } }
  let octx = CGContext(data: &ob, width: side, height: side, bitsPerComponent: 8, bytesPerRow: side*4, space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
  let out = "\(prefix)-\(names[k]).png"
  let d = CGImageDestinationCreateWithURL(URL(fileURLWithPath: out) as CFURL, UTType.png.identifier as CFString, 1, nil)!
  CGImageDestinationAddImage(d, octx.makeImage()!, nil); CGImageDestinationFinalize(d)
  print(out, bw, bh)
}
