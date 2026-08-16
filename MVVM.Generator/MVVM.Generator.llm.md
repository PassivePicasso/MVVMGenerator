# MVVM.Generator — LLM Reference

MVVM.Generator is a C# Roslyn incremental source generator that eliminates MVVM boilerplate. It generates `INotifyPropertyChanged` properties, `ICommand` implementations, WPF `DependencyProperty`, and Avalonia `StyledProperty` declarations from annotated fields and methods. All generated code emits into partial classes.

## Installation

Add the `MVVM.Generator` NuGet package. Use `using MVVM.Generator.Attributes;` in consuming files. All target classes **must** be declared `partial`.

---

## Attributes

### `[AutoNotify]` — on fields

Generates a public property with `INotifyPropertyChanged` plumbing. If the class hierarchy doesn't already implement `INotifyPropertyChanged`, the generator adds the interface, event, and `OnPropertyChanged` method.

| Parameter | Type | Default | Effect |
|---|---|---|---|
| `GetterAccess` | `Access` enum | `Public` | Getter visibility (`Private`, `Internal`, `Protected`, `Public`) |
| `SetterAccess` | `Access` enum | `Public` | Setter visibility |
| `IsVirtual` | `bool` | `false` | Emits `virtual` on the property |
| `PropertyChangedHandlerName` | `string?` | `null` | Method invoked on change — signature: `void(object?, EventArgs)` |
| `CollectionChangedHandlerName` | `string?` | `null` | Method invoked on collection change — signature: `void(object?, NotifyCollectionChangedEventArgs)`. Auto-subscribes/unsubscribes. |

**Naming convention:** `_fieldName` or `fieldName` → `FieldName`. Prefix `s_` is stripped.

**Example:**
```csharp
public partial class ViewModel
{
    [AutoNotify(SetterAccess = Access.Private)]
    private string _name;

    [AutoNotify(PropertyChangedHandlerName = nameof(OnStatusChanged))]
    private bool isActive;
    private void OnStatusChanged(object? sender, EventArgs e) { }

    [AutoNotify(CollectionChangedHandlerName = nameof(OnItemsChanged))]
    private ObservableCollection<string> items = new();
    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) { }
}
```

### `[DependsOn]` — on fields or properties

Declares that a property depends on one or more other properties. When the referenced property changes, `PropertyChanged` fires for the dependent too.

```csharp
public partial class ViewModel
{
    [AutoNotify] private string firstName;
    [AutoNotify] private string lastName;

    // Automatic: generator scans property body and detects references to AutoNotify fields
    public string FullName => $"{firstName} {lastName}";

    // Explicit: DependsOn adds additional dependency edges
    [DependsOn(nameof(firstName), nameof(lastName))]
    public bool HasName => !string.IsNullOrEmpty(firstName);
}
```

**Dependency direction:** If property A reads field B, then A *depends on* B. When B changes, A is notified. The generator also builds automatic dependencies by scanning property bodies for AutoNotify field references.

### `[AutoCommand]` — on methods

Generates a nested `ICommand` class and a lazy-initialized command property.

| Parameter | Type | Effect |
|---|---|---|
| `canExecuteMethod` | `string?` | Name of a `bool`-returning method with matching parameters |

**Rules:**
- Method must be `public`
- 0 or 1 parameters
- Return type: `void` or `Task`
- CanExecute method (if specified) must return `bool` with matching parameter count and types

```csharp
public partial class ViewModel
{
    [AutoCommand(nameof(CanSave))]
    public void Save() { /* ... */ }
    public bool CanSave() => IsDirty;

    [AutoCommand]
    public void Delete(int id) { /* ... */ }

    [AutoCommand]
    public static void Reset() { /* ... */ }  // Static commands supported

    [AutoCommand]
    public async Task LoadAsync() { /* ... */ }  // Async commands supported
}
```

