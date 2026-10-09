import AppKit
import MitosisCore
import SwiftUI

/// Live previews of a clone icon: the original app's icon with the badge drawn by the same renderer clones use.
@MainActor
enum IconPreview {
    private static var bases: [String: CGImage] = [:]

    static func image(source: URL, badge: Badge, size: Int = 256) -> NSImage? {
        let base: CGImage
        if let hit = bases[source.path] {
            base = hit
        } else {
            guard let made = IconRenderer.baseIcon(forApp: source) else { return nil }
            bases[source.path] = made
            base = made
        }
        guard let cg = IconRenderer.render(base: base, badge: badge, size: size) else { return nil }
        return NSImage(cgImage: cg, size: NSSize(width: size / 2, height: size / 2))
    }
}

struct ColorSwatches: View {
    @Binding var selection: String

    var body: some View {
        HStack(spacing: Brand.Space.s) {
            ForEach(NewCloneForm.colors) { color in
                let selected = color.hex == selection
                Button {
                    selection = color.hex
                } label: {
                    Circle()
                        .fill(Color(cgColor: IconRenderer.color(fromHex: color.hex)))
                        .frame(width: 20, height: 20)
                        .overlay(Circle().strokeBorder(.white.opacity(0.9), lineWidth: selected ? 2 : 0).padding(1))
                        .overlay(Circle().strokeBorder(Color.primary.opacity(selected ? 0.55 : 0), lineWidth: 1).padding(-2))
                }
                .buttonStyle(.plain)
                .help(color.name)
                .accessibilityLabel(color.name)
                .accessibilityAddTraits(selected ? [.isButton, .isSelected] : .isButton)
            }
        }
    }
}

struct RestyleSheet: View {
    let model: AppModel
    let entry: RegistryEntry
    @State private var text: String
    @State private var color: String
    @Environment(\.dismiss) private var dismiss

    init(model: AppModel, entry: RegistryEntry) {
        self.model = model
        self.entry = entry
        _text = State(initialValue: entry.manifest.badge.text)
        _color = State(initialValue: entry.manifest.badge.color)
    }

    private var badge: Badge {
        Badge(text: text.isEmpty ? String(CloneLibrary.appName(for: entry).prefix(1)) : text.uppercased(), color: color)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Brand.Space.m) {
            Text("Edit Badge")
                .font(.title2.weight(.semibold))
            HStack(alignment: .center, spacing: Brand.Space.l) {
                Group {
                    if let image = IconPreview.image(source: URL(fileURLWithPath: entry.manifest.source.path), badge: badge) {
                        Image(nsImage: image).resizable().frame(width: 96, height: 96)
                    } else {
                        CloneIconView(entry: entry, size: 96)
                    }
                }
                .accessibilityHidden(true)
                Form {
                    TextField("Badge", text: Binding(get: { text }, set: { text = String($0.prefix(2)).uppercased() }),
                              prompt: Text("A"))
                        .frame(width: 56)
                        .help("One or two letters or digits")
                    LabeledContent("Color") { ColorSwatches(selection: $color) }
                }
                .formStyle(.columns)
            }
            Text("The name stays \(entry.manifest.name), so the clone keeps its logins.")
                .font(.callout)
                .foregroundStyle(.secondary)
            HStack {
                Spacer()
                Button("Cancel", role: .cancel) { dismiss() }
                    .keyboardShortcut(.cancelAction)
                Button("Save") {
                    let target = entry
                    let newBadge = badge
                    dismiss()
                    // After the sheet closes, so a "Quit it first?" alert can appear.
                    Task { @MainActor in
                        try? await Task.sleep(for: .milliseconds(300))
                        model.request(.restyle(newBadge), for: target)
                    }
                }
                .keyboardShortcut(.defaultAction)
                .disabled(badge == entry.manifest.badge)
            }
        }
        .padding(Brand.Space.l)
        .frame(width: 460)
    }
}
