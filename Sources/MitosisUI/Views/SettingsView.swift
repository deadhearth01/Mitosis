import AppKit
import MitosisCore
import SwiftUI

struct SettingsView: View {
    let model: AppModel
    let updates: UpdateChecker

    var body: some View {
        TabView {
            GeneralSettings(model: model, updates: updates)
                .tabItem { Label("General", systemImage: "gearshape") }
            AdvancedSettings()
                .tabItem { Label("Advanced", systemImage: "slider.horizontal.3") }
        }
        .frame(width: 540)
        .tint(Brand.accent)
    }
}

struct GeneralSettings: View {
    let model: AppModel
    let updates: UpdateChecker
    @AppStorage(Prefs.menuBarKey) private var showMenuBar = false
    @AppStorage(Prefs.checkUpdatesKey) private var checkUpdates = true
    @AppStorage(Prefs.autoUpdateKey) private var autoUpdate = true
    @State private var cliMessage: String?
    @State private var cliFailed = false

    var body: some View {
        Form {
            Section {
                Toggle("Show Mitosis in the menu bar", isOn: $showMenuBar)
                Toggle("Check for updates automatically", isOn: $checkUpdates)
                HStack {
                    updateStatus
                    Spacer()
                    Button("Check Now") { Task { await updates.check() } }
                        .disabled(updates.state == .checking)
                }
            }
            Section("Clones") {
                VStack(alignment: .leading, spacing: 4) {
                    Toggle("Update clones automatically", isOn: $autoUpdate)
                        .onChange(of: autoUpdate) { _, _ in
                            model.syncAutoRefreshAgent()
                            model.autoRefresh()
                        }
                    Text("When an app updates, Mitosis rebuilds its clones in the background, even while Mitosis is closed. Logins and data stay. Clones that are open update after you quit them.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
                LabeledContent("Location") {
                    HStack {
                        Text(CommandLineTool.display(model.services?.environment.clonesDir ?? URL(fileURLWithPath: "~/Applications/Mitosis")))
                            .foregroundStyle(.secondary)
                        Button("Show in Finder") { revealClones() }
                    }
                }
                Text("Clones stay in an Applications folder because macOS only sends notifications to apps there.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Section("Command line") {
                HStack {
                    Text("Use Mitosis from Terminal with the `mitosis` command.")
                    Spacer()
                    Button("Install Command") { installCLI() }
                }
                if let cliMessage {
                    Label(cliMessage, systemImage: cliFailed ? "exclamationmark.triangle.fill" : "checkmark.circle.fill")
                        .font(.caption)
                        .foregroundStyle(cliFailed ? .red : .secondary)
                        .textSelection(.enabled)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
        }
        .formStyle(.grouped)
        .scrollDisabled(true)
        .frame(height: 470)
    }

    @ViewBuilder private var updateStatus: some View {
        switch updates.state {
        case .idle:
            Text("Mitosis \(Mitosis.version)").foregroundStyle(.secondary)
        case .checking:
            HStack(spacing: 6) { ProgressView().controlSize(.small); Text("Checking…").foregroundStyle(.secondary) }
        case .upToDate:
            Label("Mitosis \(Mitosis.version) is up to date.", systemImage: "checkmark.circle.fill").foregroundStyle(.secondary)
        case .available(let version, let url):
            HStack {
                Label("Mitosis \(version) is available.", systemImage: "arrow.down.circle.fill")
                Link("View Release", destination: url)
            }
        case .failed(let message):
            Label(message, systemImage: "exclamationmark.triangle.fill").foregroundStyle(.secondary)
        }
    }

    private func revealClones() {
        guard let dir = model.services?.environment.clonesDir else { return }
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        NSWorkspace.shared.open(dir)
    }

    private func installCLI() {
        let tool = CommandLineTool.bundledTool
        guard FileManager.default.isExecutableFile(atPath: tool.path) else {
            cliFailed = true
            cliMessage = "This copy of Mitosis doesn't include the command. Reinstall Mitosis, then try again."
            return
        }
        do {
            let result = try CommandLineTool.install(target: tool)
            cliFailed = false
            let bin = CommandLineTool.defaultBinDir
            let hint = shellConfigMentionsLocalBin() ? "" : " If Terminal can't find it, add this line to ~/.zshrc: export PATH=\"$HOME/.local/bin:$PATH\""
            cliMessage = (result == .installed ? "Installed" : "Already installed") + " at \(CommandLineTool.display(bin.appendingPathComponent("mitosis"))).\(hint)"
        } catch {
            cliFailed = true
            cliMessage = "\(error)"
        }
    }

    /// GUI apps don't see the shell's PATH, so look for ~/.local/bin in the usual shell files instead.
    private func shellConfigMentionsLocalBin() -> Bool {
        let home = FileManager.default.homeDirectoryForCurrentUser
        return [".zshrc", ".zprofile", ".bash_profile", ".bashrc", ".profile"].contains { name in
            ((try? String(contentsOf: home.appendingPathComponent(name), encoding: .utf8)) ?? "").contains(".local/bin")
        }
    }
}

struct AdvancedSettings: View {
    @AppStorage(Prefs.defaultModeKey) private var defaultMode = ""
    @AppStorage(Prefs.showTipsKey) private var showTips = true
    @AppStorage(Prefs.onboardedKey) private var onboarded = false
    @State private var tipsReset = false

    var body: some View {
        Form {
            Section {
                Picker("Default mode", selection: $defaultMode) {
                    Text("Automatic (recommended)").tag("")
                    Text("Full clone").tag(CloneMode.identity.rawValue)
                    Text("Compatibility mode").tag(CloneMode.fallback.rawValue)
                }
                Text("Automatic picks the best mode for each app. Full clones get their own Dock icon and notifications; compatibility mode works with more apps.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Section {
                HStack {
                    Text("Tips and the welcome screen")
                    Spacer()
                    Button(tipsReset ? "Tips Are On" : "Show Tips Again") {
                        showTips = true
                        onboarded = false
                        tipsReset = true
                    }
                    .disabled(tipsReset)
                }
            }
        }
        .formStyle(.grouped)
        .scrollDisabled(true)
        .frame(height: 260)
    }
}
