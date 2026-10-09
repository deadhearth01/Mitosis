import AppKit

/// Mito as a menu bar glyph: the rounded body with its split seam, a wink, and two feet. A template image, so
/// macOS tints it for light and dark menu bars like the system's own icons.
enum MenuBarIcon {
    static let image: NSImage = {
        let image = NSImage(size: NSSize(width: 18, height: 18), flipped: false) { _ in
            guard let ctx = NSGraphicsContext.current?.cgContext else { return false }
            NSColor.black.setFill()
            // Feet.
            NSBezierPath(ovalIn: NSRect(x: 3.6, y: 1.2, width: 4.4, height: 3.2)).fill()
            NSBezierPath(ovalIn: NSRect(x: 10.0, y: 1.2, width: 4.4, height: 3.2)).fill()
            // Body.
            NSBezierPath(roundedRect: NSRect(x: 2.2, y: 3.0, width: 13.6, height: 12.6), xRadius: 4.6, yRadius: 4.6).fill()
            // Cut-outs: the seam, the open eye, the wink.
            ctx.setBlendMode(.clear)
            let seam = NSBezierPath()
            seam.move(to: NSPoint(x: 9.2, y: 15.8))
            seam.curve(to: NSPoint(x: 8.9, y: 9.4), controlPoint1: NSPoint(x: 8.5, y: 13.6), controlPoint2: NSPoint(x: 9.6, y: 11.4))
            seam.curve(to: NSPoint(x: 9.1, y: 2.8), controlPoint1: NSPoint(x: 8.3, y: 7.2), controlPoint2: NSPoint(x: 9.5, y: 5.0))
            seam.lineWidth = 1.1
            seam.stroke()
            NSBezierPath(ovalIn: NSRect(x: 4.9, y: 8.7, width: 2.4, height: 2.6)).fill()
            let wink = NSBezierPath()
            wink.move(to: NSPoint(x: 10.8, y: 9.5))
            wink.curve(to: NSPoint(x: 13.4, y: 9.5), controlPoint1: NSPoint(x: 11.5, y: 10.9), controlPoint2: NSPoint(x: 12.7, y: 10.9))
            wink.lineWidth = 1.1
            wink.lineCapStyle = .round
            wink.stroke()
            return true
        }
        image.isTemplate = true
        image.accessibilityDescription = "Mitosis"
        return image
    }()
}
