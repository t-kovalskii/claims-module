targetScope = 'subscription'

@description('Short environment name used in resource naming, e.g. prod')
param environmentName string = 'prod'

@description('Azure region for all resources')
param location string = 'eastus2'

param sqlAdministratorLogin string = 'claimsadmin'

@secure()
param sqlAdministratorLoginPassword string

@secure()
param jwtSecretKey string

var resourceGroupName = 'rg-claimsmodule-${environmentName}'
var uniqueSuffix = take(uniqueString(subscription().id, environmentName), 6)

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
}

module resources 'resources.bicep' = {
  name: 'claimsmodule-resources'
  scope: rg
  params: {
    location: location
    environmentName: environmentName
    uniqueSuffix: uniqueSuffix
    sqlAdministratorLogin: sqlAdministratorLogin
    sqlAdministratorLoginPassword: sqlAdministratorLoginPassword
    jwtSecretKey: jwtSecretKey
  }
}

output resourceGroupName string = resourceGroupName
output backendHostName string = resources.outputs.backendHostName
output backendAppName string = resources.outputs.backendAppName
output staticWebAppHostName string = resources.outputs.staticWebAppHostName
output sqlServerFqdn string = resources.outputs.sqlServerFqdn
output storageAccountName string = resources.outputs.storageAccountName
