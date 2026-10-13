param location string
param environmentName string
param uniqueSuffix string

@secure()
param sqlConnectionString string
@secure()
param jwtSecretKey string
@secure()
param storageConnectionString string
param corsAllowedOrigins array = []

var planName = 'plan-claimsmodule-${environmentName}'
var appName = 'app-claimsmodule-api-${environmentName}-${uniqueSuffix}'

resource plan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: planName
  location: location
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource api 'Microsoft.Web/sites@2023-01-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ConnectionStrings__ClaimsModuleDb', value: sqlConnectionString }
        { name: 'Jwt__SecretKey', value: jwtSecretKey }
        { name: 'Jwt__Issuer', value: 'ClaimsModule.Api' }
        { name: 'Jwt__Audience', value: 'ClaimsModule.Clients' }
        { name: 'Jwt__ExpiryMinutes', value: '60' }
        { name: 'Storage__Provider', value: 'AzureBlob' }
        { name: 'Storage__AzureBlob__ConnectionString', value: storageConnectionString }
        { name: 'Storage__AzureBlob__ContainerName', value: 'claim-documents' }
        { name: 'Cors__AllowedOrigins__0', value: length(corsAllowedOrigins) > 0 ? corsAllowedOrigins[0] : '' }
      ]
    }
  }
}

output appName string = appName
output defaultHostName string = api.properties.defaultHostName
output appId string = api.id
