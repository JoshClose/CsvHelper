<Query Kind="Statements" />

var el = XElement.Load(@"https://packages.nuget.org/v1/FeedService.svc/Packages?$filter=substringof(%27CsvHelper%27,%20Dependencies)%20eq%20true&$select=Id,Dependencies");
//el.Attributes().Dump();
XNamespace nsm = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
XNamespace nsd = "http://schemas.microsoft.com/ado/2007/08/dataservices";
el.Descendants(nsd + "Id").Select(n => n.Value).Distinct().Dump();
el.Dump();
