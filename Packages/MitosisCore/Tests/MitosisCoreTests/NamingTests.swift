import Testing
@testable import MitosisCore

@Suite struct NamingTests {
    @Test(arguments: [
        ("Slack Work", "slack-work"),
        ("Slack (Work)", "slack-work"),
        ("  Café   Ñoño!! ", "cafe-nono"),
        ("Client_A 2", "client-a-2"),
        ("日本", "clone"),
        ("---", "clone"),
    ])
    func slugFromName(input: String, expected: String) {
        #expect(Slug.make(from: input) == expected)
    }

    @Test func slugAvoidsExistingValues() {
        #expect(Slug.make(from: "Slack Work", existing: ["slack-work"]) == "slack-work-2")
        #expect(Slug.make(from: "Slack Work", existing: ["slack-work", "slack-work-2"]) == "slack-work-3")
    }

    @Test func slugIsCappedAt40Characters() {
        let s = Slug.make(from: String(repeating: "abc ", count: 30))
        #expect(s.count <= 40)
        #expect(!s.hasSuffix("-"))
    }

    @Test func validNamesAreTrimmed() throws {
        #expect(try CloneName.validate("  Slack (Work) ") == "Slack (Work)")
        #expect(try CloneName.validate("Ünïcode Wörk") == "Ünïcode Wörk")
    }

    // Review Focus: odd names must be rejected clearly.
    @Test func invalidNamesAreRejected() {
        #expect(throws: CloneNameError.empty) { try CloneName.validate("   ") }
        #expect(throws: CloneNameError.leadingDot) { try CloneName.validate(".hidden") }
        #expect(throws: CloneNameError.invalidCharacter("/")) { try CloneName.validate("a/b") }
        #expect(throws: CloneNameError.invalidCharacter(":")) { try CloneName.validate("a:b") }
        #expect(throws: CloneNameError.tooLong) { try CloneName.validate(String(repeating: "x", count: 61)) }
    }

    @Test func errorMessagesAreFriendly() {
        #expect(CloneNameError.invalidCharacter("/").description == "Clone names can't contain \"/\".")
        #expect(CloneNameError.empty.description == "Give the clone a name.")
    }

    // A8: "<App> (<Label>)" so the label never merges with the app name.
    @Test func composesAppAndLabel() {
        #expect(CloneName.compose(app: "Slack", label: " Work ") == "Slack (Work)")
        #expect(CloneName.compose(app: " Google Chrome ", label: "Client A") == "Google Chrome (Client A)")
        #expect(CloneName.compose(app: "Slack", label: "  ") == "Slack")
    }
}
