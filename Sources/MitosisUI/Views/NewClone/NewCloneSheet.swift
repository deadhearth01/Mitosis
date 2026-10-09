import MitosisCore
import SwiftUI

struct NewCloneSheet: View {
    @Bindable var model: NewCloneModel
    let close: () -> Void
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        Group {
            switch model.step {
            case .pick:
                AppPickerView(model: model, close: close)
            case .details:
                QuickSheetView(model: model, close: close)
            case .working(let label):
                WorkingView(name: model.form?.composedName ?? "", label: label)
            case .done(let entry):
                DoneView(entry: entry, opened: model.form?.openAfter ?? true, close: close)
            case .failed(let report, let canRetry):
                FailureView(title: "Couldn't create \(model.form?.composedName ?? "the clone")", report: report,
                            retryTitle: canRetry ? "Try Compatibility Mode" : nil,
                            retry: canRetry ? { Task { await model.retryInCompatibilityMode() } } : nil,
                            close: close)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            }
        }
        .frame(width: model.step == .pick ? 700 : 640, height: model.step == .pick ? 680 : 440)
        .animation(reduceMotion ? nil : .easeInOut(duration: 0.2), value: model.step)
        .interactiveDismissDisabled(model.isWorking)
        .task { await model.loadApps() }
    }
}

/// A short guided-mode hint with Mito. Hidden after the first clone (Settings → Show tips again).
struct TipRow: View {
    let pose: Mascot
    let text: String

    var body: some View {
        HStack(spacing: Brand.Space.m - 4) {
            MascotView(pose: pose, size: 44)
            Text(text)
                .font(.callout)
                .fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: 0)
        }
        .padding(.horizontal, Brand.Space.m - 4)
        .padding(.vertical, Brand.Space.s)
        .background(Brand.primaryBlue.opacity(0.08), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
    }
}
