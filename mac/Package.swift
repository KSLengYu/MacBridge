// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "MacBridgeMac",
    platforms: [
        .macOS(.v14)
    ],
    targets: [
        .executableTarget(
            name: "MacBridgeMac",
            path: "Sources/MacBridgeMac"
        )
    ]
)
