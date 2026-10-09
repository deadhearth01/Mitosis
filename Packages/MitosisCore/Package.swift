// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "MitosisCore",
    platforms: [.macOS(.v15)],
    products: [
        .library(name: "MitosisCore", targets: ["MitosisCore"]),
        .executable(name: "mitosis", targets: ["mitosis"]),
        .executable(name: "LaunchStub", targets: ["LaunchStub"]),
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
        .executableTarget(
            name: "mitosis",
            dependencies: [
                "MitosisCore",
                .product(name: "ArgumentParser", package: "swift-argument-parser"),
            ]
        ),
        .testTarget(
            name: "MitosisCoreTests",
            dependencies: ["MitosisCore", "LaunchStub", "mitosis"]
        ),
    ]
)
