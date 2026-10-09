import SwiftUI

enum SystemLinks {
    static let privacy = URL(string: "x-apple.systempreferences:com.apple.settings.PrivacySecurity.extension")!
    static let repository = URL(string: "https://github.com/deadhearth01/Mitosis")!
    static let newIssue = URL(string: "https://github.com/deadhearth01/Mitosis/issues/new/choose")!
    static let releases = URL(string: "https://github.com/deadhearth01/Mitosis/releases")!
}

/// Three short pages on first launch: what Mitosis does, why clones ask for permissions, and the first clone.
struct OnboardingView: View {
    let finish: (_ startFirstClone: Bool) -> Void
    @State private var page = 0
    @Environment(\.openURL) private var openURL
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    private struct Page {
        var pose: Mascot
        var title: String
        var text: String
    }

    private let pages = [
        Page(pose: .wave, title: "Welcome to Mitosis",
             text: "Run separate copies of your Mac apps. Each copy has its own login, data, and Dock icon."),
        Page(pose: .key, title: "Each clone asks for its own permissions",
             text: "macOS treats every clone as a new app, so it may ask again for notifications, files, or the camera."),
        Page(pose: .split, title: "Make your first clone",
             text: "Pick an app, add a label like Work, and Mitosis checks that the copy starts."),
    ]

    var body: some View {
        let current = pages[page]
        VStack(spacing: Brand.Space.m) {
            Spacer(minLength: 0)
            MascotView(pose: current.pose, size: 160)
                .id(page)
                .transition(reduceMotion ? .identity : .opacity)
            Text(current.title)
                .font(.title.weight(.semibold))
                .multilineTextAlignment(.center)
            Text(current.text)
                .font(.title3)
                .multilineTextAlignment(.center)
                .foregroundStyle(.secondary)
                .frame(maxWidth: 420)
                .fixedSize(horizontal: false, vertical: true)
            if page == 1 {
                Button("Open Privacy & Security") { openURL(SystemLinks.privacy) }
                    .buttonStyle(.link)
            }
            Spacer(minLength: 0)
            HStack {
                HStack(spacing: 6) {
                    ForEach(pages.indices, id: \.self) { i in
                        Circle()
                            .fill(i == page ? Brand.accent : Color.secondary.opacity(0.35))
                            .frame(width: 7, height: 7)
                    }
                }
                .accessibilityElement()
                .accessibilityLabel("Page \(page + 1) of \(pages.count)")
                Spacer()
                if page > 0 {
                    Button("Back") { go(page - 1) }
                }
                if page < pages.count - 1 {
                    Button("Next") { go(page + 1) }
                        .keyboardShortcut(.defaultAction)
                } else {
                    Button("Later") { finish(false) }
                        .keyboardShortcut(.cancelAction)
                    Button("Choose an App") { finish(true) }
                        .keyboardShortcut(.defaultAction)
                }
            }
        }
        .padding(Brand.Space.xl)
        .frame(width: 580, height: 460)
    }

    private func go(_ index: Int) {
        if reduceMotion { page = index } else { withAnimation(.easeInOut(duration: 0.25)) { page = index } }
    }
}
