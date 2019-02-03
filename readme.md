# cs Runner

Simple command line that can run C# source files like the [go](https://golang.org) or
[dotnet](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet) does.

1. Only the 'run' command works at the moment:

    <pre>
    cs run &lt;file&gt;
    </pre>


2. It does not require a project file of any kind.

3. Only C# source files with all required classes in a single source file
can be run.

4. This has only been tested in an environment with the .NET SDK installed,
so it might not work on machines with only the runtime installed.
