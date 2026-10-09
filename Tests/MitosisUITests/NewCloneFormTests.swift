import Foundation
import MitosisCore
import Testing
@testable import MitosisUI

@Suite struct NewCloneFormTests {
    @Test func suggestsWorkThenPersonalThenNumbers() {
        #expect(NewCloneForm.suggestedLabel(appName: "Slack", existingNames: []) == "Work")
        #expect(NewCloneForm.suggestedLabel(appName: "Slack", existingNames: ["Slack (Work)"]) == "Personal")
        #expect(NewCloneForm.suggestedLabel(appName: "Slack", existingNames: ["slack (work)", "Slack (Personal)"]) == "2")
        #expect(NewCloneForm.suggestedLabel(appName: "Slack", existingNames: ["Slack (Work)", "Slack (Personal)", "Slack (2)"]) == "3")
    }

    @Test func badgeFollowsTheLabelUntilEdited() {
        var form = NewCloneForm(appName: "Slack", existingNames: [])
        #expect(form.label == "Work")
        #expect(form.badgeText == "W")
        #expect(form.composedName == "Slack (Work)")
        form.setLabel("personal")
        #expect(form.badgeText == "P")
        form.setBadge("xy")
        form.setLabel("Home")
        #expect(form.badgeText == "XY")
        form.setBadge("ABC")
        #expect(form.badgeText == "AB")
        form.setBadge("")
        form.setLabel("Clients")
        #expect(form.badgeText == "C")
        #expect(form.badge.text == "C")
    }

    @Test func emptyBadgeFallsBackToTheAppInitial() {
        var form = NewCloneForm(appName: "Slack", existingNames: [])
        form.setLabel("")
        #expect(form.badge.text == "S")
    }

    @Test func validationMessages() {
        var form = NewCloneForm(appName: "Slack", existingNames: [])
        form.setLabel("  ")
        #expect(form.validationMessage(existingNames: []) == "Add a label, like Work.")
        form.setLabel("Work")
        #expect(form.validationMessage(existingNames: ["slack (work)"]) == "You already have Slack (Work).")
        form.setLabel("A/B")
        #expect(form.validationMessage(existingNames: []) == "Clone names can't contain \"/\".")
        form.setLabel(String(repeating: "x", count: 60))
        #expect(form.validationMessage(existingNames: []) == "Clone names can be at most 60 characters.")
        form.setLabel("Ünïcode 🚀")
        #expect(form.composedName == "Slack (Ünïcode 🚀)")
        #expect(form.validationMessage(existingNames: []) == nil)
    }

    @Test func picksTheFirstUnusedColor() {
        let form = NewCloneForm(appName: "Slack", existingNames: [], usedColors: ["#0A84FF"])
        #expect(form.color == "#30D158")
        #expect(NewCloneForm.colors.count == 8)
    }
}