**Generated names:** Method `Save` → property `SaveCommand`, nested class `SaveCommandClass`.

### `[AddAttribute]` — on methods (with `[AutoCommand]`)

Adds arbitrary attributes to the generated command property. `AllowMultiple = true`.

```csharp
[AutoCommand]
[AddAttribute(typeof(JsonIgnoreAttribute), [])]
public void DoWork() { }
// Generated: [JsonIgnore] public ICommand DoWorkCommand => ...
```

### `[AutoDProp]` — on fields (WPF)

Generates a WPF `DependencyProperty` with static registration and CLR wrapper.

```csharp
public partial class MyControl : Control
{
    [AutoDProp] private string header;
    [AutoDProp] private bool isEnabled = true;
}
// Generated: public static readonly DependencyProperty HeaderProperty = ...
//            public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
```

### `[AutoSProp]` — on fields (Avalonia)

Generates an Avalonia `StyledProperty<T>` with static registration and CLR wrapper.

```csharp
public partial class MyControl : TemplatedControl
{
    [AutoSProp] private string title;
}
// Generated: public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<MyControl, string>(nameof(Title));
//            public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
```

---

## Diagnostics

### Generator Diagnostics

| ID | Severity | Meaning |
|---|---|---|
| MGAN001 | Error | Circular property dependency detected |
| MGAN002 | Error | Cannot generate property for static type |
| MGAN003 | Error | DependsOn references nonexistent/non-AutoNotify property |
| MGAC001 | Error | AutoCommand method must be public |
| MGAC002 | Error | Invalid command method signature (must be void/Task, 0-1 params) |
| MGAC003 | Error | CanExecute method must return bool with matching parameters |
| MGAC101 | Info | Suggestion to add a CanExecute method (disabled by default) |

### Analyzer Diagnostics

| ID | Severity | Meaning |
|---|---|---|
| MAANA001 | Error | Static field cannot have AutoNotify |
| MAANA002 | Error | Generated property name conflicts with existing member |
| MAANA003 | Error | PropertyChangedHandler method invalid (wrong signature or missing) |
| MAANA004 | Error | CollectionChangedHandler method invalid (wrong signature or missing) |
| MAACA001 | Error | AutoCommand method must be public |
| MAACA002 | Error | AutoCommand method must have 0 or 1 parameters |
| MAACA003 | Error | CanExecute method invalid (wrong return type or parameter mismatch) |
| MAACA004 | Error | Generated command class name conflicts with existing member |

### Code Fix

| Diagnostic | Fix |
|---|---|
| MAACA001 | "Make method public" — adds `public` modifier |

---

## Architecture

- **ViewModelGenerator** (`IIncrementalGenerator`): Entry point. Discovers classes, runs attribute generators, renders partial class output.
- **AttributeGeneratorHandler<TSymbol, TAttribute>**: Base class for all generators. Filters members by attribute, validates, and delegates to `Execute`.
- **PropertyGenerator**: Emits property code with getter/setter visibility, handlers, and dependency notifications.
- **CommandClassGenerator**: Emits nested `ICommand` class with constructor, `CanExecute`, and `Execute`.
- **AttributeProcessor** (`IDependencyAnalyzer`): Builds reverse dependency map by scanning property bodies and `[DependsOn]` attributes.
- **TypeHelper**: Resolves type names including generics, nullables, arrays, and C# keyword aliases.

## Key Constraints

1. Target classes must be `partial`
2. AutoNotify fields must not be `static`
3. AutoCommand methods must be `public` with 0–1 parameters returning `void` or `Task`
4. Handler signatures are strict: `void(object?, EventArgs)` for property, `void(object?, NotifyCollectionChangedEventArgs)` for collection
5. Circular dependencies are rejected
6. `DependsOn` can reference either field names or property names
7. Inheritance is supported — derived classes reuse base `INotifyPropertyChanged` implementation
8. Static methods generate static commands (no owner reference)
