import MitosisCore
import SwiftUI

struct QuickSheetView: View {
    @Bindable var model: NewCloneModel
    let close: () -> Void
    @AppStorage(Settings.showTipsKey) private var showTips = true
    @FocusState private var labelFocused: Bool

    var body: some View {
        if let app = model.chosen, let form = model.form {
            content(app: app, form: form)
        }
    }

    private func content(app: CatalogApp, form: NewCloneForm) -> some View {
        VStack(alignment: .leading, spacing: Brand.Space.m) {
            HStack(spacing: Brand.Space.s) {
                AppIconView(path: app.url.path, size: 28)
                Text("New Clone of \(app.name)")
                    .font(.title2.weight(.semibold))
            }
            HStack(alignment: .top, spacing: Brand.Space.xl) {
                VStack(spacing: Brand.Space.m - 4) {
                    Group {
                        if let image = IconPreview.image(source: app.url, badge: form.badge) {
                            Image(nsImage: image).resizable().frame(width: 128, height: 128)
                        } else {
                            AppIconView(path: app.url.path, size: 128)
                        }
                    }
                    .accessibilityHidden(true)
                    Text(form.composedName)
                        .font(.headline)
                        .multilineTextAlignment(.center)
                        .lineLimit(3)
                        .truncationMode(.middle)
                        .accessibilityLabel("Clone name: \(form.composedName)")
                }
                .frame(width: 180)
                fields(form: form)
            }
            supportLine
            Spacer(minLength: 0)
            if showTips {
                TipRow(pose: .split, text: "The badge helps you spot this copy in the Dock. Mitosis opens it once to check that it starts.")
            }
            HStack {
                Button("Back") { model.back() }
                Spacer()
                Button("Cancel", role: .cancel, action: close)
                    .keyboardShortcut(.cancelAction)
                Button("Create Clone") { Task { await model.create() } }
                    .keyboardShortcut(.defaultAction)
                    .disabled(model.validationMessage != nil)
            }
        }
        .padding(Brand.Space.l)
        .onAppear { labelFocused = true }
        .alert("Make a full copy?", isPresented: $model.askFullCopy) {
            Button("Create Copy") { Task { await model.create(confirmedFullCopy: true) } }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("\(app.name) is on a different drive, so this clone will be a full copy and use \(Format.bytes(model.preflight?.copyBytes ?? 0)).")
        }
    }

    private func fields(form: NewCloneForm) -> some View {
        Form {
            VStack(alignment: .leading, spacing: Brand.Space.xs) {
                TextField("Label", text: Binding(get: { model.form?.label ?? "" }, set: { model.form?.setLabel($0) }),
                          prompt: Text("Work"))
                    .focused($labelFocused)
                if let message = model.validationMessage {
                    Label(message, systemImage: "exclamationmark.triangle.fill")
                        .font(.caption)
                        .foregroundStyle(.red)
                } else {
                    Text("Shown in the name, like \(app(form)) (Work).")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
            TextField("Badge", text: Binding(get: { model.form?.badgeText ?? "" }, set: { model.form?.setBadge($0) }),
                      prompt: Text(String(form.appName.prefix(1))))
                .frame(width: 120)
                .help("One or two letters or digits")
            LabeledContent("Color") {
                ColorSwatches(selection: Binding(get: { model.form?.color ?? Badge.defaultColor }, set: { model.form?.color = $0 }))
            }
            Toggle("Open after creating", isOn: Binding(get: { model.form?.openAfter ?? true }, set: { model.form?.openAfter = $0 }))
            DisclosureGroup("Advanced") {
                Picker("Mode", selection: Binding(get: { model.form?.mode }, set: { model.form?.mode = $0 })) {
                    Text("Automatic (recommended)").tag(CloneMode?.none)
                    Text("Full clone").tag(CloneMode?.some(.identity))
                    Text("Compatibility mode").tag(CloneMode?.some(.fallback))
                }
                .fixedSize()
            }
        }
        .formStyle(.columns)
    }

    private func app(_ form: NewCloneForm) -> String { form.appName }

    @ViewBuilder private var supportLine: some View {
        if let id = model.chosen?.id, let decision = model.support[id] {
            HStack(alignment: .firstTextBaseline, spacing: Brand.Space.s) {
                SupportCapsule(support: decision.support)
                Text(decision.reasons.first ?? "")
                    .font(.callout)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }
}
