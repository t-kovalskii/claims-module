targetScope = 'resourceGroup'

param location string
param environmentName string
param uniqueSuffix string

param sqlAdministratorLogin string
@secure()
param sqlAdministratorLoginPassword string
@secure()
param jwtSecretKey string

// Mirrors modules/appservice.bicep's own `appName` var — recomputed here (rather than read from
// appService.outputs.appName) because a child resource's `name` property must be computable
// before deployment starts, which a module output isn't, even though this value in fact always
// is.
var appServiceName = 'app-claimsmodule-api-${environmentName}-${uniqueSuffix}'

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

// Linking a Static Web App backend auto-enables the App Service's built-in Authentication (Easy
// Auth) with no identity provider configured — returning 401 on every route — as a side effect of
// provisioning the Microsoft.Web/staticSites/linkedBackends resource above. An explicit dependsOn
// on the staticWebApp module (Bicep won't infer one; nothing here consumes its outputs) makes
// this disable deploy *after* that side effect instead of racing it, which is what makes Ticket
// 11's "re-run to reproduce the environment from scratch" actually true rather than silently
// landing in a locked-out state every redeploy.
resource disableEasyAuth 'Microsoft.Web/sites/config@2023-01-01' = {
  name: '${appServiceName}/authsettingsV2'
  properties: {
    platform: {
      enabled: false
    }
  }
  dependsOn: [
    appService
    staticWebApp
  ]
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
