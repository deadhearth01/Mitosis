using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mitosis.Windows.ViewModels;

/// <summary>Minimal observable base. Hand-rolled for Stage 2 (see decision D2 in
/// docs/WINDOWS-ARCHITECTURE-MAPPING.md): no MVVM toolkit dependency yet.
/// <para>Thread affinity: these view models raise <see cref="PropertyChanged"/> for state
/// that x:Bind applies directly to XAML elements. Continuations must therefore stay on the
/// caller's synchronization context, which is why the Windows view models do not call
/// <c>ConfigureAwait(false)</c> after an await that precedes a state change. Resuming on a
/// pool thread there raised <c>RPC_E_WRONG_THREAD</c> inside <c>Application.Resources</c>
/// (Stage 27 startup crash); the rule is pinned by ViewModelThreadAffinityTests.</para>
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
