import Foundation
import MitosisCore
import Testing
@testable import MitosisUI

@Suite struct CloneLibraryTests {
    let all = [Samples.slackWork, Samples.discordFriends, Samples.slackClient]

    @Test func groupsBySourceAppWithCounts() {
        let groups = CloneLibrary.groups(all)
        #expect(groups.map(\.name) == ["Discord", "Slack"])
        #expect(groups.map(\.count) == [1, 2])
        #expect(groups.last?.bundleID == "com.tinyspeck.slackmacgap")
        #expect(groups.last?.sourcePath == "/Applications/Slack.app")
    }

    @Test func filtersBySidebarItemAndSortsByName() {
        #expect(CloneLibrary.filter(all, sidebar: .all, running: [], search: "").map(\.manifest.name)
                == ["Discord (Friends)", "Slack (Client)", "Slack (Work)"])
        #expect(CloneLibrary.filter(all, sidebar: .running, running: [Samples.slackWork.id], search: "").map(\.id) == [Samples.slackWork.id])
        #expect(CloneLibrary.filter(all, sidebar: .app(bundleID: "com.tinyspeck.slackmacgap"), running: [], search: "").count == 2)
    }

    @Test func searchIgnoresCaseAndAccentsAndMatchesTheAppName() {
        #expect(CloneLibrary.filter(all, sidebar: .all, running: [], search: "WÖRK").map(\.id) == [Samples.slackWork.id])
        #expect(CloneLibrary.filter(all, sidebar: .all, running: [], search: "slack").count == 2)
        #expect(CloneLibrary.filter(all, sidebar: .all, running: [], search: "  ").count == 3)
        #expect(CloneLibrary.filter(all, sidebar: .all, running: [], search: "zzz").isEmpty)
    }

    @Test func labelIsTheBracketedPartOfTheName() {
        #expect(CloneLibrary.label(for: Samples.slackWork) == "Work")
        #expect(CloneLibrary.label(for: Samples.entry("Custom Name", app: "Slack", bundleID: "com.tinyspeck.slackmacgap")) == "Custom Name")
        #expect(CloneLibrary.label(for: Samples.entry("Slack ()", app: "Slack", bundleID: "com.tinyspeck.slackmacgap")) == "Slack ()")
    }
}
