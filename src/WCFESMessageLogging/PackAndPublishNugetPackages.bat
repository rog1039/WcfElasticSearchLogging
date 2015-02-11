nuget pack WCFESMessageLogging.csproj -Symbols
nuget push *.nupkg -source $serverURL -apikey $apiKey