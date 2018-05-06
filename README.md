# stonehenge
An open source .NET Framework to use Web UI technologies for desktop and/or web applications.

See a (very) short [getting started introdution here](docs/GettingStarted.md).

## Version V3.0
What's new?
This version is based on .NET Standard 2.0.

* Kestrel - the Microsoft web stack for self hosting
* Aurelia client framework
* Newtonsoft.JSON serializer for view models


## Still supported 

V2.0 - .NET Full Framework V4.6
V1.x - .NET Full Framework V4.6


## SampleFull with target framework V4.7.1
The application is able tu use netstandard 2.0 libraries adding the following lines to lines to the csproj file.

	<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
    <GenerateBindingRedirectsOutputType>true</GenerateBindingRedirectsOutputType>

