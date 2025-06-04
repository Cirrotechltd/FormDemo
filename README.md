# FormDemo

A dynamic form application built with ASP.NET Core 8.0 that demonstrates integration with Microsoft services including Azure AD, Microsoft Graph API, and SharePoint Online.

![image](https://github.com/user-attachments/assets/d2d4fc42-4fc6-4fa9-8d5c-a42cfd970806)


## Features

- **Dynamic Form Templates**: Easily create and switch between different form templates defined in JSON
- **Microsoft Authentication**: Integration with Azure AD for secure authentication
- **Microsoft Graph API**: Retrieves user data to pre-fill forms for authenticated users
- **SharePoint Integration**: Saves form submissions to a SharePoint list
- **Azure Key Vault Integration**: Securely stores and manages application secrets

## Technology Stack

- ASP.NET Core 8.0 with Razor Pages
- C# / .NET 8
- Microsoft Identity Web for authentication
- Microsoft Graph API for user information
- PnP Framework for SharePoint integration
- Azure Key Vault for secrets management
- Bootstrap 5 for responsive UI

## Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or Visual Studio Code
- An Azure subscription (for Key Vault and Azure AD)
- SharePoint Online subscription (for form submissions)
- Azure CLI (for local development with Key Vault)

## Getting Started

### Local Development Setup

1. Clone the repository
2. Open the solution in Visual Studio 2022 or Visual Studio Code
3. Set up user secrets or Azure Key Vault (see configuration section)
4. Run the application

### Configuration

#### Using Local Secrets (Development)

1. In `appsettings.Development.json`, ensure `KeyVault:UseLocalSecrets` is set to `true`
2. Configure secrets in your local user secrets store:
   - Right-click the project in Visual Studio and select "Manage User Secrets"
   - Add your Azure AD, Graph API, and SharePoint credentials

#### Using Azure Key Vault (Production)

1. Create an Azure Key Vault (see Key Vault setup section)
2. Add your secrets to the Key Vault
3. Set `KeyVault:UseLocalSecrets` to `false` in `appsettings.Development.json`
4. Set the `VaultUri` environment variable or configure it in `launchSettings.json`

## Form Templates

The application uses JSON-based form templates located in the `/forms` directory. Each template defines:

- Form title and description
- Field definitions (name, type, validation rules, etc.)
- Submit button label

### Included Templates

- `user-information-form.json`: Basic user information (name, email)
- `university-information-glasgow.json`: University of Glasgow student information
- `university-information-strathclyde.json`: University of Strathclyde student information

### Creating Custom Templates

To create a custom form template:

1. Add a new JSON file to the `/forms` directory
2. Define the form structure following the established pattern
3. The new template will automatically appear in the template selection dropdown

## SharePoint Integration

The application can save form submissions to a SharePoint list. To configure:

1. Set up a SharePoint site and list
2. Configure the SharePoint settings in configuration:
   - TenantId
   - ClientId
   - Certificate information
   - Site URL
   - List name

## Azure Key Vault Setup

See the [Key Vault README](FormDemo/README.md) for detailed instructions on setting up and using Azure Key Vault with this application.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
