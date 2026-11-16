namespace ClaudeTray.Core;

/// <summary>Varijable okruženja hooka: CLAUDE_CODE_ENTRYPOINT, TERM_PROGRAM, CLAUDE_PROJECT_DIR.</summary>
public sealed record HookEnvironment(string Entrypoint, string TermProgram, string ProjectDir);
