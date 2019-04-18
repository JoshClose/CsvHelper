
// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("WpfAnalyzers.DependencyProperties", "WPF0005:Method 'x' should be named 'y'.", Justification = "Don't enforce this")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("WpfAnalyzers.DependencyProperties", "WPF0041:Set mutable dependency properties using SetCurrentValue.", Justification = "Not supported in UWP")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("WpfAnalyzers.DependencyProperties", "WPF1010:Property '[Property]' must notify when value changes.", Justification = "Don't enforce this")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("WpfAnalyzers.DependencyProperties", "WPF1011:Implement INotifyPropertyChanged.", Justification = "Don't enforce this")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("WpfAnalyzers.DependencyProperties", "WPF1012:Notify that property '[Property]' changes..", Justification = "Don't enforce this")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("WpfAnalyzers.DependencyProperties", "WPF1013:Use [CallerMemberName].", Justification = "Don't enforce this, base class doesn't neccessarily support this")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("WpfAnalyzers.DependencyProperties", "WPF1015:Check if value is different before notifying.", Justification = "Don't enforce this, base class doesn't neccessarily support this")]
