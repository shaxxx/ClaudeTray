namespace ClaudeTray.Tests;

/// <summary>Privremena mapa koja se briše na kraju testa.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ct-test-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string Sub(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

/// <summary>Korijen repozitorija: prva mapa iznad izlaza testova koja sadrži ClaudeTray.sln.</summary>
public static class Repo
{
    public static string Root { get; } = Find();

    static string Find()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(System.IO.Path.Combine(d.FullName, "ClaudeTray.sln"))) return d.FullName;
        throw new InvalidOperationException("ClaudeTray.sln nije pronađen iznad " + AppContext.BaseDirectory);
    }

    public static string Publish(string file) => System.IO.Path.Combine(Root, "artifacts", "publish", file);
}

/// <summary>Testovi koji mijenjaju varijable okruženja procesa ne smiju se izvoditi paralelno.</summary>
[CollectionDefinition("Env", DisableParallelization = true)]
public sealed class EnvCollection;

/// <summary>Mjerenja brzine izvode se sama, bez opterećenja paralelnih testova.</summary>
[CollectionDefinition("Perf", DisableParallelization = true)]
public sealed class PerfCollection;

/// <summary>Postavlja varijablu okruženja i vraća staru vrijednost na kraju.</summary>
public sealed class EnvVar : IDisposable
{
    readonly string _name;
    readonly string? _old;

    public EnvVar(string name, string? value)
    {
        _name = name;
        _old = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_name, _old);
}
