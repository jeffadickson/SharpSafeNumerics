using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DeepDiveWasm;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Lab>("#app");
await builder.Build().RunAsync();
