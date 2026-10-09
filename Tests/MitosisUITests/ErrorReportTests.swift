import Foundation
import MitosisCore
import Testing
@testable import MitosisUI

@Suite struct ErrorReportTests {
    @Test func hidesTheHomeFolderAndPrefillsAnIssue() throws {
        let home = NSHomeDirectory()
        let error = CloneError.failed(step: "copy", message: "couldn't write \(home)/Applications/Mitosis/X.app")
        let report = ErrorReport.make(error: error, appName: "Slack", appVersion: "1.2", mode: .identity)
        #expect(report.message == "Cloning failed while copying the app: couldn't write ~/Applications/Mitosis/X.app")
        #expect(!report.details.contains(home))
        #expect(report.details.contains("App: Slack 1.2"))
        #expect(report.details.contains("Mode: Full clone"))
        #expect(report.details.contains("Mitosis: \(Mitosis.version)"))
        let items = try #require(URLComponents(url: report.issueURL, resolvingAgainstBaseURL: false)?.queryItems)
        func value(_ name: String) -> String? { items.first { $0.name == name }?.value }
        #expect(report.issueURL.absoluteString.hasPrefix("https://github.com/deadhearth01/Mitosis/issues/new?"))
        #expect(value("template") == "bug_report.yml")
        #expect(value("app") == "Slack 1.2")
        #expect(value("version") == Mitosis.version)
        #expect(value("what")?.contains("~/Applications/Mitosis/X.app") == true)
        #expect(value("macos")?.isEmpty == false)
    }

    @Test func plainErrorsUseTheirLocalizedDescription() {
        let error = NSError(domain: NSCocoaErrorDomain, code: NSFileWriteNoPermissionError,
                            userInfo: [NSLocalizedDescriptionKey: "You don't have permission."])
        #expect(ErrorReport.make(error: error, appName: nil, appVersion: nil, mode: nil).message == "You don't have permission.")
    }
}
