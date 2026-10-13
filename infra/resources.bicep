targetScope = 'resourceGroup'

param location string
param environmentName string
param uniqueSuffix string

param sqlAdministratorLogin string
@secure()
param sqlAdministratorLoginPassword string
@secure()
param jwtSecretKey string

module sql 'modules/sql.bicep' = {
  name: 'sql-deployment'
  params: {
    location: location
    environmentName: environmentName
    uniqueSuffix: uniqueSuffix
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorLoginPassword
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage-deployment'
  params: {
    location: location
    uniqueSuffix: uniqueSuffix
  }
}

module appService 'modules/appservice.bicep' = {
  name: 'appservice-deployment'
  params: {
    location: location
    environmentName: environmentName
    uniqueSuffix: uniqueSuffix
    sqlConnectionString: sql.outputs.connectionString
    jwtSecretKey: jwtSecretKey
    storageConnectionString: storage.outputs.connectionString
  }
}

module staticWebApp 'modules/staticwebapp.bicep' = {
  name: 'staticwebapp-deployment'
  params: {
    location: location
    environmentName: environmentName
    backendAppId: appService.outputs.appId
    backendRegion: location
  }
}

// Static Web Apps assigns its own hostname at creation time (not derivable from the resource
// name the way App Service's is) — so the backend's CORS allow-list can only be set as a
// follow-up step once staticWebAppHostName below is known, not inside this same deployment.
output backendHostName string = appService.outputs.defaultHostName
output backendAppName string = appService.outputs.appName
output staticWebAppName string = staticWebApp.outputs.staticWebAppName
output staticWebAppHostName string = staticWebApp.outputs.defaultHostname
output sqlServerFqdn string = sql.outputs.sqlServerFqdn
output storageAccountName string = storage.outputs.storageAccountName
