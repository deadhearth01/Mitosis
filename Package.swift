// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "MitosisCore",
    platforms: [.macOS(.v15)],
    products: [
        .library(name: "MitosisCore", targets: ["MitosisCore"]),
        .library(name: "MitosisUI", targets: ["MitosisUI"]),
        .executable(name: "MitosisApp", targets: ["MitosisApp"]),
        .executable(name: "mitosis", targets: ["mitosis"]),
        .executable(name: "LaunchStub", targets: ["LaunchStub"]),
        .executable(name: "MitosisRouter", targets: ["MitosisRouter"]),
    ],
    dependencies: [
        .package(url: "https://github.com/apple/swift-argument-parser", from: "1.5.0"),
    ],
    targets: [
        .target(
            name: "MitosisCore",
            resources: [.copy("Resources/profiles.json")]
        ),
        .executableTarget(name: "LaunchStub"),
        // Mitosis Link Router: receives sign-in links and delivers them to the right copy of an app.
        .executableTarget(name: "MitosisRouter", dependencies: ["MitosisCore"]),
        .executableTarget(
            name: "mitosis",
            dependencies: [
                "MitosisCore",
                .product(name: "ArgumentParser", package: "swift-argument-parser"),
            ]
        ),
        // The Mac app: all UI lives in MitosisUI; MitosisApp is the tiny executable that runs it.
        .target(
            name: "MitosisUI",
            dependencies: ["MitosisCore"],
            resources: [.copy("Resources/Mascot")]
        ),
        .executableTarget(name: "MitosisApp", dependencies: ["MitosisUI"]),
        // Developer tool: renders every screen to PNG for visual review. Not shipped.
        .executableTarget(name: "MitosisSnapshot", dependencies: ["MitosisUI"]),
        .testTarget(
            name: "MitosisCoreTests",
            dependencies: ["MitosisCore", "LaunchStub", "mitosis", "MitosisRouter"]
        ),
        .testTarget(
            name: "MitosisUITests",
            dependencies: ["MitosisUI", "MitosisCore"]
        ),
    ]
)
