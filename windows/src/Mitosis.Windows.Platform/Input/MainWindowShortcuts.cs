using Windows.System;

namespace Mitosis.Windows.Platform.Input;

/// <summary>Where a main-window key event routes (macOS menu commands are the reference:
/// MitosisRootApp.swift:46-64). Shortcuts preserve the Stage 13 Windows bindings; F1 and
/// Ctrl+, honor the menu-bar-free adaptation (matrix W05/W44).</summary>
public enum MainWindowShortcut
{
    None,

    /// <summary>Open Mitosis Help. Windows: F1 (standard Windows help key). macOS: ⌘?
    /// (Command+Shift+/) — F1 is the Windows-native equivalent adaptation.</summary>
    Help,

    /// <summary>Open Settings. Windows: Ctrl+, (standard Windows settings shortcut).
    /// macOS: opens via the app menu (⌘,) — same key shape on Windows.</summary>
    Settings,

    /// <summary>Focus the sidebar search box. macOS: no explicit binding found (audit
    /// §6: SwiftUI sidebar search may provide ⌘F natively); Windows binds Ctrl+F
    /// deliberately so search stays one keystroke away.</summary>
    FocusSearch,
}

/// <summary>Pure routing decision for main-window content key events. N (New Clone) is
/// not routed here: the sidebar button's KeyboardAccelerator already handles Ctrl+N and
/// a second router path would double-invoke the command. Key handling stays honest —
/// routed actions command only windows/features that exist.</summary>
public static class MainWindowShortcuts
{
    /// <summary>WinUI's VirtualKey enumeration lacks an OEM-comma member; 188 is its
    /// documented, stable value (VirtualKey.OemComma = VK_OEM_COMMA = 0xBC).</summary>
    private const VirtualKey OemComma = (VirtualKey)188;

    public static MainWindowShortcut Route(bool controlDown, VirtualKey key) =>
        key switch
        {
            VirtualKey.F1 => MainWindowShortcut.Help,
            _ when controlDown && key == OemComma => MainWindowShortcut.Settings,
            _ when controlDown && key == VirtualKey.F => MainWindowShortcut.FocusSearch,
            _ => MainWindowShortcut.None,
        };
}
