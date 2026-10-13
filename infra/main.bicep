targetScope = 'subscription'

@description('Short environment name used in resource naming, e.g. prod')
param environmentName string = 'prod'

@description('Azure region for all resources')
param location string = 'centralus'

param sqlAdministratorLogin string = 'claimsadmin'

@secure()
param sqlAdministratorLoginPassword string

@secure()
param jwtSecretKey string

var resourceGroupName = 'rg-claimsmodule-${environmentName}'
var uniqueSuffix = take(uniqueString(subscription().id, environmentName, location), 6)

// A resource group's own location is immutable once created and otherwise unrelated to where
// its resources live — pinned independently of `location` (which drives where the actual
// billable resources go, and may need to change if a region rejects provisioning for this
// subscription) so re-running this template against a new region doesn't try to "move" the
// already-existing resource group and fail with InvalidResourceGroupLocation.
resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: 'eastus2'
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
