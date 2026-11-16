using ClaudeTray.Core;

namespace ClaudeTray.Tests;

public class StateStoreTests
{
    static SessionRecord Rec(string id = "s1") => new()
    {
        SessionId = id, State = SessionStatus.Working, Detail = "Uređuje", ProjectDir = @"C:\projekti\Novi modul",
        Started = true, Pid = 42, PidStartedAt = 7, UpdatedAt = 1_000,
    };

    [Theory]
    [InlineData("abc-123_X.y", "abc-123_X.y")]
    [InlineData("../..\\x y", "....xy")]
    [InlineData("", "unknown")]
    [InlineData(null, "unknown")]
    [InlineData("č/ž", "unknown")]
    public void SafeId_Cases(string? input, string expected) => Assert.Equal(expected, StateStore.SafeId(input));

    [Fact]
    public void SafeId_LongIdTruncatedTo64() => Assert.Equal(64, StateStore.SafeId(new string('a', 100)).Length);

    [Fact]
    public void WriteThenRead_RoundTrip()
    {
        using var t = new TempDir();
        var r = Rec();
        StateStore.WriteAtomic(t.Path, r);
        Assert.Equal(r, StateStore.TryRead(StateStore.PathFor(t.Path, r.SessionId)));
    }

    [Fact]
    public void Write_NoTmpLeft_CreatesDir()
    {
        using var t = new TempDir();
        string dir = t.Sub("state");
        StateStore.WriteAtomic(dir, Rec());
        Assert.True(File.Exists(Path.Combine(dir, "s1.json")));
        Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
    }

    [Fact]
    public void TryRead_GarbageOrMissing_Null()
    {
        using var t = new TempDir();
        string garbage = t.Sub("g.json");
        File.WriteAllText(garbage, "{{{");
        Assert.Null(StateStore.TryRead(garbage));
        Assert.Null(StateStore.TryRead(t.Sub("nema.json")));
    }

    [Fact]
    public void ReadAll_SkipsGarbageAndLocked()
    {
        using var t = new TempDir();
        StateStore.WriteAtomic(t.Path, Rec("a"));
        File.WriteAllText(t.Sub("b.json"), "{{{");
        StateStore.WriteAtomic(t.Path, Rec("c"));
        using var locked = new FileStream(t.Sub("c.json"), FileMode.Open, FileAccess.Read, FileShare.None);

        var all = StateStore.ReadAll(t.Path);

        Assert.Single(all);
        Assert.Equal("a", all[0].SessionId);
    }

    [Fact]
    public void ReadAll_MissingDir_Empty()
    {
        using var t = new TempDir();
        Assert.Empty(StateStore.ReadAll(t.Sub("nema")));
    }

    [Fact]
    public void ParallelWrites_FinalFileValid()
    {
        using var t = new TempDir();
        Parallel.For(0, 50, i => StateStore.WriteAtomic(t.Path, Rec() with { UpdatedAt = i }));
        Assert.NotNull(StateStore.TryRead(StateStore.PathFor(t.Path, "s1")));
        Assert.Empty(Directory.GetFiles(t.Path, "*.tmp"));
    }

    [Fact]
    public void Read_DoesNotBlockConcurrentWrite()
    {
        using var t = new TempDir();
        StateStore.WriteAtomic(t.Path, Rec());
        string path = StateStore.PathFor(t.Path, "s1");
        using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            StateStore.WriteAtomic(t.Path, Rec() with { Detail = "Čita" });
        }
        Assert.Equal("Čita", StateStore.TryRead(path)!.Detail);
    }

    [Fact]
    public void Delete_RemovesFile_MissingNoThrow()
    {
        using var t = new TempDir();
        StateStore.WriteAtomic(t.Path, Rec());
        StateStore.Delete(t.Path, "s1");
        Assert.False(File.Exists(StateStore.PathFor(t.Path, "s1")));
        StateStore.Delete(t.Path, "s1");
        StateStore.Delete(t.Sub("nema"), "s1");
    }
}
