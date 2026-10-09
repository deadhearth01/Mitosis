import Foundation
import MitosisCore

struct BadgeColor: Identifiable, Hashable, Sendable {
    var name: String
    var hex: String
    var id: String { hex }
}

/// The quick sheet's fields and rules. The user types a label; the clone is named "<App> (<Label>)".
struct NewCloneForm: Equatable, Sendable {
    var appName: String
    private(set) var label: String
    private(set) var badgeText: String
    private(set) var badgeEdited = false
    var color: String
    var mode: CloneMode?
    var openAfter = true

    static let colors: [BadgeColor] = [
        .init(name: "Blue", hex: "#0A84FF"), .init(name: "Green", hex: "#30D158"), .init(name: "Orange", hex: "#FF9F0A"),
        .init(name: "Red", hex: "#FF453A"), .init(name: "Purple", hex: "#BF5AF2"), .init(name: "Pink", hex: "#FF375F"),
        .init(name: "Yellow", hex: "#FFD60A"), .init(name: "Gray", hex: "#8E8E93"),
    ]

    static func suggestedLabel(appName: String, existingNames: Set<String>) -> String {
        let taken = Set(existingNames.map { $0.lowercased() })
        let candidates = ["Work", "Personal"] + (2...99).map(String.init)
        return candidates.first { !taken.contains(CloneName.compose(app: appName, label: $0).lowercased()) } ?? "New"
    }

    init(appName: String, existingNames: Set<String>, usedColors: Set<String> = []) {
        self.appName = appName
        let label = Self.suggestedLabel(appName: appName, existingNames: existingNames)
        self.label = label
        self.badgeText = Self.initial(of: label)
        self.color = (Self.colors.first { !usedColors.contains($0.hex) } ?? Self.colors[0]).hex
    }

    var composedName: String { CloneName.compose(app: appName, label: label) }

    var badge: Badge {
        Badge(text: badgeText.isEmpty ? Self.initial(of: appName) : badgeText, color: color)
    }

    mutating func setLabel(_ newValue: String) {
        label = newValue
        if !badgeEdited { badgeText = Self.initial(of: newValue) }
    }

    mutating func setBadge(_ newValue: String) {
        badgeText = String(newValue.trimmingCharacters(in: .whitespaces).prefix(2)).uppercased()
        badgeEdited = !badgeText.isEmpty
    }

    func validationMessage(existingNames: Set<String>) -> String? {
        guard !label.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return "Add a label, like Work." }
        let name: String
        do { name = try CloneName.validate(composedName) } catch let e as CloneNameError { return e.description } catch { return "\(error)" }
        if existingNames.contains(where: { $0.lowercased() == name.lowercased() }) { return "You already have \(name)." }
        return nil
    }

    private static func initial(of text: String) -> String {
        text.trimmingCharacters(in: .whitespacesAndNewlines).first.map { String($0).uppercased() } ?? ""
    }
}
