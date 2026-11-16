using System.Text;

namespace ClaudeTray.Hook;

static class StdinReader
{
    /// <summary>Čita stdin do kraja. Ako u zadanom vremenu ne stigne EOF, vraća prazan niz (događaj se ignorira).</summary>
    public static string ReadAll(TimeSpan timeout)
    {
        var task = Task.Run(() =>
        {
            using var reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
            return reader.ReadToEnd();
        });
        return task.Wait(timeout) ? task.Result : "";
    }
}
