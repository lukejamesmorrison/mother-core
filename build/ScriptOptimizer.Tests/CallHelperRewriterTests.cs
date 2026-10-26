using Microsoft.CodeAnalysis;
using NUnit.Framework;

[TestFixture]
public class CallHelperRewriterTests
{
    static readonly List<MetadataReference> References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
        .ToList();

    // The wrapper's using directives name game namespaces that do not exist here, so only errors in the script count.
    static List<Diagnostic> ScriptErrors(string script) =>
        ScriptCompilation.Create(script, References).Errors()
            .Where(d => d.Location.SourceSpan.Start >= ScriptCompilation.Prefix.Length)
            .ToList();

    // Program's body ends with "}" the way the packager emits it, followed by the other types. The last
    // type is left open, like the packager does.
    static string Script(string members, string types = "") =>
        $"public Program(){{}}{members}}}{types}class Z{{";

    static CallHelperRewriter.Result? Rewrite(string script)
    {
        Assert.That(ScriptErrors(script), Is.Empty, "the input script must compile");
        var result = CallHelperRewriter.Rewrite(script, ScriptCompilation.Create(script, References));
        if (result != null)
            Assert.That(ScriptErrors(result.Script), Is.Empty, "the rewritten script must compile:\n" + result.Script);
        return result;
    }

    // Enough repeated calls for a helper to repay its declaration.
    static string Calls(int count, Func<int, string> call) =>
        string.Concat(Enumerable.Range(0, count).Select(i => $"public bool M{i}(string s,string t){{return {call(i)};}}"));

    [Test]
    public void IsNullOrEmpty_BecomesExtensionCall()
    {
        var result = Rewrite(Script(Calls(10, i => "string.IsNullOrEmpty(s)")));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Script, Does.Contain("return s.").And.Not.Contain("return string.IsNullOrEmpty(s)"));
        Assert.That(result.Script, Does.Contain("=>string.IsNullOrEmpty(a);"));
    }

    [Test]
    public void IsNullOrEmpty_ParenthesisesNonPrimaryReceiver()
    {
        var result = Rewrite(Script(Calls(10, i => "string.IsNullOrEmpty(s+t)")));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Script, Does.Contain("(s+t)."));
    }

    [Test]
    public void IsNullOrEmpty_NullLiteralIsLeftAlone()
    {
        Assert.That(Rewrite(Script(Calls(10, i => "string.IsNullOrEmpty(null)"))), Is.Null);
    }

    [Test]
    public void Substring_RenamesOnlyTheMethodName()
    {
        var result = Rewrite(Script(Calls(10, i => "s.Substring(1)==t.Substring(2,3)")));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Script, Does.Not.Contain("s.Substring(1)").And.Not.Contain("t.Substring(2,3)"));
        Assert.That(result.Helpers, Is.EqualTo(2));
    }

    [Test]
    public void NestedCalls_AreBothRewritten()
    {
        var result = Rewrite(Script(Calls(10, i => "string.IsNullOrEmpty(s.Substring(1))")));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Script, Does.Not.Contain("s.Substring(1)").And.Not.Contain("return string.IsNullOrEmpty"));
    }

    [Test]
    public void Dictionary_ContainsKeyAndTryGetValue_UseGenericHelpers()
    {
        var members = string.Concat(Enumerable.Range(0, 10).Select(i =>
            $"public bool M{i}(Dictionary<string,int> d,string k){{int v;return d.ContainsKey(k)&&d.TryGetValue(k,out v);}}"));

        var result = Rewrite(Script(members));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Script, Does.Not.Contain("d.ContainsKey(k)").And.Not.Contain("d.TryGetValue(k,out v)"));
        Assert.That(result.Script, Does.Contain("<K,V>(this Dictionary<K,V> a,K b)=>a.ContainsKey(b);"));
        Assert.That(result.Script, Does.Contain("<K,V>(this Dictionary<K,V> a,K b,out V c)=>a.TryGetValue(b,out c);"));
    }

    [Test]
    public void Dictionary_CallsThroughAnInterfaceAreLeftAlone()
    {
        var members = string.Concat(Enumerable.Range(0, 10).Select(i =>
            $"public bool M{i}(IDictionary<string,int> d,string k){{return d.ContainsKey(k);}}"));

        Assert.That(Rewrite(Script(members)), Is.Null);
    }

    [Test]
    public void Dictionary_SubclassReceiverStillBinds()
    {
        var members = string.Concat(Enumerable.Range(0, 10).Select(i =>
            $"public bool M{i}(A d,string k){{return d.ContainsKey(k);}}"));

        var result = Rewrite(Script(members, "public class A:Dictionary<string,int>{}"));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Script, Does.Not.Contain("d.ContainsKey(k)"));
    }

    [Test]
    public void DoubleTryParse_KeepsOutArgument()
    {
        var members = string.Concat(Enumerable.Range(0, 10).Select(i =>
            $"public bool M{i}(string s){{double v;return double.TryParse(s,out v);}}"));

        var result = Rewrite(Script(members));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Script, Does.Not.Contain("double.TryParse(s,out v)"));
        Assert.That(result.Script, Does.Contain("this string a,out double b)=>double.TryParse(a,out b);"));
    }

    [Test]
    public void ConditionalAccess_IsRenamed()
    {
        var result = Rewrite(Script(Calls(10, i => "s?.Substring(1)==t")));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Script, Does.Not.Contain("Substring(1)"));
    }

    [Test]
    public void FewCalls_DoNotRepayTheHelper_ScriptIsUntouched()
    {
        Assert.That(Rewrite(Script(Calls(1, i => "string.IsNullOrEmpty(s)"))), Is.Null);
    }

    [Test]
    public void NamedArguments_AreLeftAlone()
    {
        Assert.That(Rewrite(Script(Calls(10, i => "string.IsNullOrEmpty(value:s)"))), Is.Null);
    }

    [Test]
    public void HelperNames_DoNotCollideWithExistingIdentifiers()
    {
        var result = Rewrite(Script(Calls(10, i => "string.IsNullOrEmpty(s)") + "int A,B,C;"));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Script, Does.Not.Contain("static class A").And.Not.Contain("static class B"));
    }

    [Test]
    public void Names_FallBackToPairsWhenEverySingleCharacterIsUsed()
    {
        var singles = AliasRewriter.Names(new HashSet<string>()).TakeWhile(n => n.Length == 1).ToHashSet();
        var next = AliasRewriter.Names(singles).First();

        Assert.That(singles, Is.Not.Empty);
        Assert.That(next, Has.Length.EqualTo(2));
    }

    [Test]
    public void InstanceCallOnStruct_IsLeftAlone()
    {
        var members = string.Concat(Enumerable.Range(0, 10).Select(i =>
            $"public string M{i}(DateTime d){{return d.ToString();}}"));

        Assert.That(Rewrite(Script(members)), Is.Null);
    }
}
