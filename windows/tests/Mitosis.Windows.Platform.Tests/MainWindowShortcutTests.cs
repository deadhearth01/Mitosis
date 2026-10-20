using Mitosis.Windows.Platform.Input;
using Windows.System;

namespace Mitosis.Windows.Platform.Tests;

[TestClass]
public sealed class MainWindowShortcutTests
{
    private const VirtualKey OemComma = (VirtualKey)188;

    [TestMethod]
    public void F1_RoutesToHelp()
    {
        Assert.AreEqual(MainWindowShortcut.Help, MainWindowShortcuts.Route(controlDown: false, VirtualKey.F1));
        Assert.AreEqual(MainWindowShortcut.Help, MainWindowShortcuts.Route(controlDown: true, VirtualKey.F1));
    }

    [TestMethod]
    public void CtrlComma_RoutesToSettings()
    {
        Assert.AreEqual(MainWindowShortcut.Settings, MainWindowShortcuts.Route(controlDown: true, OemComma));
        Assert.AreEqual(MainWindowShortcut.None, MainWindowShortcuts.Route(controlDown: false, OemComma),
            "Plain comma types text; only Ctrl+, opens Settings.");
    }

    [TestMethod]
    public void CtrlF_RoutesToSearchFocus()
    {
        Assert.AreEqual(MainWindowShortcut.FocusSearch, MainWindowShortcuts.Route(controlDown: true, VirtualKey.F));
        Assert.AreEqual(MainWindowShortcut.None, MainWindowShortcuts.Route(controlDown: false, VirtualKey.F),
            "Plain F types text; the search shortcut deliberately requires Ctrl.");
    }

    [TestMethod]
    public void CtrlN_IsNotRoutedHere()
    {
        Assert.AreEqual(MainWindowShortcut.None, MainWindowShortcuts.Route(controlDown: true, VirtualKey.N),
            "Ctrl+N belongs to the sidebar KeyboardAccelerator — a second path would "
            + "double-invoke NewCloneCommand on the same keypress.");
    }

    [TestMethod]
    public void UnrelatedKeys_RouteNothing()
    {
        Assert.AreEqual(MainWindowShortcut.None, MainWindowShortcuts.Route(controlDown: true, VirtualKey.O));
        Assert.AreEqual(MainWindowShortcut.None, MainWindowShortcuts.Route(controlDown: false, VirtualKey.A));
        Assert.AreEqual(MainWindowShortcut.None, MainWindowShortcuts.Route(controlDown: true, (VirtualKey)190),
            "Ctrl+. (VK_OEM_PERIOD) has no meaning here; only the three documented routes exist.");
        Assert.AreEqual(MainWindowShortcut.None, MainWindowShortcuts.Route(controlDown: false, VirtualKey.Escape),
            "Escape stays with dialogs; the main window has no dismiss action.");
    }

    [TestMethod]
    public void RoutedActions_MatchFeaturesThatExist()
    {
        // Honesty guard: the router may only name shortcuts for existing windows/features.
        var routed = Enum.GetValues<MainWindowShortcut>()
            .Where(k => k != MainWindowShortcut.None)
            .ToList();
        Assert.AreEqual(3, routed.Count);
    }
}
