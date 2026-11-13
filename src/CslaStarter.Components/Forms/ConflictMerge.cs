using System.Collections;
using Csla.Core;

namespace CslaStarter.Components.Forms;

/// <summary>The fresh copy with my changes applied, and the values both of us changed (the user picks for those).</summary>
public sealed record MergeResult(object Merged, IReadOnlyList<FieldDifference> BothChanged);

/// <summary>
/// "Apply mine" after a concurrency conflict: every value I changed since <c>original</c> is applied onto their fresh
/// copy, and so are my child adds, edits and deletes, matched by key. Their other changes (and their version) stay, so
/// the result saves cleanly. Where we both changed a value, mine wins unless its <see cref="FieldDifference.Key"/> is in
/// <c>keepTheirs</c>. The result is never saved here: it goes back into the form for review.
/// </summary>
public static class ConflictMerge
{
    public static MergeResult Apply(object original, object mine, object theirs, IReadOnlySet<string>? keepTheirs = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(mine);
        ArgumentNullException.ThrowIfNull(theirs);
        keepTheirs ??= new HashSet<string>();
        var bothChanged = new List<FieldDifference>();

        // My new children are moved into their copy, so first bring my edits to edit level 0 (like their fresh copy).
        var flatMine = Flatten(mine);
        MergeValues(string.Empty, original, flatMine, theirs, keepTheirs, bothChanged);
        foreach (var (property, theirItems) in ObjectGraph.ChildLists(theirs))
        {
            var originalItems = ObjectGraph.ByKey((IList)ObjectGraph.Read(original, property)!);
            var myItems = ObjectGraph.ByKey((IList)ObjectGraph.Read(flatMine, property)!);
            var theirsByKey = ObjectGraph.ByKey(theirItems);

            foreach (var (key, myChild) in myItems)
            {
                var path = $"{property.Name}[{key}]";
                if (originalItems.TryGetValue(key, out var originalChild))
                {
                    if (theirsByKey.TryGetValue(key, out var theirChild))
                    {
                        MergeValues(path, originalChild, myChild, theirChild, keepTheirs, bothChanged);
                    }
                }
                else if (!theirsByKey.ContainsKey(key))
                {
                    theirItems.Add(myChild);
                }
            }

            foreach (var (key, _) in originalItems.Where(o => !myItems.ContainsKey(o.Key)))
            {
                if (theirsByKey.TryGetValue(key, out var theirChild))
                {
                    theirItems.Remove(theirChild);
                }
            }
        }

        (theirs as ICheckRules)?.CheckRules();
        return new MergeResult(theirs, bothChanged);
    }

    private static void MergeValues(string path, object original, object mine, object theirs, IReadOnlySet<string> keepTheirs, List<FieldDifference> bothChanged)
    {
        foreach (var property in ObjectGraph.Values(theirs))
        {
            var originalValue = ObjectGraph.Read(original, property);
            var myValue = ObjectGraph.Read(mine, property);
            if (Equals(myValue, originalValue))
            {
                continue;
            }

            var theirValue = ObjectGraph.Read(theirs, property);
            var difference = new FieldDifference(path, property.Name, property.FriendlyName, theirValue, myValue, FieldDiff.IsSensitive(theirs, property.Name));
            if (!Equals(theirValue, originalValue) && !Equals(theirValue, myValue))
            {
                bothChanged.Add(difference);
            }

            if (!keepTheirs.Contains(difference.Key))
            {
                ObjectGraph.Write(theirs, property, myValue);
            }
        }
    }

    /// <summary>A copy of <paramref name="mine"/> with every pending edit level applied (edit level 0).</summary>
    public static object Flatten(object mine)
    {
        var context = ((IUseApplicationContext)mine).ApplicationContext;
        var copy = ObjectCloner.GetInstance(context).Clone(mine)!;
        while (copy is IUndoableObject { EditLevel: > 0 } and ISupportUndo undo)
        {
            undo.ApplyEdit();
        }

        return copy;
    }
}
