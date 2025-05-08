using System.Reflection;
using Duo;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

internal class Program
{
    private static bool silentMode = false;
    private static string factor = "push";
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
            Console.WriteLine("Usage: DuoAuth /username <username> [/passcode <passcode>] [/silent true|false]");
            return 1;
        }

        if(String.IsNullOrEmpty(config["DUO_KEYS:IKEY"]) || 
            String.IsNullOrEmpty(config["DUO_KEYS:SKEY"]) ||
            String.IsNullOrEmpty(config["DUO_KEYS:HOST"])){
                Console.WriteLine("Invalid configuration. Check appsettings.json");
                return 2;
            }

        
        if(!String.IsNullOrEmpty(config["passcode"])){
            factor = "passcode";
        }

        DuoApi api = new (config["DUO_KEYS:IKEY"]!, config["DUO_KEYS:SKEY"]!, config["DUO_KEYS:HOST"]!);
        
        Out($"Sending Authentication Request for {config["username"]}...");

        var parameters = new Dictionary<string, string>(){
            {"username",config["username"]!},
            {"factor",factor}
        };

        if(factor == "passcode"){
            parameters.Add("passcode",config["passcode"]!);
        }else{
            parameters.Add("device","auto");
        }

        var responseJson = api.ApiCall("POST", "/auth/v2/auth", parameters);

        if(responseJson.Contains("OK")){
            var responseObj = JsonConvert.DeserializeObject<DuoResponse>(responseJson);
            if(responseObj?.response?.result == "deny"){
            Out($"Authentication Failed. {responseObj.response.status_msg}");
                return 1;
            }else if(responseObj?.response?.result == "allow"){
                Out(responseObj.response.status_msg??"Success");
                    return 0;
            }else{
                Out("Unrecognized response/result");
                return 2;
            }          
        }else{
            var fail = JsonConvert.DeserializeObject<FailResponse>(responseJson);
            Out($"Authentcation request failed: {fail?.message} - {fail?.message_detail}");
            return 3;
        }


    }

    private static void Out(string s){
        if(!silentMode) Console.WriteLine(s);
    }
}