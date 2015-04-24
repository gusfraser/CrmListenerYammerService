CRM Listener Azure Yammer Cloud Service
=============

This solution was presented at [SharePoint Evolution 2015](http://www.sharepointevolution.com) demonstrating the [Azure Service Bus](http://azure.microsoft.com/en-us/services/service-bus/) being used to connect up systems including Microsoft Dynamics CRM 2015 Online, SharePoint 2013 Online and Yammer (this project). 

[Slides from the presentation can be found here](http://www.slideshare.net/gusfraser/aonghus-fraser-share-point-evolution-conference-2015)

Points of note:

0. The Cloud Service project will run as a console application in Azure - this could run anywhere, but will need to be configured to match the Service Definition (ServiceDefinition.csdef), by adding a minimum of two files; typically ServiceConfiguration.Local.cscfg and ServiceConfiguration.Cloud.cscfg
0. The code is "demo code"; no warranties implied etc. - for example Yammer authentication should not be using the global access token but use OAuth2.0 how it was meant to be used (although from a console application, there are some interesting workarounds required..) 

(Blog post about the project)[]

