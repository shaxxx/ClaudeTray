using ClaudeTray.Model;

namespace ClaudeTray.Tests;

public class TranscriptProbeTests
{
    static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "transcripts", name);

    [Theory]
    [InlineData("interrupt-tail.jsonl", true)]
    [InlineData("interrupt-running-tail.jsonl", true)]
    [InlineData("normal-tail.jsonl", false)]
    [InlineData("blob-old-marker-tail.jsonl", false)]   // stara oznaka samo u api-request-blob, zadnji redak je assistant
    public void Fixtures(string file, bool expected) => Assert.Equal(expected, new TranscriptProbe().EndsWithInterrupt(Fixture(file)));

    [Fact]
    public void InterruptThenMetadata_True()
    {
        using var t = new TempDir();
        string path = t.Sub("t.jsonl");
        File.WriteAllText(path, File.ReadAllText(Fixture("interrupt-tail.jsonl")).TrimEnd() + "\n"
            + """{"type":"last-prompt","lastPrompt":"x"}""" + "\n"
            + """{"type":"ai-title","aiTitle":"x"}""" + "\n"
            + """{"type":"api-request-blob","message":{"role":"user","content":[{"type":"text","text":"bok"}]}}""" + "\n");
        Assert.True(new TranscriptProbe().EndsWithInterrupt(path));
    }

    [Fact]
    public void MissingFile_False() => Assert.False(new TranscriptProbe().EndsWithInterrupt(@"C:\nema\t.jsonl"));

    [Fact]
    public void Cache_RecomputesWhenFileChanges()
    {
        using var t = new TempDir();
        string path = t.Sub("t.jsonl");
        File.Copy(Fixture("normal-tail.jsonl"), path);
        var probe = new TranscriptProbe();
        Assert.False(probe.EndsWithInterrupt(path));

        File.AppendAllText(path, """{"type":"user","message":{"role":"user","content":[{"type":"text","text":"[Request interrupted by user]"}]}}""" + "\n");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
        Assert.True(probe.EndsWithInterrupt(path));
    }
}
