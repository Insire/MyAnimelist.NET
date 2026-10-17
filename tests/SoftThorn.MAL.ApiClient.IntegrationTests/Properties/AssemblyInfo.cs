using Microsoft.Extensions.Configuration.UserSecrets;
using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("SoftThorn.MAL.ApiClient.IntegrationTests")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("66a6e703-08fa-46ce-88e0-5165b822b529")]

[assembly: UserSecretsId("cc5b08ee-4b6b-4874-bfba-75c8768beed0")]

// MAL stops answering some requests when too many arrive at once (they hang until HttpClient's 100s timeout),
// and its API agreement asks to respect usage limits, so the live tests run one at a time
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]