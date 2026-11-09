# Cutsy.PackTool

Validate a pack directory from the repository root.

~~~powershell
dotnet run --project tools/Cutsy.PackTool -- validate packs/cutsy/runtime
~~~

Exit code 0 means the pack is usable. Optional sound failures appear as warnings. Exit code 1 means pack validation failed. Exit code 2 indicates incorrect arguments.
