import Foundation
import MitosisCore

/// Where the app finds the engine's pieces: clone folders, the launch stub, and the bundled app profiles.
struct AppServices: Sendable {
    var environment: MitosisEnvironment
    var profiles: ProfileStore

    var builder: CloneBuilder { CloneBuilder(environment: environment, profiles: profiles) }

    /// Inside Mitosis.app the stub lives in Contents/Helpers; during development it sits next to the binary.
    static func stubURL(bundle: URL, executable: URL) -> URL {
        let inBundle = bundle.appendingPathComponent("Contents/Helpers/LaunchStub")
        if FileManager.default.fileExists(atPath: inBundle.path) { return inBundle }
        return executable.resolvingSymlinksInPath().deletingLastPathComponent().appendingPathComponent("LaunchStub")
    }

    /// The link router executable: inside Mitosis.app (Contents/Helpers) or next to the binary during development.
    static func routerURL(bundle: URL, executable: URL) -> URL {
        let inBundle = bundle.appendingPathComponent("Contents/Helpers/MitosisRouter")
        if FileManager.default.fileExists(atPath: inBundle.path) { return inBundle }
        return executable.resolvingSymlinksInPath().deletingLastPathComponent().appendingPathComponent("MitosisRouter")
    }

    var linkRouter: LinkRouterSetup? {
        let executable = Bundle.main.executableURL ?? URL(fileURLWithPath: CommandLine.arguments[0])
        let router = Self.routerURL(bundle: Bundle.main.bundleURL, executable: executable)
        guard FileManager.default.isExecutableFile(atPath: router.path) else { return nil }
        return LinkRouterSetup(environment: environment, handlers: WorkspaceURLHandlers(), routerExecutable: router)
    }

    static func live() throws -> AppServices {
        let vars = ProcessInfo.processInfo.environment
        let executable = Bundle.main.executableURL ?? URL(fileURLWithPath: CommandLine.arguments[0])
        var environment = MitosisEnvironment.standard(stubBinary: stubURL(bundle: Bundle.main.bundleURL, executable: executable),
                                                      root: vars["MITOSIS_HOME"].map { URL(fileURLWithPath: $0) })
        if let trash = vars["MITOSIS_TRASH_DIR"] { environment.trashOverride = URL(fileURLWithPath: trash) }
        return AppServices(environment: environment, profiles: try ProfileStore.bundled())
    }
}
