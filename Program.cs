using System.Reflection;
using Duo;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

internal class Program
{
    private static bool silentMode = false;
    private static int Main(string[] args)
    {  
        IConfigurationRoot config = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)??AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json",false)
            .AddCommandLine(args)
            .Build();        
        
        if(!String.IsNullOrEmpty(config["silent"]) && config["silent"]!.ToUpper() == "TRUE"){
            silentMode = true;   
        }

        if(String.IsNullOrEmpty(config["username"])){
            Console.WriteLine("Usage: DuoAuth /username <username>");
            return 1;
        }

        if(String.IsNullOrEmpty(config["DUO_KEYS:IKEY"]) || 
            String.IsNullOrEmpty(config["DUO_KEYS:SKEY"]) ||
            String.IsNullOrEmpty(config["DUO_KEYS:HOST"])){
                Console.WriteLine("Invalid configuration. Check appsettings.json");
                return 2;
            }

        DuoApi api = new (config["DUO_KEYS:IKEY"]!, config["DUO_KEYS:SKEY"]!, config["DUO_KEYS:HOST"]!);
        
        Out($"Sending Authentication Request for {config["username"]}...");

        var responseJson = api.ApiCall("POST", "/auth/v2/auth", new Dictionary<string, string>(){
            {"username",config["username"]!},
            {"factor","auto"},
            {"device","auto"}
        });

        var responseObj = JsonConvert.DeserializeObject<dynamic>(responseJson);

        if(responseObj?.response?.result == null){
            Out($"Failed to authenticate user {config["username"]}");
            return 3;
        }else if(responseObj.response.result == "deny"){
            Out($"Access denied by {config["username"]}");
                return 4;
        }else if(responseObj.response.result == "allow"){
            Out("Access granted");
                return 0;
        }else{
            Out("Unrecognized response/result");
            return 5;
        }          
    }

    private static void Out(string s){
        if(!silentMode) Console.WriteLine(s);
    }
}