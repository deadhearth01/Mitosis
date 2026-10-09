import AppKit
// Cuts Mito out of the approved icon: flood-fills the light tile from its edges, keeping soft alpha on the outline,
// then places him at ~78% on a clean 1024 px light background (the Icon Composer layer).
let src = NSBitmapImageRep(data: try! Data(contentsOf: URL(fileURLWithPath: CommandLine.arguments[1])))!
let W = src.pixelsWide, H = src.pixelsHigh
var r = [Float](repeating: 0, count: W * H), g = r, b = r
for y in 0..<H { for x in 0..<W { let c = src.colorAt(x: x, y: y)!.usingColorSpace(.sRGB)!; let i = y * W + x
    r[i] = Float(c.redComponent); g[i] = Float(c.greenComponent); b[i] = Float(c.blueComponent) } }
func blueness(_ i: Int) -> Float { b[i] - r[i] }
// Flood fill the background from a ring just inside the tile.
var bg = [Bool](repeating: false, count: W * H)
var stack: [Int] = []
for x in 110..<914 { stack.append(115 * W + x); stack.append(912 * W + x) }
for y in 115..<913 { stack.append(y * W + 110); stack.append(y * W + 913) }
while let i = stack.popLast() {
    if bg[i] || blueness(i) > 0.16 { continue }
    bg[i] = true
    let x = i % W, y = i / W
    if x > 0 { stack.append(i - 1) }; if x < W - 1 { stack.append(i + 1) }
    if y > 0 { stack.append(i - W) }; if y < H - 1 { stack.append(i + W) }
}
// Everything outside the tile ring is background too.
for y in 0..<H { for x in 0..<W where x < 110 || x > 913 || y < 115 || y > 912 { bg[y * W + x] = true } }
// Soft edges only right next to the creature (drops stray tinted specks from the tile's rim).
var near = bg.map { !$0 }
for _ in 0..<3 {
    var grown = near
    for y in 1..<(H - 1) { for x in 1..<(W - 1) where !near[y * W + x] {
        let i = y * W + x
        if near[i - 1] || near[i + 1] || near[i - W] || near[i + W] { grown[i] = true }
    } }
    near = grown
}
// Bounding box of the creature.
var minX = W, minY = H, maxX = 0, maxY = 0
for y in 0..<H { for x in 0..<W where !bg[y * W + x] { minX = min(minX, x); maxX = max(maxX, x); minY = min(minY, y); maxY = max(maxY, y) } }
let cw = maxX - minX + 1, ch = maxY - minY + 1
var bytes = [UInt8](repeating: 0, count: cw * ch * 4)
for y in 0..<ch { for x in 0..<cw {
    let i = (y + minY) * W + (x + minX)
    var a: Float = 1
    if bg[i] { a = near[i] ? max(0, min(1, (blueness(i) - 0.03) / 0.13)) : 0 }
    // Un-mix the white background from soft edge pixels so the outline doesn't glow.
    func un(_ v: Float) -> Float { (bg[i] && a > 0.01) ? max(0, min(1, (v - (1 - a)) / a)) : v }
    let o = (y * cw + x) * 4
    bytes[o] = UInt8(un(r[i]) * a * 255); bytes[o + 1] = UInt8(un(g[i]) * a * 255); bytes[o + 2] = UInt8(un(b[i]) * a * 255); bytes[o + 3] = UInt8(a * 255)
} }
let cutImage = bytes.withUnsafeMutableBytes { buf -> CGImage in
    CGContext(data: buf.baseAddress, width: cw, height: ch, bitsPerComponent: 8, bytesPerRow: cw * 4, space: CGColorSpace(name: CGColorSpace.sRGB)!,
              bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!.makeImage()!
}
// Compose the layer.
let size = 1024, s = CGFloat(size)
let space = CGColorSpace(name: CGColorSpace.sRGB)!
let ctx = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: 0, space: space, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
let gradient = CGGradient(colorsSpace: space, colors: [CGColor(srgbRed: 0.985, green: 0.988, blue: 0.996, alpha: 1),
                                                         CGColor(srgbRed: 0.918, green: 0.937, blue: 0.965, alpha: 1)] as CFArray, locations: [0, 1])!
ctx.drawLinearGradient(gradient, start: CGPoint(x: 0, y: s), end: CGPoint(x: 0, y: 0), options: [])
let scale = (s * 0.80) / CGFloat(max(cw, ch))
let dw = CGFloat(cw) * scale, dh = CGFloat(ch) * scale
let rect = CGRect(x: (s - dw) / 2, y: (s - dh) / 2 - s * 0.01, width: dw, height: dh)
// Soft contact shadow under the feet.
ctx.saveGState()
ctx.setShadow(offset: CGSize(width: 0, height: -s * 0.018), blur: s * 0.05, color: CGColor(srgbRed: 0.05, green: 0.2, blue: 0.45, alpha: 0.28))
ctx.interpolationQuality = .high
ctx.draw(cutImage, in: rect)
ctx.restoreGState()
try! NSBitmapImageRep(cgImage: ctx.makeImage()!).representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: CommandLine.arguments[2]))
print("creature bbox \(minX),\(minY) \(cw)x\(ch)")
