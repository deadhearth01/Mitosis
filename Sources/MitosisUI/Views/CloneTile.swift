import MitosisCore
import SwiftUI

struct CloneTile: View {
    let entry: RegistryEntry
    let status: CloneStatus
    let running: Bool
    let busy: String?
    let selected: Bool
    @State private var hovering = false

    var body: some View {
        VStack(spacing: Brand.Space.s) {
            CloneIconView(entry: entry, size: 72)
                .opacity(status == .originalMissing ? 0.55 : 1)
            Text(entry.manifest.name)
                .font(.callout.weight(.medium))
                .multilineTextAlignment(.center)
                .lineLimit(2)
                .truncationMode(.middle)
                .frame(height: 34, alignment: .top)
            StatusLabel(status: status, running: running, busy: busy)
        }
        .padding(.vertical, 12)
        .padding(.horizontal, 8)
        .frame(width: 148)
        .background(
            RoundedRectangle(cornerRadius: Brand.cardRadius, style: .continuous)
                .fill(selected ? Brand.accent.opacity(0.16) : (hovering ? Color.primary.opacity(0.05) : .clear))
        )
        .overlay(
            RoundedRectangle(cornerRadius: Brand.cardRadius, style: .continuous)
                .strokeBorder(selected ? Brand.accent.opacity(0.55) : .clear, lineWidth: 1)
        )
        .contentShape(RoundedRectangle(cornerRadius: Brand.cardRadius, style: .continuous))
        .onHover { hovering = $0 }
        .help(entry.manifest.name)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(entry.manifest.name)
        .accessibilityValue(busy ?? StatusLabel.text(status: status, running: running))
        .accessibilityAddTraits(selected ? [.isButton, .isSelected] : .isButton)
        .accessibilityHint("Double-click to open")
    }
}
