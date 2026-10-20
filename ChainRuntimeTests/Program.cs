using System.Collections.ObjectModel;
using System.ComponentModel;

using MVVM.Generator.Attributes;

namespace ChainRuntimeTests;

/// <summary>
/// The class under test: each computed property is named for the path shape it
/// depends on, so a failing assertion names the shape that broke.
/// </summary>
public partial class ChainRoot
{
    [AutoNotify] private bool localField = true;
    [AutoNotify] private ChainMiddle middle = new();

    public bool ReadsLocalFieldAndOneLink => localField && Middle.Flag;

    public bool ReadsCollectionCount => Middle.Items.Count > 1;

    public bool ReadsCollectionViaLinq => Middle.Items.Any();

    public bool ReadsThreeLinks => Middle.Leaf.Flag;
}

public partial class ChainMiddle
{
    [AutoNotify] private bool flag;
    [AutoNotify] private ObservableCollection<string> items = new();
    [AutoNotify] private ChainLeaf leaf = new();
}

public partial class ChainLeaf
{
    [AutoNotify] private bool flag;
}

/// <summary>
/// A collection deriving from ObservableCollection, which is not obliged to raise
/// PropertyChanged for Count and so must be observed via CollectionChanged.
/// </summary>
public class DerivedObservableCollection<T> : ObservableCollection<T>
{
}

internal static class Program
{
    private static int failures;

    private static int Main()
    {
        SameClassFieldNotifies();
        OneLinkOutwardNotifies();
        CollectionContentsNotify();
        ReplacedCollectionMovesSubscription();
        ReplacedHeadMovesSubscription();
        ThreeLinksNotify();
        ReplacedIntermediateMovesSubscription();
        DerivedCollectionNotifies();
        OnlyDependentsOfTheChangedPathNotify();
        SameValueDoesNotNotify();
        SameInstanceKeepsSubscriptions();
        AssigningInOwnChangeHandlerTerminates();

        Console.WriteLine(failures == 0
            ? "All chain runtime tests passed."
            : $"{failures} chain runtime test(s) FAILED.");

        return failures == 0 ? 0 : 1;
    }

    private static void SameClassFieldNotifies()
    {
        var root = new ChainRoot();
        var seen = Watch(root);

        root.LocalField = false;

        Assert("same-class field", seen.Contains(nameof(ChainRoot.ReadsLocalFieldAndOneLink)));
    }

    private static void OneLinkOutwardNotifies()
    {
        var root = new ChainRoot { Middle = new ChainMiddle() };
        var seen = Watch(root);

        root.Middle.Flag = true;

        Assert("one link outward", seen.Contains(nameof(ChainRoot.ReadsLocalFieldAndOneLink)));
    }

    private static void CollectionContentsNotify()
    {
        var root = new ChainRoot { Middle = new ChainMiddle() };
        var seen = Watch(root);

        root.Middle.Items.Add("added");

        Assert("collection add", seen.Contains(nameof(ChainRoot.ReadsCollectionCount)));
        Assert("collection add via linq path", seen.Contains(nameof(ChainRoot.ReadsCollectionViaLinq)));
    }

    private static void ReplacedCollectionMovesSubscription()
    {
        var root = new ChainRoot { Middle = new ChainMiddle() };
        var replaced = root.Middle.Items;
        var current = new ObservableCollection<string>();

        root.Middle.Items = current;

        var seenOnCurrent = Watch(root);
        current.Add("added");
        Assert("replacement collection observed",
            seenOnCurrent.Contains(nameof(ChainRoot.ReadsCollectionCount)));

        var seenOnReplaced = Watch(root);
        replaced.Add("added");
        Assert("replaced collection released",
            !seenOnReplaced.Contains(nameof(ChainRoot.ReadsCollectionCount)));
    }

    private static void ReplacedHeadMovesSubscription()
    {
        var root = new ChainRoot();
        var replaced = root.Middle;
        var current = new ChainMiddle();

        root.Middle = current;

        var seenOnCurrent = Watch(root);
        current.Flag = true;
        Assert("replacement head observed",
            seenOnCurrent.Contains(nameof(ChainRoot.ReadsLocalFieldAndOneLink)));

        var seenOnReplaced = Watch(root);
        replaced.Flag = true;
        Assert("replaced head released",
            !seenOnReplaced.Contains(nameof(ChainRoot.ReadsLocalFieldAndOneLink)));
    }

    private static void ThreeLinksNotify()
    {
        var root = new ChainRoot { Middle = new ChainMiddle() };
        var seen = Watch(root);

        root.Middle.Leaf.Flag = true;

        Assert("three links", seen.Contains(nameof(ChainRoot.ReadsThreeLinks)));
    }

    private static void ReplacedIntermediateMovesSubscription()
    {
        var root = new ChainRoot { Middle = new ChainMiddle() };
        var replaced = root.Middle.Leaf;
        var current = new ChainLeaf();

        root.Middle.Leaf = current;

        var seenOnCurrent = Watch(root);
        current.Flag = true;
        Assert("replacement intermediate observed",
            seenOnCurrent.Contains(nameof(ChainRoot.ReadsThreeLinks)));

        var seenOnReplaced = Watch(root);
        replaced.Flag = true;
        Assert("replaced intermediate released",
            !seenOnReplaced.Contains(nameof(ChainRoot.ReadsThreeLinks)));
    }

    private static void DerivedCollectionNotifies()
    {
        var root = new ChainRoot { Middle = new ChainMiddle() };
        var derived = new DerivedObservableCollection<string>();
        root.Middle.Items = derived;

        var seen = Watch(root);
        derived.Add("added");

        Assert("derived collection observed", seen.Contains(nameof(ChainRoot.ReadsCollectionCount)));
    }

    private static void OnlyDependentsOfTheChangedPathNotify()
    {
        var root = new ChainRoot { Middle = new ChainMiddle() };
        var seen = Watch(root);

        root.Middle.Flag = true;

        Assert("dependent of changed path notified",
            seen.Contains(nameof(ChainRoot.ReadsLocalFieldAndOneLink)));
        Assert("dependent of other path untouched",
            !seen.Contains(nameof(ChainRoot.ReadsThreeLinks)));
    }

    private static void SameValueDoesNotNotify()
    {
        var root = new ChainRoot();
        var seen = Watch(root);

        root.LocalField = root.LocalField;

        Assert("same value not notified", seen.Count == 0);
    }

    private static void SameInstanceKeepsSubscriptions()
    {
        var root = new ChainRoot { Middle = new ChainMiddle() };
        root.Middle = root.Middle;
        root.Middle.Items = root.Middle.Items;

        var seen = Watch(root);
        root.Middle.Items.Add("added");

        Assert("same instance keeps one collection subscription",
            seen.Count(name => name == nameof(ChainRoot.ReadsCollectionCount)) == 1);
    }

    private static void AssigningInOwnChangeHandlerTerminates()
    {
        var leaf = new ChainLeaf();
        var calls = 0;
        leaf.PropertyChanged += (_, _) =>
        {
            calls++;
            leaf.Flag = true;
        };

        leaf.Flag = true;

        Assert("assigning in own change handler terminates", calls == 1);
    }

    private static List<string> Watch(INotifyPropertyChanged source)
    {
        var seen = new List<string>();
        source.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null) seen.Add(e.PropertyName);
        };
        return seen;
    }

    private static void Assert(string label, bool condition)
    {
        if (condition)
        {
            Console.WriteLine($"  pass  {label}");
            return;
        }

        failures++;
        Console.WriteLine($"  FAIL  {label}");
    }
}
