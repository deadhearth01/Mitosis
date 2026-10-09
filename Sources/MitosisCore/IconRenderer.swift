import AppKit
import CoreText
import ImageIO
import UniformTypeIdentifiers

public enum IconError: Error, Equatable, CustomStringConvertible, Sendable {
    case noBaseIcon(String)
    case renderFailed(Int)
    case encodeFailed

    public var description: String {
        switch self {
        case .noBaseIcon(let app): return "Couldn't read the icon of \(app)."
        case .renderFailed(let size): return "Couldn't draw the \(size)px icon."
        case .encodeFailed: return "Couldn't save the clone icon."
        }
    }
}

public enum IconRenderer {
    public static let iconFileBaseName = "MitosisIcon"
    static let icnsSizes = [16, 32, 128, 256, 512, 1024]

    public static func baseIcon(forApp url: URL) -> CGImage? {
        let image = NSWorkspace.shared.icon(forFile: url.path)
        image.size = NSSize(width: 1024, height: 1024)
        var rect = CGRect(x: 0, y: 0, width: 1024, height: 1024)
        return image.cgImage(forProposedRect: &rect, context: nil, hints: nil)
    }

    public static func render(base: CGImage, badge: Badge, size: Int) -> CGImage? {
        guard let space = CGColorSpace(name: CGColorSpace.sRGB),
              let ctx = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: 0,
                                  space: space, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
        else { return nil }
        let s = CGFloat(size)
        ctx.interpolationQuality = .high
        ctx.draw(base, in: CGRect(x: 0, y: 0, width: s, height: s))

        // Badge circle in the bottom-right corner (Core Graphics origin is bottom-left), centered on the corner's curve
        // so it stays inside the standard rounded-square outline. macOS 26+ shrinks icons whose outline sticks out.
        let d = s * 0.31
        let circle = CGRect(x: s - d - s * 0.125, y: s * 0.125, width: d, height: d)
        ctx.setFillColor(CGColor(gray: 1, alpha: 1))
        ctx.fillEllipse(in: circle.insetBy(dx: -s * 0.015, dy: -s * 0.015))
        ctx.setFillColor(color(fromHex: badge.color))
        ctx.fillEllipse(in: circle)

        let text = String(badge.text.prefix(2)).uppercased()
        if !text.isEmpty, size >= 32,
           let font = CTFontCreateUIFontForLanguage(.emphasizedSystem, d * (text.count == 1 ? 0.6 : 0.45), nil) {
            let attributed = NSAttributedString(string: text, attributes: [
                NSAttributedString.Key(kCTFontAttributeName as String): font,
                NSAttributedString.Key(kCTForegroundColorAttributeName as String): CGColor(gray: 1, alpha: 1),
            ])
            let line = CTLineCreateWithAttributedString(attributed)
            let b = CTLineGetBoundsWithOptions(line, .useGlyphPathBounds)
            ctx.textPosition = CGPoint(x: circle.midX - b.width / 2 - b.minX, y: circle.midY - b.height / 2 - b.minY)
            CTLineDraw(line, ctx)
        }
        return ctx.makeImage()
    }

    public static func icnsData(base: CGImage, badge: Badge) throws -> Data {
        let data = NSMutableData()
        guard let dest = CGImageDestinationCreateWithData(data, UTType.icns.identifier as CFString, icnsSizes.count, nil) else {
            throw IconError.encodeFailed
        }
        for size in icnsSizes {
            guard let image = render(base: base, badge: badge, size: size) else { throw IconError.renderFailed(size) }
            // The 1024 px image is stored as the 512 pt @2x representation; without 144 DPI ImageIO drops it.
            let props: [CFString: Any] = size == 1024 ? [kCGImagePropertyDPIWidth: 144, kCGImagePropertyDPIHeight: 144] : [:]
            CGImageDestinationAddImage(dest, image, props as CFDictionary)
        }
        guard CGImageDestinationFinalize(dest) else { throw IconError.encodeFailed }
        return data as Data
    }

    public static func color(fromHex hex: String) -> CGColor {
        let digits = hex.hasPrefix("#") ? String(hex.dropFirst()) : hex
        guard digits.count == 6, let v = UInt32(digits, radix: 16) else {
            return color(fromHex: Badge.defaultColor)
        }
        return CGColor(srgbRed: CGFloat((v >> 16) & 0xFF) / 255, green: CGFloat((v >> 8) & 0xFF) / 255,
                       blue: CGFloat(v & 0xFF) / 255, alpha: 1)
    }
}
