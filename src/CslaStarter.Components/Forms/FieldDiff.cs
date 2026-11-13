namespace CslaStarter.Components.Forms;

/// <summary>
/// One value that differs between two copies of an object. <see cref="Path"/> is "" for the root and
/// "Tasks[&lt;key&gt;]" for a child; a child present in only one copy has an empty <see cref="Property"/> and
/// true/false in <see cref="Theirs"/> and <see cref="Mine"/>.
/// </summary>
public sealed record FieldDifference(string Path, string Property, string FriendlyName, object? Theirs, object? Mine)
{
    public string Key => $"{Path}.{Property}";
}

/// <summary>Which editable values differ between their copy and mine: root first, then children matched by key.</summary>
public static class FieldDiff
{
    public static IReadOnlyList<FieldDifference> Between(object theirs, object mine)
    {
        ArgumentNullException.ThrowIfNull(theirs);
        ArgumentNullException.ThrowIfNull(mine);
        var differences = new List<FieldDifference>();
        Compare(string.Empty, theirs, mine, differences);
        foreach (var (property, theirItems) in ObjectGraph.ChildLists(theirs))
        {
            var mineItems = ObjectGraph.ByKey((System.Collections.IList)ObjectGraph.Read(mine, property)!);
            var theirsByKey = ObjectGraph.ByKey(theirItems);
            foreach (var key in theirsByKey.Keys.Concat(mineItems.Keys.Where(k => !theirsByKey.ContainsKey(k))))
            {
                var path = $"{property.Name}[{key}]";
                if (theirsByKey.TryGetValue(key, out var theirChild) && mineItems.TryGetValue(key, out var myChild))
                {
                    Compare(path, theirChild, myChild, differences);
                }
                else
                {
                    differences.Add(new FieldDifference(path, string.Empty, property.FriendlyName, theirsByKey.ContainsKey(key), mineItems.ContainsKey(key)));
                }
            }
        }

        return differences;
    }

    private static void Compare(string path, object theirs, object mine, List<FieldDifference> differences)
    {
        foreach (var property in ObjectGraph.Values(theirs))
        {
            var theirValue = ObjectGraph.Read(theirs, property);
            var myValue = ObjectGraph.Read(mine, property);
            if (!Equals(theirValue, myValue))
            {
                differences.Add(new FieldDifference(path, property.Name, property.FriendlyName, theirValue, myValue));
            }
        }
    }
}
