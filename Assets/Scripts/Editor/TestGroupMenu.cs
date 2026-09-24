using UnityEditor;

// PsychGame > Test Group menu: picks which group Play mode runs as, since the
// Editor has no page URL to read ?group= from. The choice is remembered
// between Editor sessions. Add a pair of methods here when adding a group.
public static class TestGroupMenu
{
    private const string MenuRoot = "PsychGame/Test Group/";

    [MenuItem(MenuRoot + "Group 1 (original)")]
    private static void SelectGroup1() => Select(1);

    [MenuItem(MenuRoot + "Group 1 (original)", true)]
    private static bool ValidateGroup1() => Validate(MenuRoot + "Group 1 (original)", 1);

    [MenuItem(MenuRoot + "Group 2")]
    private static void SelectGroup2() => Select(2);

    [MenuItem(MenuRoot + "Group 2", true)]
    private static bool ValidateGroup2() => Validate(MenuRoot + "Group 2", 2);

    private static void Select(int group)
    {
        EditorPrefs.SetInt(TestGroups.EditorPrefsKey, group);
    }

    // Shows a checkmark next to the selected group. Always enabled.
    private static bool Validate(string menuPath, int group)
    {
        Menu.SetChecked(menuPath, EditorPrefs.GetInt(TestGroups.EditorPrefsKey, TestGroups.DefaultGroup) == group);
        return true;
    }
}
