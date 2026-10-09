import Foundation

public struct MitosisEnvironment: Sendable {
    public var clonesDir: URL
    public var dataRoot: URL
    public var registryFile: URL
    public var stubBinary: URL
    /// When set, "Trash" moves go here instead of the user's Trash (tests, MITOSIS_TRASH_DIR).
    public var trashOverride: URL?
    public var registerWithLaunchServices: Bool

    public init(clonesDir: URL, dataRoot: URL, registryFile: URL, stubBinary: URL,
                trashOverride: URL? = nil, registerWithLaunchServices: Bool = true) {
        self.clonesDir = clonesDir
        self.dataRoot = dataRoot
        self.registryFile = registryFile
        self.stubBinary = stubBinary
        self.trashOverride = trashOverride
        self.registerWithLaunchServices = registerWithLaunchServices
    }

    /// Spec locations (§5.1 + §14 decisions 3–4); with `root`, everything lives under that folder (tests, MITOSIS_HOME).
    /// Clones must live in an Applications folder (macOS refuses notifications elsewhere), and data paths stay
    /// short because Electron/Chromium put Unix sockets (max 103 chars) inside them.
    public static func standard(stubBinary: URL, root: URL? = nil) -> MitosisEnvironment {
        if let root {
            return MitosisEnvironment(clonesDir: root.appendingPathComponent("Clones"),
                                      dataRoot: root.appendingPathComponent("Data"),
                                      registryFile: root.appendingPathComponent("clones.json"),
                                      stubBinary: stubBinary)
        }
        let home = FileManager.default.homeDirectoryForCurrentUser
        return MitosisEnvironment(clonesDir: home.appendingPathComponent("Applications/Mitosis"),
                                  dataRoot: home.appendingPathComponent("Library/Mitosis/Data"),
                                  registryFile: home.appendingPathComponent("Library/Application Support/Mitosis/clones.json"),
                                  stubBinary: stubBinary)
    }
}

public enum LaunchServices {
    static let lsregister = "/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister"

    public static func register(_ app: URL) throws {
        try Shell.run(lsregister, ["-f", app.path])
    }

    public static func unregister(_ app: URL) {
        _ = try? Shell.run(lsregister, ["-u", app.path], check: false)
    }
}
