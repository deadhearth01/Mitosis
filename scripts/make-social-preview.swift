// Draws the GitHub/X social preview (1280x640) from the app icon layer and a light screenshot.
// usage: swift scripts/make-social-preview.swift <icon.png> <screenshot.png> <out.png>
import AppKit

let a = CommandLine.arguments
let icon = NSImage(contentsOfFile: a[1])!, shot = NSImage(contentsOfFile: a[2])!
let W: CGFloat = 1280, H: CGFloat = 640
let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(W), pixelsHigh: Int(H), bitsPerSample: 8, samplesPerPixel: 4,
                           hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
let ctx = NSGraphicsContext.current!.cgContext
func rgb(_ hex: UInt32, _ alpha: CGFloat = 1) -> NSColor {
    NSColor(srgbRed: CGFloat((hex >> 16) & 0xFF) / 255, green: CGFloat((hex >> 8) & 0xFF) / 255, blue: CGFloat(hex & 0xFF) / 255, alpha: alpha)
}
// Background: soft brand-tinted gradient.
NSGradient(colors: [rgb(0xE6F3FF), rgb(0xF6FAFF), rgb(0xFFFFFF)])!.draw(in: NSRect(x: 0, y: 0, width: W, height: H), angle: -35)

// Screenshot on the right, rounded, with a soft shadow; it bleeds off the right and bottom edges.
let shotRect = NSRect(x: 610, y: -60, width: 840, height: 840 * shot.size.height / shot.size.width)
ctx.saveGState()
ctx.setShadow(offset: CGSize(width: 0, height: -18), blur: 50, color: rgb(0x021944, 0.22).cgColor)
NSBezierPath(roundedRect: shotRect, xRadius: 18, yRadius: 18).fill()
ctx.restoreGState()
ctx.saveGState()
NSBezierPath(roundedRect: shotRect, xRadius: 18, yRadius: 18).addClip()
shot.draw(in: shotRect)
ctx.restoreGState()
rgb(0x021944, 0.10).setStroke()
let border = NSBezierPath(roundedRect: shotRect.insetBy(dx: 0.5, dy: 0.5), xRadius: 18, yRadius: 18); border.lineWidth = 1; border.stroke()

// Left column: icon, name, tagline, meta.
let iconRect = NSRect(x: 72, y: H - 96 - 128, width: 128, height: 128)
ctx.saveGState()
ctx.setShadow(offset: CGSize(width: 0, height: -14), blur: 34, color: rgb(0x1450A8, 0.28).cgColor)
NSBezierPath(roundedRect: iconRect, xRadius: 29, yRadius: 29).fill()
ctx.restoreGState()
ctx.saveGState()
NSBezierPath(roundedRect: iconRect, xRadius: 29, yRadius: 29).addClip()
icon.draw(in: iconRect)
ctx.restoreGState()

func draw(_ text: String, _ font: NSFont, _ color: NSColor, x: CGFloat, top: CGFloat, width: CGFloat, kern: CGFloat = 0) -> CGFloat {
    let style = NSMutableParagraphStyle(); style.lineHeightMultiple = 1.08
    let s = NSAttributedString(string: text, attributes: [.font: font, .foregroundColor: color, .paragraphStyle: style, .kern: kern])
    let bounds = s.boundingRect(with: NSSize(width: width, height: 400), options: [.usesLineFragmentOrigin, .usesFontLeading])
    s.draw(with: NSRect(x: x, y: H - top - bounds.height, width: width, height: bounds.height), options: [.usesLineFragmentOrigin, .usesFontLeading])
    return top + bounds.height
}
var y = draw("Mitosis", .systemFont(ofSize: 76, weight: .bold), rgb(0x021944), x: 68, top: 256, width: 520, kern: -1.5)
y = draw("Run separate copies of your Mac apps, side by side.", .systemFont(ofSize: 31, weight: .medium), rgb(0x1D3A66), x: 72, top: y + 10, width: 480)
let meta = NSMutableAttributedString(string: "Free", attributes: [.font: NSFont.systemFont(ofSize: 21, weight: .semibold), .foregroundColor: rgb(0x1A7FE0)])
meta.append(NSAttributedString(string: "  ·  Source-available  ·  Native macOS", attributes: [.font: NSFont.systemFont(ofSize: 21, weight: .regular), .foregroundColor: rgb(0x4B6385)]))
meta.draw(at: NSPoint(x: 72, y: H - (y + 26) - 26))

NSGraphicsContext.restoreGraphicsState()
try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: a[3]))
print("wrote \(a[3])")
