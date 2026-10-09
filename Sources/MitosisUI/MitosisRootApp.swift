import SwiftUI

public struct MitosisRootApp: App {
    public init() {}

    public var body: some Scene {
        Window("Mitosis", id: "main") {
            Text("Mitosis").frame(minWidth: 400, minHeight: 300)
        }
    }
}
