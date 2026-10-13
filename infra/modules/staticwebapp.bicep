param location string
param environmentName string
param backendAppId string
param backendRegion string

var staticWebAppName = 'swa-claimsmodule-${environmentName}'

// Ticket 11: Standard tier (not Free) is required for "bring your own backend" linking — the
// frontend's apiBaseUrl is the relative '/api', so Static Web Apps must proxy that path to the
// linked App Service server-to-server rather than the browser calling it cross-origin.
resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    buildProperties: {
      skipGithubActionWorkflowGeneration: true
    }
  }
}

resource linkedBackend 'Microsoft.Web/staticSites/linkedBackends@2023-01-01' = {
  parent: staticWebApp
  name: 'claims-api'
  properties: {
    backendResourceId: backendAppId
    region: backendRegion
  }
}

output staticWebAppName string = staticWebAppName
output defaultHostname string = staticWebApp.properties.defaultHostname
