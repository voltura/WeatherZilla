WeatherZilla

Get and present weather based on data from reliable weather data providers in an intuative way.

One source could be SMHI for Sweden, more info here (Swedish) 

https://www.smhi.se/data/oppna-data/tekniska-fragor-och-svar-1.76975 

https://opendata.smhi.se/apidocs/

https://opendata.smhi.se/apidocs/metobs/index.html

https://opendata.smhi.se/apidocs/metobs/demo.html

https://opendata-download-metfcst.smhi.se/api/category/pmp3g/version/2/geotype/point/lon/18.08/lat/59.31/data.json


Technical help - Azure Web App / DB

https://docs.microsoft.com/en-us/azure/app-service/

https://docs.microsoft.com/en-us/azure/app-service/tutorial-dotnetcore-sqldb-app?tabs=azure-portal%2Cvisualstudio-deploy%2Cdeploy-instructions-azure-portal%2Cazure-portal-logs%2Cazure-portal-resources



Misc;
dotnet tool install -g dotnet-ef

dotnet ef migrations add InitialCreate

dotnet ef database update


## Favorite places and database updates

Sign in, open **Add favorite**, search the weather provider's stations, and add a result. Your favorites link to weather for that place and can be removed. Ownership comes only from the signed-in user; requests cannot choose another user's favorites.

The EF migration adopts the SQL project's existing `AspNetFavorites` table, preserves its rows, and adds generated IDs, a per-user unique index and a user foreign key. Existing duplicate/orphan rows cause migration to fail instead of deleting data.

The existing `release**` deployment workflow now generates an idempotent SQL artifact and applies EF migrations before uploading the Web App. It reuses the existing Azure login secret and the deployed app's `DefaultConnection`; a missing connection, unavailable SQL firewall access or failed migration stops deployment. Azure must allow the runner to reach SQL. No new cloud storage is required; the reviewed SQL is retained as a GitHub Actions artifact. Pushing `master` does not deploy to Azure.

Validation on Windows with SQL Server LocalDB:

```powershell
dotnet build WeatherZilla.WebApp/WeatherZilla.WebApp.csproj -c Release
dotnet run --project tests/FavoritesSmoke/FavoritesSmoke.csproj -c Release
npm ci --prefix tests/validation
npm test --prefix tests/validation
```

The database test uses a unique temporary database and deletes only that test database. The application retains its existing .NET 6 target; this unsupported runtime is a separate modernization task before further production use.
