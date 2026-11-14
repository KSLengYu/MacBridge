// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "MacBridgeInstaller",
    platforms: [
        .macOS(.v14)
    ],
    targets: [
        .executableTarget(
            name: "MacBridgeInstaller",
            path: "Sources/MacBridgeInstaller"
        )
    ]
)
