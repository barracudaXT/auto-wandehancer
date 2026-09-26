using System.Reflection;

// This fork's own version. It is independent of upstream's numbering: upstream
// changes arrive whenever we next bump, and a fork-only fix can ship without
// waiting for them. Do not tie this to an upstream release number -- an install
// compares its version to the newest release tag, so reusing a number the
// installed build already has makes the update invisible to it.
[assembly: AssemblyVersion("2.1.3.0")]
[assembly: AssemblyFileVersion("2.1.3.0")]
