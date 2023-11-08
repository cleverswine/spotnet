# spotnet

Some experimental Spotify stuff with .NET

Always a work in progress...

## Spotify API

* Spotify developer dashboard: https://developer.spotify.com/dashboard
* Spotify API docs: https://developer.spotify.com/documentation/web-api

## Dev notes...

enable secret storage

```bash
dotnet user-secrets init
```

add auth secrets

```bash
dotnet user-secrets set "Authentication:Spotify:ClientId" "<client-id>"
dotnet user-secrets set "Authentication:Spotify:ClientSecret" "<client-secret>"
```

## Get an intial token

Run the ./src/web app and then navigate to http://localhost:5130/login

## Do stuff

After getting a token, run the ./src/cli app. The cli takes care of refreshing expired tokens.