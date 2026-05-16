git pull origin
Get-ChildItem -Recurse -Directory -Include bin,obj | Remove-Item -Recurse -Force
Remove-Item -Recurse -Force .vs -ErrorAction SilentlyContinue
dotnet restore 
dotnet build